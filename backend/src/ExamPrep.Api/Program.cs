using System.Threading.RateLimiting;
using ExamPrep.Api.Data;
using ExamPrep.Api.Infrastructure;
using ExamPrep.Api.Modules;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(cfg.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is required.")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<AuthOptions>(cfg.GetSection("Auth"));
builder.Services.Configure<EmailOptions>(cfg.GetSection("Email"));
builder.Services.AddScoped<Audit>();
builder.Services.AddScoped<AttemptService>();
builder.Services.AddScoped<ContentImporter>();
builder.Services.AddScoped<Bootstrapper>();

var emailMode = cfg["Email:Mode"] ?? "outbox";
if (emailMode == "smtp") builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("Email:Mode=outbox is only allowed in Development/Testing. Configure SMTP for other environments.");
    builder.Services.AddScoped<IEmailSender, OutboxEmailSender>();
}

if (cfg.GetValue("Workers:Expiry", true)) builder.Services.AddHostedService<ExpiryWorker>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "examprep.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(o => o.AddPolicy("admin", p => p.RequireRole(Roles.Admin)));
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-XSRF-TOKEN";
    o.Cookie.Name = "examprep.af";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    var permit = cfg.GetValue("RateLimits:AuthPerMinute", 10);
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = ContentImporter.MaxBytes + 64 * 1024);
builder.Services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (ctx.Request.Path.StartsWithSegments("/api")) h["Cache-Control"] = "no-store";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// CSRF: every state-changing /api request must echo the antiforgery token in X-XSRF-TOKEN.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method)
        && !HttpMethods.IsOptions(ctx.Request.Method))
    {
        var af = ctx.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await af.IsRequestValidAsync(ctx))
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = "csrf_invalid", message = "Security token missing or invalid. Reload the page and try again." });
            return;
        }
    }
    await next();
});

AuthEndpoints.Map(app);
CatalogEndpoints.Map(app);
AttemptEndpoints.Map(app);
AdminEndpoints.Map(app);

app.MapGet("/api/health", async (AppDbContext db) => Results.Ok(new { status = "ok", database = await db.Database.CanConnectAsync() }));

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    // Local mailbox for development only. Never registered in other environments.
    app.MapGet("/api/dev/outbox", async (string? to, AppDbContext db) =>
        Results.Ok(await db.OutboxEmails.Where(m => to == null || m.To == to).OrderByDescending(m => m.CreatedAt).Take(20).ToListAsync()));
}

app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (cfg.GetValue("Database:MigrateOnStartup", false)) await db.Database.MigrateAsync();
    var contentDir = cfg["Content:Directory"];
    if (cfg.GetValue("Content:BootstrapOnStartup", false) && !string.IsNullOrEmpty(contentDir))
        await scope.ServiceProvider.GetRequiredService<Bootstrapper>().RunAsync(contentDir, CancellationToken.None);
}

app.Run();

public partial class Program;
