using System.Text.Json;
using ExamPrep.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamPrep.Api.Modules;

public class CatalogFile
{
    public List<CatalogCert> Certifications { get; set; } = new();
    public List<CatalogLanguage> Languages { get; set; } = new();
}

public class CatalogLanguage
{
    public string CertificationCode { get; set; } = "";
    public string Locale { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Evidence { get; set; } = "";
}

public class CatalogCert
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Level { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string Description { get; set; } = "";
    public string Disclaimer { get; set; } = "";
    public int SortOrder { get; set; }
    public List<CatalogDomain> Domains { get; set; } = new();
    public CatalogProfile Profile { get; set; } = new();
}

public class CatalogDomain
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal WeightPercent { get; set; }
    public int BankTarget { get; set; }
    public List<string> Objectives { get; set; } = new();
}

public class CatalogProfile
{
    public int QuestionCount { get; set; }
    public int ExamDurationMinutes { get; set; }
    public int? AppointmentDurationMinutes { get; set; }
    public int? ScenariosPerForm { get; set; }
    public List<string> AllowedQuestionTypes { get; set; } = new();
    public decimal SimulatorPassPercent { get; set; } = 80;
    public string? OfficialScoreReference { get; set; }
    public string VerificationStatus { get; set; } = "provisional_secondary_source";
    public List<string> SourceIds { get; set; } = new();
    public string? BlueprintVersion { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Idempotent bootstrap of catalog, sources and seed banks from the backend-only content directory.</summary>
public class Bootstrapper(AppDbContext db, ContentImporter importer, ReviewApplier reviews, TimeProvider clock, ILogger<Bootstrapper> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>When catalog.json changes a profile, record it as a new profile version; attempts keep the version they used.</summary>
    private async Task VersionProfileIfChanged(Certification cert, CatalogCert c, CancellationToken ct)
    {
        var cur = cert.Profiles.Single(p => p.IsCurrent);
        var weights = c.Domains.Select(d => new DomainWeight { Code = d.Code, WeightPercent = d.WeightPercent }).ToList();
        var same = cur.QuestionCount == c.Profile.QuestionCount && cur.ExamDurationMinutes == c.Profile.ExamDurationMinutes
            && cur.AppointmentDurationMinutes == c.Profile.AppointmentDurationMinutes && cur.ScenariosPerForm == c.Profile.ScenariosPerForm
            && cur.SimulatorPassPercent == c.Profile.SimulatorPassPercent && cur.VerificationStatus == c.Profile.VerificationStatus
            && cur.DomainWeights.Count == weights.Count && cur.DomainWeights.Zip(weights).All(z => z.First.Code == z.Second.Code && z.First.WeightPercent == z.Second.WeightPercent);
        if (same) return;
        cur.IsCurrent = false;
        await db.SaveChangesAsync(ct);
        db.ExamProfiles.Add(new ExamProfile
        {
            CertificationId = cert.Id, Version = cert.Profiles.Max(p => p.Version) + 1, IsCurrent = true, QuestionCount = c.Profile.QuestionCount,
            ExamDurationMinutes = c.Profile.ExamDurationMinutes, AppointmentDurationMinutes = c.Profile.AppointmentDurationMinutes,
            ScenariosPerForm = c.Profile.ScenariosPerForm, AllowedQuestionTypes = c.Profile.AllowedQuestionTypes, DomainWeights = weights,
            SimulatorPassPercent = c.Profile.SimulatorPassPercent, ScoringPolicy = cur.ScoringPolicy, OfficialScoreReference = c.Profile.OfficialScoreReference,
            VerificationStatus = c.Profile.VerificationStatus, SourceIds = c.Profile.SourceIds, BlueprintVersion = c.Profile.BlueprintVersion,
            Notes = c.Profile.Notes, CreatedAt = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);
        log.LogInformation("Profile for {Code} versioned from catalog.json", c.Code);
    }

    public async Task RunAsync(string contentDir, CancellationToken ct)
    {
        var catalogPath = Path.Combine(contentDir, "catalog.json");
        var sourcesPath = Path.Combine(contentDir, "sources.json");
        if (File.Exists(sourcesPath))
        {
            var sources = JsonSerializer.Deserialize<List<Source>>(await File.ReadAllTextAsync(sourcesPath, ct), Json)!;
            var existing = (await db.Sources.Select(s => s.Id).ToListAsync(ct)).ToHashSet();
            foreach (var s in sources.Where(s => !existing.Contains(s.Id))) db.Sources.Add(s);
            await db.SaveChangesAsync(ct);
        }
        if (File.Exists(catalogPath))
        {
            var catalog = JsonSerializer.Deserialize<CatalogFile>(await File.ReadAllTextAsync(catalogPath, ct), Json)!;
            foreach (var c in catalog.Certifications)
            {
                var existingCert = await db.Certifications.Include(x => x.Profiles).SingleOrDefaultAsync(x => x.Code == c.Code, ct);
                if (existingCert is not null)
                {
                    await VersionProfileIfChanged(existingCert, c, ct); // never edits a profile in place
                    continue;
                }
                var cert = new Certification
                {
                    Code = c.Code, Title = c.Title, Level = c.Level, ShortDescription = c.ShortDescription, Description = c.Description,
                    Disclaimer = c.Disclaimer, SortOrder = c.SortOrder
                };
                var order = 0;
                foreach (var d in c.Domains)
                    cert.Domains.Add(new Domain { Code = d.Code, Name = d.Name, BankTarget = d.BankTarget, Objectives = d.Objectives, SortOrder = order++ });
                cert.Profiles.Add(new ExamProfile
                {
                    Version = 1, IsCurrent = true, QuestionCount = c.Profile.QuestionCount, ExamDurationMinutes = c.Profile.ExamDurationMinutes,
                    AppointmentDurationMinutes = c.Profile.AppointmentDurationMinutes, ScenariosPerForm = c.Profile.ScenariosPerForm, AllowedQuestionTypes = c.Profile.AllowedQuestionTypes,
                    DomainWeights = c.Domains.Select(d => new DomainWeight { Code = d.Code, WeightPercent = d.WeightPercent }).ToList(),
                    SimulatorPassPercent = c.Profile.SimulatorPassPercent, OfficialScoreReference = c.Profile.OfficialScoreReference,
                    VerificationStatus = c.Profile.VerificationStatus, SourceIds = c.Profile.SourceIds, BlueprintVersion = c.Profile.BlueprintVersion,
                    Notes = c.Profile.Notes, CreatedAt = clock.GetUtcNow()
                });
                db.Certifications.Add(cert);
                await db.SaveChangesAsync(ct);
                log.LogInformation("Bootstrapped certification {Code}", c.Code);
            }
            foreach (var l in catalog.Languages)
            {
                var cert = await db.Certifications.Include(x => x.Languages).SingleOrDefaultAsync(x => x.Code == l.CertificationCode, ct);
                var src = await db.Sources.FindAsync(new object[] { l.SourceId }, ct);
                if (cert is null || cert.Languages.Any(x => x.Locale == l.Locale)) continue;
                if (src is null || src.Kind is not ("official_exam" or "provider") || src.Status != "reachable" || string.IsNullOrWhiteSpace(l.Evidence))
                {
                    log.LogWarning("Language {Locale} for {Code} not verified: missing official evidence", l.Locale, l.CertificationCode);
                    continue;
                }
                db.VerifiedLanguages.Add(new VerifiedLanguage
                {
                    CertificationId = cert.Id, Locale = l.Locale, SourceId = l.SourceId, Evidence = l.Evidence,
                    VerifiedByUserId = Guid.Empty, VerifiedAt = clock.GetUtcNow()
                });
                await db.SaveChangesAsync(ct);
            }
        }
        var banks = Path.Combine(contentDir, "banks");
        if (Directory.Exists(banks))
        {
            foreach (var f in Directory.GetFiles(banks).Where(f => f.EndsWith(".json") || f.EndsWith(".md")).OrderBy(f => f))
            {
                var bytes = await File.ReadAllBytesAsync(f, ct);
                var (report, batch) = await importer.CommitAsync(Path.GetFileName(f), bytes, null, "bootstrap from content/banks", ct);
                if (!report.Valid)
                    log.LogWarning("Bank {File} not imported: {Errors} file errors, {Rejected} rejected items. {Detail}", Path.GetFileName(f), report.FileErrors.Count, report.Rejected,
                        string.Join(" | ", report.FileErrors.Concat(report.Items.Where(i => i.Action == "reject")
                            .Select(i => i.ExternalId + ": " + string.Join("; ", i.Issues.Where(x => x.Severity == "error").Select(x => x.Code + " " + x.Message))))));
                else if (report.ToCreate + report.ToVersion > 0)
                    log.LogInformation("Bank {File}: {Created} created, {Versions} new versions", Path.GetFileName(f), report.ToCreate, report.ToVersion);
            }
        }
        var reviewsDir = Path.Combine(contentDir, "reviews");
        if (Directory.Exists(reviewsDir))
        {
            foreach (var f in Directory.GetFiles(reviewsDir, "*.json").OrderBy(f => f))
            {
                var record = JsonSerializer.Deserialize<ReviewRecord>(await File.ReadAllTextAsync(f, ct), Json)!;
                var (applied, skipped, problems) = await reviews.ApplyAsync(record, null, ct);
                if (applied > 0 || problems.Count > 0)
                    log.LogInformation("Review {Review}: {Applied} advanced, {Skipped} already at target, {Problems} not applied", record.ReviewId, applied, skipped, problems.Count);
            }
        }
    }
}

public class ExpiryWorker(IServiceScopeFactory scopes, ILogger<ExpiryWorker> log) : BackgroundService
{
    public static TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<AttemptService>().ExpireDueAsync(stoppingToken);
                if (n > 0) log.LogInformation("Expired {Count} overdue attempts", n);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { log.LogError(e, "Expiry sweep failed"); }
        }
    }
}
