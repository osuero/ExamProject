using System.Security.Claims;
using ExamPrep.Api.Data;
using ExamPrep.Api.DomainLogic;
using ExamPrep.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExamPrep.Api.Modules;

public record TransitionDto(string To, string? Note);
public record EditQuestionDto(string DomainCode, string Objective, string? ScenarioId, string QuestionType, int SelectCount, string Stem,
    List<QuestionOption> Options, List<string> CorrectOptionIds, string Explanation, string Difficulty, List<string> SourceIds, string? Note);
public record LanguageDto(string Locale, string SourceId, string Evidence);
public record ProfileDto(int QuestionCount, int ExamDurationMinutes, int? AppointmentDurationMinutes, List<string> AllowedQuestionTypes,
    List<DomainWeight> DomainWeights, decimal SimulatorPassPercent, string VerificationStatus, List<string> SourceIds, string? BlueprintVersion, string? Notes);

public static class AdminEndpoints
{
    public static object VersionView(QuestionVersion v, Question q) => new
    {
        questionId = q.Id, externalId = q.ExternalId, familyId = q.FamilyId, versionId = v.Id, version = v.VersionNo, status = v.Status, statusNote = v.StatusNote,
        v.DomainCode, v.Objective, v.Locale, v.ScenarioId, v.QuestionType, v.SelectCount, v.Stem, v.Options, v.CorrectOptionIds, v.Explanation,
        v.Points, v.Difficulty, v.DifficultyBasis, v.SourceIds, v.SourceCheckedAt, v.Tags, v.Originality, v.ContentHash, v.Provenance,
        v.CreatedAt, v.UpdatedAt, nextStates = Lifecycle.NextStates(v.Status)
    };

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("admin");

        g.MapGet("/questions", async (string certificationCode, string? status, string? domain, AppDbContext db, CancellationToken ct) =>
        {
            var cert = await db.Certifications.SingleOrDefaultAsync(c => c.Code == certificationCode, ct);
            if (cert is null) return Results.NotFound();
            var qs = await db.Questions.AsNoTracking().Include(q => q.Versions).Where(q => q.CertificationId == cert.Id).OrderBy(q => q.ExternalId).ToListAsync(ct);
            var rows = qs.Select(q => (q, v: q.Versions.OrderByDescending(v => v.VersionNo).First()))
                .Where(x => status is null || x.v.Status == status)
                .Where(x => domain is null || x.v.DomainCode == domain)
                .Select(x => new
                {
                    questionId = x.q.Id, externalId = x.q.ExternalId, version = x.v.VersionNo, status = x.v.Status, domainCode = x.v.DomainCode,
                    objective = x.v.Objective, questionType = x.v.QuestionType, locale = x.v.Locale, stem = x.v.Stem, scenarioId = x.v.ScenarioId,
                    versions = x.q.Versions.Count
                });
            return Results.Ok(rows);
        });

        g.MapGet("/questions/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var q = await db.Questions.AsNoTracking().Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (q is null) return Results.NotFound();
            var cert = await db.Certifications.Include(c => c.Domains).SingleAsync(c => c.Id == q.CertificationId, ct);
            var sources = (await db.Sources.Select(s => s.Id).ToListAsync(ct)).ToHashSet();
            var scenarios = (await db.Scenarios.Where(s => s.CertificationId == cert.Id).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
            var latest = q.Versions.OrderByDescending(v => v.VersionNo).First();
            var scenario = latest.ScenarioId is null ? null : await db.Scenarios.FindAsync(new object[] { latest.ScenarioId }, ct);
            return Results.Ok(new
            {
                certificationCode = cert.Code,
                latest = VersionView(latest, q),
                scenario,
                validation = QuestionValidator.Validate(latest, cert.Domains.Select(d => d.Code).ToHashSet(), sources, scenarios),
                history = q.Versions.OrderByDescending(v => v.VersionNo).Select(v => new { v.Id, v.VersionNo, v.Status, v.ContentHash, v.CreatedAt, v.StatusNote })
            });
        });

        g.MapPost("/questions/{id:guid}/transition", async (Guid id, TransitionDto dto, ClaimsPrincipal p, AppDbContext db, Audit audit, TimeProvider clock, CancellationToken ct) =>
        {
            var q = await db.Questions.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (q is null) return Results.NotFound();
            var v = q.Versions.OrderByDescending(x => x.VersionNo).First();
            if (!QuestionStatuses.All.Contains(dto.To)) return Problems.Error(400, "status_unknown", "Unknown status.");
            if (!Lifecycle.CanTransition(v.Status, dto.To)) return Problems.Error(409, "transition_not_allowed", $"Cannot move from {v.Status} to {dto.To}.");
            if (dto.To is QuestionStatuses.Quarantined or QuestionStatuses.Retired && string.IsNullOrWhiteSpace(dto.Note))
                return Problems.Error(400, "note_required", "Explain why the question is quarantined or retired.");

            var cert = await db.Certifications.Include(c => c.Domains).Include(c => c.Languages).SingleAsync(c => c.Id == q.CertificationId, ct);
            var sources = await db.Sources.ToDictionaryAsync(s => s.Id, ct);
            var scenarios = (await db.Scenarios.Where(s => s.CertificationId == cert.Id).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
            var issues = QuestionValidator.Validate(v, cert.Domains.Select(d => d.Code).ToHashSet(), sources.Keys.ToHashSet(), scenarios);
            var advancing = dto.To is QuestionStatuses.TechnicalReview or QuestionStatuses.EditorialReview or QuestionStatuses.Approved or QuestionStatuses.Published;
            if (advancing && issues.Any(i => i.Severity == "error"))
                return Problems.Error(409, "validation_failed", "Fix validation errors before advancing.", issues);
            if (dto.To is QuestionStatuses.Approved or QuestionStatuses.Published)
            {
                var missing = v.SourceIds.Where(s => !sources.ContainsKey(s) || sources[s].Status != "reachable").ToList();
                if (missing.Count > 0) return Problems.Error(409, "sources_unverified", "Every cited source must be registered and marked reachable before approval.", missing);
            }
            if (dto.To == QuestionStatuses.Published && cert.Languages.All(l => l.Locale != v.Locale))
                return Problems.Error(409, "language_not_verified",
                    $"Locale '{v.Locale}' is not verified for {cert.Code}. Keep the question in administrative preview until the exam language is verified with evidence.");

            var from = v.Status;
            v.Status = dto.To; v.StatusNote = dto.Note; v.UpdatedAt = clock.GetUtcNow();
            if (dto.To == QuestionStatuses.Published)
                foreach (var old in q.Versions.Where(o => o.Id != v.Id && o.Status == QuestionStatuses.Published))
                { old.Status = QuestionStatuses.Retired; old.StatusNote = $"Superseded by version {v.VersionNo}"; }
            audit.Record(p.UserId(), "question.transition", "question_version", v.Id.ToString(), new { q.ExternalId, from, to = dto.To, dto.Note });
            await db.SaveChangesAsync(ct);
            return Results.Ok(VersionView(v, q));
        });

        g.MapPost("/questions/{id:guid}/versions", async (Guid id, EditQuestionDto dto, ClaimsPrincipal p, AppDbContext db, Audit audit, TimeProvider clock, CancellationToken ct) =>
        {
            var q = await db.Questions.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (q is null) return Results.NotFound();
            var prev = q.Versions.OrderByDescending(x => x.VersionNo).First();
            var now = clock.GetUtcNow();
            var v = new QuestionVersion
            {
                QuestionId = q.Id, VersionNo = prev.VersionNo + 1, DomainCode = dto.DomainCode, Objective = dto.Objective, Locale = prev.Locale,
                ScenarioId = string.IsNullOrWhiteSpace(dto.ScenarioId) ? null : dto.ScenarioId, QuestionType = dto.QuestionType, SelectCount = dto.SelectCount,
                Stem = dto.Stem.Trim(), Options = dto.Options, CorrectOptionIds = dto.CorrectOptionIds, Explanation = dto.Explanation.Trim(),
                Points = prev.Points, Difficulty = dto.Difficulty, DifficultyBasis = prev.DifficultyBasis, SourceIds = dto.SourceIds,
                SourceCheckedAt = prev.SourceCheckedAt, Tags = prev.Tags, Originality = prev.Originality, Status = QuestionStatuses.Draft,
                StatusNote = dto.Note, Provenance = $"admin edit of v{prev.VersionNo}", CreatedAt = now, UpdatedAt = now
            };
            v.ContentHash = ContentImporter.ContentHash(v);
            if (v.ContentHash == prev.ContentHash) return Problems.Error(409, "no_changes", "The content is identical to the latest version.");
            var cert = await db.Certifications.Include(c => c.Domains).SingleAsync(c => c.Id == q.CertificationId, ct);
            var issues = QuestionValidator.Validate(v, cert.Domains.Select(d => d.Code).ToHashSet(), (await db.Sources.Select(s => s.Id).ToListAsync(ct)).ToHashSet());
            if (issues.Any(i => i.Severity == "error")) return Problems.Error(400, "validation_failed", "The new version has validation errors.", issues);
            db.QuestionVersions.Add(v);
            audit.Record(p.UserId(), "question.versioned", "question_version", v.Id.ToString(), new { q.ExternalId, v.VersionNo });
            await db.SaveChangesAsync(ct);
            return Results.Ok(VersionView(v, q));
        });

        g.MapGet("/coverage", async (string certificationCode, AppDbContext db, CancellationToken ct) =>
        {
            var cert = await db.Certifications.Include(c => c.Domains).Include(c => c.Profiles).SingleOrDefaultAsync(c => c.Code == certificationCode, ct);
            if (cert is null) return Results.NotFound();
            var profile = cert.Profiles.Single(x => x.IsCurrent);
            var latest = (await db.QuestionVersions.AsNoTracking().Where(v => v.Question!.CertificationId == cert.Id).ToListAsync(ct))
                .GroupBy(v => v.QuestionId).Select(g2 => g2.OrderByDescending(v => v.VersionNo).First()).ToList();
            var alloc = Allocation.LargestRemainder(profile.DomainWeights, profile.QuestionCount);
            return Results.Ok(new
            {
                certificationCode, editorialTarget = cert.Domains.Sum(d => d.BankTarget),
                totals = QuestionStatuses.All.ToDictionary(s => s, s => latest.Count(v => v.Status == s)),
                domains = cert.Domains.OrderBy(d => d.SortOrder).Select(d => new
                {
                    d.Code, d.Name, weightPercent = profile.DomainWeights.FirstOrDefault(w => w.Code == d.Code)?.WeightPercent,
                    bankTarget = d.BankTarget, simulationItems = alloc.GetValueOrDefault(d.Code),
                    byStatus = QuestionStatuses.All.ToDictionary(s => s, s => latest.Count(v => v.DomainCode == d.Code && v.Status == s)),
                    objectives = latest.Where(v => v.DomainCode == d.Code).GroupBy(v => v.Objective).Select(o => new { objective = o.Key, count = o.Count() })
                })
            });
        });

        g.MapGet("/content-stats", async (string certificationCode, AppDbContext db, CancellationToken ct) =>
        {
            var cert = await db.Certifications.SingleOrDefaultAsync(c => c.Code == certificationCode, ct);
            if (cert is null) return Results.NotFound();
            var latest = (await db.QuestionVersions.AsNoTracking().Where(v => v.Question!.CertificationId == cert.Id && v.Status != QuestionStatuses.Retired).ToListAsync(ct))
                .GroupBy(v => v.QuestionId).Select(g2 => g2.OrderByDescending(v => v.VersionNo).First()).ToList();
            var single = latest.Where(v => v.CorrectOptionIds.Count == 1).ToList();
            var longestCorrect = single.Count(v => v.Options.OrderByDescending(o => o.Text.Length).First().Id == v.CorrectOptionIds[0]);
            var expectedLongest = single.Count == 0 ? 0 : single.Sum(v => 1.0 / v.Options.Count);
            return Results.Ok(new
            {
                sampleSize = latest.Count,
                note = "Stored option letters are shuffled at attempt start. Interpret ratios with the sample size; do not change a correct answer only to balance letters.",
                keyPositions = latest.SelectMany(v => v.CorrectOptionIds).GroupBy(x => x).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Count()),
                singleChoiceItems = single.Count,
                correctIsLongest = longestCorrect,
                expectedIfRandom = Math.Round(expectedLongest, 1),
                lengthCueItems = single.Where(v => QuestionValidator.Validate(v, new HashSet<string> { v.DomainCode }, v.SourceIds.ToHashSet()).Any(i => i.Code == "length_cue"))
                    .Select(v => v.Id)
            });
        });

        g.MapGet("/sources", async (AppDbContext db, CancellationToken ct) => Results.Ok(await db.Sources.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct)));
        g.MapPut("/sources/{id}", async (string id, Source s, ClaimsPrincipal p, AppDbContext db, Audit audit, CancellationToken ct) =>
        {
            if (!Uri.TryCreate(s.Url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return Problems.Error(400, "url_invalid", "Source URL must be an absolute https URL.");
            var existing = await db.Sources.FindAsync(new object[] { id }, ct);
            s.Id = id;
            if (existing is null) db.Sources.Add(s); else db.Entry(existing).CurrentValues.SetValues(s);
            audit.Record(p.UserId(), "source.upserted", "source", id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(s);
        });

        g.MapGet("/certifications/{code}", async (string code, AppDbContext db, CancellationToken ct) =>
        {
            var c = await db.Certifications.AsNoTracking().Include(x => x.Domains).Include(x => x.Languages).Include(x => x.Profiles).AsSplitQuery().SingleOrDefaultAsync(x => x.Code == code, ct);
            return c is null ? Results.NotFound() : Results.Ok(new
            {
                c.Code, c.Title, domains = c.Domains.OrderBy(d => d.SortOrder), languages = c.Languages,
                profiles = c.Profiles.OrderByDescending(x => x.Version)
            });
        });

        g.MapPost("/certifications/{code}/languages", async (string code, LanguageDto dto, ClaimsPrincipal p, AppDbContext db, Audit audit, TimeProvider clock, CancellationToken ct) =>
        {
            var c = await db.Certifications.Include(x => x.Languages).SingleOrDefaultAsync(x => x.Code == code, ct);
            if (c is null) return Results.NotFound();
            if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Locale ?? "", "^[a-z]{2}(-[A-Z]{2})?$")) return Problems.Error(400, "locale_invalid", "Use a BCP-47 tag such as en or es-MX.");
            var src = await db.Sources.FindAsync(new object[] { dto.SourceId }, ct);
            if (src is null || src.Kind is not ("official_exam" or "provider")) return Problems.Error(400, "evidence_required", "Language verification requires a registered official exam or provider source for this specific exam.");
            if (string.IsNullOrWhiteSpace(dto.Evidence)) return Problems.Error(400, "evidence_required", "Describe the evidence (page, section, quote) for this exam's language.");
            if (c.Languages.Any(l => l.Locale == dto.Locale)) return Problems.Error(409, "already_verified", "Locale already verified.");
            db.VerifiedLanguages.Add(new VerifiedLanguage { CertificationId = c.Id, Locale = dto.Locale!, SourceId = dto.SourceId, Evidence = dto.Evidence, VerifiedByUserId = p.UserId(), VerifiedAt = clock.GetUtcNow() });
            audit.Record(p.UserId(), "language.verified", "certification", code, dto);
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        });

        g.MapDelete("/certifications/{code}/languages/{locale}", async (string code, string locale, ClaimsPrincipal p, AppDbContext db, Audit audit, CancellationToken ct) =>
        {
            var c = await db.Certifications.Include(x => x.Languages).SingleOrDefaultAsync(x => x.Code == code, ct);
            var l = c?.Languages.SingleOrDefault(x => x.Locale == locale);
            if (l is null) return Results.NotFound();
            db.VerifiedLanguages.Remove(l);
            audit.Record(p.UserId(), "language.unverified", "certification", code, new { locale });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/certifications/{code}/profiles", async (string code, ProfileDto dto, ClaimsPrincipal p, AppDbContext db, Audit audit, TimeProvider clock, CancellationToken ct) =>
        {
            var c = await db.Certifications.Include(x => x.Profiles).Include(x => x.Domains).SingleOrDefaultAsync(x => x.Code == code, ct);
            if (c is null) return Results.NotFound();
            if (dto.QuestionCount is < 1 or > 300 || dto.ExamDurationMinutes is < 1 or > 600) return Problems.Error(400, "profile_invalid", "Question count or duration out of range.");
            if (dto.DomainWeights.Select(w => w.Code).Except(c.Domains.Select(d => d.Code)).Any() || dto.DomainWeights.Count == 0 || dto.DomainWeights.Any(w => w.WeightPercent < 0))
                return Problems.Error(400, "weights_invalid", "Weights must reference this certification's domains and be non-negative.");
            if (dto.AllowedQuestionTypes.Except(new[] { QuestionTypes.Single, QuestionTypes.Multiple }).Any()) return Problems.Error(400, "types_invalid", "Unknown question type.");
            if (dto.SimulatorPassPercent is < 1 or > 100) return Problems.Error(400, "threshold_invalid", "Threshold must be between 1 and 100.");
            var strategy = db.Database.CreateExecutionStrategy();
            ExamProfile? created = null;
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var current = c.Profiles.Single(x => x.IsCurrent);
                current.IsCurrent = false;
                await db.SaveChangesAsync(ct);
                created = new ExamProfile
                {
                    CertificationId = c.Id, Version = c.Profiles.Max(x => x.Version) + 1, IsCurrent = true, QuestionCount = dto.QuestionCount,
                    ExamDurationMinutes = dto.ExamDurationMinutes, AppointmentDurationMinutes = dto.AppointmentDurationMinutes,
                    AllowedQuestionTypes = dto.AllowedQuestionTypes, DomainWeights = dto.DomainWeights, SimulatorPassPercent = dto.SimulatorPassPercent,
                    ScoringPolicy = current.ScoringPolicy, OfficialScoreReference = current.OfficialScoreReference,
                    VerificationStatus = dto.VerificationStatus, SourceIds = dto.SourceIds, BlueprintVersion = dto.BlueprintVersion, Notes = dto.Notes,
                    CreatedAt = clock.GetUtcNow()
                };
                db.ExamProfiles.Add(created);
                audit.Record(p.UserId(), "profile.versioned", "certification", code, new { created.Version });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
            return Results.Ok(created);
        });

        g.MapPost("/imports/preview", async (IFormFile file, ContentImporter imp, CancellationToken ct) =>
        {
            var bytes = await ReadLimited(file, ct);
            if (bytes is null) return Problems.Error(413, "file_too_large", "File exceeds the 2 MB limit.");
            return Results.Ok(await imp.PreviewAsync(file.FileName, bytes, ct));
        }).DisableAntiforgery();

        g.MapPost("/imports/commit", async (IFormFile file, ClaimsPrincipal p, ContentImporter imp, CancellationToken ct) =>
        {
            var bytes = await ReadLimited(file, ct);
            if (bytes is null) return Problems.Error(413, "file_too_large", "File exceeds the 2 MB limit.");
            var (report, batch) = await imp.CommitAsync(file.FileName, bytes, p.UserId(), $"admin upload by {p.UserId()}", ct);
            return report.Valid ? Results.Ok(new { batchId = batch, report }) : Results.UnprocessableEntity(new { error = "import_invalid", report });
        }).DisableAntiforgery();

        g.MapGet("/imports", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.ImportBatches.AsNoTracking().OrderByDescending(b => b.CreatedAt).Take(100)
                .Select(b => new { b.Id, b.FileName, b.Format, b.Sha256, b.CertificationCode, b.Created, b.NewVersions, b.Unchanged, b.CreatedAt }).ToListAsync(ct)));

        g.MapGet("/audit", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AuditEntries.AsNoTracking().OrderByDescending(a => a.Id).Take(200).ToListAsync(ct)));

        g.MapGet("/users", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Users.AsNoTracking().OrderBy(u => u.CreatedAt).Select(u => new { u.Id, u.Email, u.Role, u.CreatedAt, u.LastLoginAt }).ToListAsync(ct)));
    }

    private static async Task<byte[]?> ReadLimited(IFormFile file, CancellationToken ct)
    {
        if (file.Length > ContentImporter.MaxBytes) return null;
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return ms.Length > ContentImporter.MaxBytes ? null : ms.ToArray();
    }
}
