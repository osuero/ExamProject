using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExamPrep.Api.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ExamPrep.Api.Infrastructure;

public static class Crypto
{
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));
    public static string Sha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    public static string Sha256Hex(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    public static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class Emails
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254) return false;
        try { var a = new System.Net.Mail.MailAddress(email.Trim()); return a.Address == email.Trim() && a.Host.Contains('.'); }
        catch { return false; }
    }
}

public class EmailOptions
{
    /// <summary>"outbox" (development only: stores messages in the database) or "smtp".</summary>
    public string Mode { get; set; } = "outbox";
    public string From { get; set; } = "no-reply@example.test";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpStartTls { get; set; } = true;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

public class OutboxEmailSender(AppDbContext db, TimeProvider clock) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        db.OutboxEmails.Add(new OutboxEmail { To = to, Subject = subject, Body = body, CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
    }
}

public class SmtpEmailSender(Microsoft.Extensions.Options.IOptions<EmailOptions> options, ILogger<SmtpEmailSender> log) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var o = options.Value;
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(o.From));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new TextPart("plain") { Text = body };
        using var client = new SmtpClient();
        await client.ConnectAsync(o.SmtpHost ?? throw new InvalidOperationException("Email:SmtpHost is required."), o.SmtpPort, o.SmtpStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
        if (!string.IsNullOrEmpty(o.SmtpUser)) await client.AuthenticateAsync(o.SmtpUser, o.SmtpPassword ?? "", ct);
        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);
        log.LogInformation("Sign-in email dispatched via SMTP"); // never log the link or token
    }
}

public static class UserExtensions
{
    public static Guid UserId(this ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static bool IsAdmin(this ClaimsPrincipal p) => p.IsInRole(Roles.Admin);
}

public class Audit(AppDbContext db, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public void Record(Guid? actor, string action, string entityType, string entityId, object? data = null) =>
        db.AuditEntries.Add(new AuditEntry
        {
            ActorUserId = actor, Action = action, EntityType = entityType, EntityId = entityId,
            DataJson = data is null ? null : JsonSerializer.Serialize(data, Json), At = clock.GetUtcNow()
        });
}

public static class Problems
{
    public static IResult Error(int status, string code, string message, object? extra = null) =>
        Results.Json(new { error = code, message, details = extra }, statusCode: status);
}
