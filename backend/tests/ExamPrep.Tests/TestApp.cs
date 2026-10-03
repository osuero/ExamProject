using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExamPrep.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ExamPrep.Tests;

/// <summary>Controllable clock so time-dependent rules (expiry, token lifetime) are tested deterministically.</summary>
public class MutableClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly object _lock = new();
    public override DateTimeOffset GetUtcNow() { lock (_lock) return _now; }
    public void Advance(TimeSpan t) { lock (_lock) _now = _now.Add(t); }
}

/// <summary>Runs the real API against a fresh PostgreSQL database with the real content bootstrap.</summary>
public class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public MutableClock Clock { get; } = new();
    public string DbName { get; } = "examtest_" + Guid.NewGuid().ToString("N")[..12];
    public static string AdminConn => Environment.GetEnvironmentVariable("TEST_PG_ADMIN") ?? "Host=localhost;Port=5432;Database=postgres;Username=examdev;Password=examdev_local_only";
    public string Conn => new NpgsqlConnectionStringBuilder(AdminConn) { Database = DbName }.ToString();

    public static string RepoRoot
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "content", "banks"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", Conn);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Content:Directory", Path.Combine(RepoRoot, "content"));
        builder.UseSetting("Content:BootstrapOnStartup", "true");
        builder.UseSetting("Email:Mode", "outbox");
        builder.UseSetting("Workers:Expiry", "false");
        builder.UseSetting("RateLimits:AuthPerMinute", "1000");
        builder.UseSetting("Auth:MaxLinksPerEmailPer15Min", "100");
        builder.UseSetting("Auth:AdminEmails:0", "admin@test.local");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async ValueTask InitializeAsync()
    {
        await using var c = new NpgsqlConnection(AdminConn);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{DbName}\"", c);
        await cmd.ExecuteNonQueryAsync();
        _ = Server; // start host: migrate + bootstrap
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var c = new NpgsqlConnection(AdminConn);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{DbName}\" WITH (FORCE)", c);
        await cmd.ExecuteNonQueryAsync();
        GC.SuppressFinalize(this);
    }

    public T WithDb<T>(Func<AppDbContext, T> f)
    {
        using var scope = Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<Api> AnonymousAsync()
    {
        var api = new Api(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false }));
        await api.GetAsync("/api/auth/csrf");
        return api;
    }

    public async Task<Api> SignInAsync(string email)
    {
        var api = await AnonymousAsync();
        var r = await api.PostAsync("/api/auth/request-link", new { email });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        var token = LatestToken(email);
        var v = await api.PostAsync("/api/auth/verify", new { token });
        Assert.Equal(HttpStatusCode.OK, v.StatusCode);
        return api;
    }

    /// <summary>Newest still-unused token sent to the address (the test clock does not tick on its own).</summary>
    public string LatestToken(string email) => WithDb(db =>
    {
        var tokens = db.OutboxEmails.Where(m => m.To == email).ToList()
            .Select(m => Regex.Match(m.Body, @"token=([\w-]+)").Groups[1].Value).ToList();
        var unused = db.LoginTokens.Where(t => t.ConsumedAt == null).Select(t => t.TokenHash).ToHashSet();
        return tokens.Last(t => unused.Contains(ExamPrep.Api.Infrastructure.Crypto.Sha256Hex(t)));
    });
}

/// <summary>Test client that mirrors the SPA: keeps cookies and echoes the XSRF token header.</summary>
public class Api(HttpClient http)
{
    public HttpClient Http { get; } = http;
    public string? Xsrf { get; private set; }
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private void Capture(HttpResponseMessage r)
    {
        if (r.Headers.TryGetValues("Set-Cookie", out var cookies))
            foreach (var c in cookies)
            {
                var m = Regex.Match(c, "^XSRF-TOKEN=([^;]+)");
                if (m.Success) Xsrf = Uri.UnescapeDataString(m.Groups[1].Value);
            }
    }

    private HttpRequestMessage Req(HttpMethod m, string url, object? body, bool csrf)
    {
        var req = new HttpRequestMessage(m, url);
        if (body is not null) req.Content = JsonContent.Create(body, options: Json);
        if (csrf && Xsrf is not null) req.Headers.Add("X-XSRF-TOKEN", Xsrf);
        return req;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod m, string url, object? body = null, bool csrf = true)
    {
        var r = await Http.SendAsync(Req(m, url, body, csrf));
        Capture(r);
        return r;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url);
    public Task<HttpResponseMessage> PostAsync(string url, object? body = null, bool csrf = true) => SendAsync(HttpMethod.Post, url, body ?? new { }, csrf);
    public Task<HttpResponseMessage> PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, body);

    public async Task<JsonElement> JsonAsync(HttpResponseMessage r)
    {
        var s = await r.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrEmpty(s) ? "{}" : s).RootElement.Clone();
    }

    public async Task<JsonElement> GetJsonAsync(string url)
    {
        var r = await GetAsync(url);
        Assert.True(r.IsSuccessStatusCode, $"GET {url} -> {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return await JsonAsync(r);
    }

    public async Task<Guid> StartAsync(object body)
    {
        var r = await PostAsync("/api/attempts", body);
        Assert.True(r.StatusCode == HttpStatusCode.Created, $"start -> {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return (await JsonAsync(r)).GetProperty("id").GetGuid();
    }
}

[CollectionDefinition("app")]
public class AppCollection : ICollectionFixture<TestApp>;
