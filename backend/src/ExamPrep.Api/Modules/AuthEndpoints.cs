using System.Security.Claims;
using ExamPrep.Api.Data;
using ExamPrep.Api.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace ExamPrep.Api.Modules;

public class AuthOptions
{
    public string PublicBaseUrl { get; set; } = "http://localhost:4200";
    public int TokenMinutes { get; set; } = 15;
    public int MaxLinksPerEmailPer15Min { get; set; } = 3;
    public List<string> AdminEmails { get; set; } = new();
}

public record RequestLinkDto(string? Email);
public record VerifyDto(string? Token);

public static class AuthEndpoints
{
    public const string CsrfCookie = "XSRF-TOKEN";

    public static void IssueCsrf(HttpContext ctx, IAntiforgery af)
    {
        var tokens = af.GetAndStoreTokens(ctx);
        ctx.Response.Cookies.Append(CsrfCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // must be readable by the SPA to echo it in the X-XSRF-TOKEN header
            SameSite = SameSiteMode.Strict,
            Secure = ctx.Request.IsHttps,
            Path = "/"
        });
    }

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapGet("/csrf", (HttpContext ctx, IAntiforgery af) => { IssueCsrf(ctx, af); return Results.NoContent(); });

        g.MapPost("/request-link", async (RequestLinkDto dto, AppDbContext db, IEmailSender mail, TimeProvider clock,
            Microsoft.Extensions.Options.IOptions<AuthOptions> opt, ILogger<Program> log, CancellationToken ct) =>
        {
            // Identical response for every input to avoid account enumeration.
            var accepted = Results.Accepted(value: new { message = "If the address is valid, a sign-in link has been sent." });
            if (!Emails.IsValid(dto.Email)) return Results.BadRequest(new { error = "invalid_email", message = "Enter a valid email address." });
            var email = dto.Email!.Trim();
            var norm = Emails.Normalize(email);
            var now = clock.GetUtcNow();
            var recent = await db.LoginTokens.CountAsync(t => t.NormalizedEmail == norm && t.CreatedAt > now.AddMinutes(-15), ct);
            if (recent >= opt.Value.MaxLinksPerEmailPer15Min)
            {
                log.LogWarning("Sign-in link throttled for an address");
                return accepted;
            }
            var token = Crypto.NewToken();
            db.LoginTokens.Add(new LoginToken
            {
                NormalizedEmail = norm, TokenHash = Crypto.Sha256Hex(token), CreatedAt = now,
                ExpiresAt = now.AddMinutes(opt.Value.TokenMinutes)
            });
            await db.SaveChangesAsync(ct);
            // Fragment keeps the token out of server logs and Referer headers; the SPA posts it to /verify.
            var link = $"{opt.Value.PublicBaseUrl.TrimEnd('/')}/auth/verify#token={token}";
            await mail.SendAsync(email, "Your sign-in link — Claude certification practice",
                $"Use this single-use link within {opt.Value.TokenMinutes} minutes to sign in:\n\n{link}\n\n" +
                "If you did not request it, ignore this message. This practice platform is independent and not affiliated with Anthropic or Pearson VUE.", ct);
            return accepted;
        }).RequireRateLimiting("auth");

        g.MapPost("/verify", async (VerifyDto dto, HttpContext ctx, AppDbContext db, TimeProvider clock, IAntiforgery af,
            Microsoft.Extensions.Options.IOptions<AuthOptions> opt, Audit audit, CancellationToken ct) =>
        {
            var invalid = Problems.Error(400, "invalid_or_expired", "This sign-in link is invalid, expired or already used. Request a new one.");
            if (string.IsNullOrWhiteSpace(dto.Token) || dto.Token.Length > 100) return invalid;
            var hash = Crypto.Sha256Hex(dto.Token);
            var now = clock.GetUtcNow();
            // Atomic single-use consumption: only one concurrent request can flip ConsumedAt.
            var consumed = await db.LoginTokens
                .Where(t => t.TokenHash == hash && t.ConsumedAt == null && t.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), ct);
            if (consumed != 1) return invalid;
            var norm = await db.LoginTokens.Where(t => t.TokenHash == hash).Select(t => t.NormalizedEmail).SingleAsync(ct);

            var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == norm, ct);
            if (user is null)
            {
                user = new User { Email = norm, NormalizedEmail = norm, CreatedAt = now, EmailVerifiedAt = now };
                db.Users.Add(user);
                audit.Record(user.Id, "user.registered", "user", user.Id.ToString());
            }
            user.EmailVerifiedAt ??= now;
            user.LastLoginAt = now;
            if (opt.Value.AdminEmails.Any(a => Emails.Normalize(a) == norm) && user.Role != Roles.Admin)
            {
                user.Role = Roles.Admin;
                audit.Record(user.Id, "user.admin_bootstrap", "user", user.Id.ToString());
            }
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                user = await db.Users.SingleAsync(u => u.NormalizedEmail == norm, ct);
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            }, CookieAuthenticationDefaults.AuthenticationScheme));
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            ctx.User = principal;
            IssueCsrf(ctx, af);
            return Results.Ok(new { id = user.Id, email = user.Email, role = user.Role });
        }).RequireRateLimiting("auth");

        g.MapPost("/logout", async (HttpContext ctx, IAntiforgery af) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity());
            IssueCsrf(ctx, af);
            return Results.NoContent();
        });

        g.MapGet("/me", async (ClaimsPrincipal p, AppDbContext db, CancellationToken ct) =>
        {
            if (p.Identity?.IsAuthenticated != true) return Results.Ok(new { authenticated = false });
            var u = await db.Users.FindAsync(new object[] { p.UserId() }, ct);
            if (u is null) return Results.Ok(new { authenticated = false });
            return Results.Ok(new { authenticated = true, id = u.Id, email = u.Email, role = u.Role });
        });

        // Account data removal: deletes the user's attempts and the account itself. Audit keeps only the id.
        app.MapDelete("/api/me", async (ClaimsPrincipal p, HttpContext ctx, AppDbContext db, Audit audit, CancellationToken ct) =>
        {
            var id = p.UserId();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Attempts.Where(a => a.UserId == id).ExecuteDeleteAsync(ct);
            var u = await db.Users.FindAsync(new object[] { id }, ct);
            if (u is not null)
            {
                await db.LoginTokens.Where(t => t.NormalizedEmail == u.NormalizedEmail).ExecuteDeleteAsync(ct);
                db.Users.Remove(u);
            }
            audit.Record(id, "user.deleted", "user", id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
