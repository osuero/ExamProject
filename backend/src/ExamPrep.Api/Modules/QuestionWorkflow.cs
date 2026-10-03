using ExamPrep.Api.Data;
using ExamPrep.Api.DomainLogic;
using ExamPrep.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExamPrep.Api.Modules;

public record WorkflowResult(bool Ok, int Status, string? Code, string? Message, object? Details = null);

/// <summary>Single place for lifecycle gates, used by the admin API and by review-record application.</summary>
public class QuestionWorkflow(AppDbContext db, Audit audit, TimeProvider clock)
{
    public async Task<WorkflowResult> TransitionLatestAsync(Question q, string to, string? note, Guid? actor, CancellationToken ct)
    {
        var v = q.Versions.OrderByDescending(x => x.VersionNo).First();
        if (!QuestionStatuses.All.Contains(to)) return new(false, 400, "status_unknown", "Unknown status.");
        if (!Lifecycle.CanTransition(v.Status, to)) return new(false, 409, "transition_not_allowed", $"Cannot move from {v.Status} to {to}.");
        if (to is QuestionStatuses.Quarantined or QuestionStatuses.Retired && string.IsNullOrWhiteSpace(note))
            return new(false, 400, "note_required", "Explain why the question is quarantined or retired.");

        var cert = await db.Certifications.Include(c => c.Domains).Include(c => c.Languages).SingleAsync(c => c.Id == q.CertificationId, ct);
        var sources = await db.Sources.ToDictionaryAsync(s => s.Id, ct);
        var scenarios = (await db.Scenarios.Where(s => s.CertificationId == cert.Id).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
        var issues = QuestionValidator.Validate(v, cert.Domains.Select(d => d.Code).ToHashSet(), sources.Keys.ToHashSet(), scenarios);
        var advancing = to is QuestionStatuses.TechnicalReview or QuestionStatuses.EditorialReview or QuestionStatuses.Approved or QuestionStatuses.Published;
        if (advancing && issues.Any(i => i.Severity == "error"))
            return new(false, 409, "validation_failed", "Fix validation errors before advancing.", issues);
        if (to is QuestionStatuses.Approved or QuestionStatuses.Published)
        {
            var missing = v.SourceIds.Where(s => !sources.ContainsKey(s) || sources[s].Status != "reachable").ToList();
            if (missing.Count > 0) return new(false, 409, "sources_unverified", "Every cited source must be registered and marked reachable before approval.", missing);
        }
        if (to == QuestionStatuses.Published && cert.Languages.All(l => l.Locale != v.Locale))
            return new(false, 409, "language_not_verified",
                $"Locale '{v.Locale}' is not verified for {cert.Code}. Keep the question in administrative preview until the exam language is verified with evidence.");

        var from = v.Status;
        v.Status = to; v.StatusNote = note; v.UpdatedAt = clock.GetUtcNow();
        if (to == QuestionStatuses.Published)
            foreach (var old in q.Versions.Where(o => o.Id != v.Id && o.Status != QuestionStatuses.Retired))
            { old.Status = QuestionStatuses.Retired; old.StatusNote = $"Superseded by version {v.VersionNo}"; old.UpdatedAt = clock.GetUtcNow(); }
        audit.Record(actor, "question.transition", "question_version", v.Id.ToString(), new { q.ExternalId, from, to, note });
        await db.SaveChangesAsync(ct);
        return new(true, 200, null, null);
    }
}

public class ReviewRecord
{
    public string ReviewId { get; set; } = "";
    public string Method { get; set; } = "";
    public string TargetStatus { get; set; } = QuestionStatuses.Published;
    public List<ReviewItem> Items { get; set; } = new();
}

public class ReviewItem
{
    public string ExternalId { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public bool BlindAnswerMatchesKey { get; set; }
    public string Verdict { get; set; } = "";
    public string Evidence { get; set; } = "";
}

/// <summary>Applies a recorded independent review. A review only applies to the exact content it covered (content hash).</summary>
public class ReviewApplier(AppDbContext db, QuestionWorkflow wf, ILogger<ReviewApplier> log)
{
    private static readonly string[] Path = { QuestionStatuses.TechnicalReview, QuestionStatuses.EditorialReview, QuestionStatuses.Approved, QuestionStatuses.Published };

    public async Task<(int applied, int skipped, List<string> problems)> ApplyAsync(ReviewRecord r, Guid? actor, CancellationToken ct)
    {
        int applied = 0, skipped = 0; var problems = new List<string>();
        var targetIdx = Array.IndexOf(Path, r.TargetStatus);
        if (targetIdx < 0) { problems.Add($"Unsupported target status {r.TargetStatus}."); return (0, 0, problems); }
        foreach (var it in r.Items)
        {
            var q = await db.Questions.Include(x => x.Versions).SingleOrDefaultAsync(x => x.ExternalId == it.ExternalId, ct);
            if (q is null) { problems.Add($"{it.ExternalId}: not found"); continue; }
            var latest = q.Versions.OrderByDescending(v => v.VersionNo).First();
            if (latest.ContentHash != it.ContentHash) { problems.Add($"{it.ExternalId}: reviewed content differs from latest version {latest.VersionNo}; review not applied"); continue; }
            if (it.Verdict != "pass" || !it.BlindAnswerMatchesKey) { problems.Add($"{it.ExternalId}: review verdict does not allow advancing"); continue; }
            var cur = Array.IndexOf(Path, latest.Status);
            if (cur >= targetIdx) { skipped++; continue; }
            var ok = true;
            for (var i = cur + 1; i <= targetIdx && ok; i++)
            {
                var res = await wf.TransitionLatestAsync(q, Path[i], $"{r.ReviewId}: {it.Evidence}", actor, ct);
                if (!res.Ok) { problems.Add($"{it.ExternalId}: {res.Code} {res.Message}"); ok = false; }
            }
            if (ok) applied++;
        }
        if (problems.Count > 0) log.LogWarning("Review {Review}: {Count} items not advanced: {Problems}", r.ReviewId, problems.Count, string.Join(" | ", problems.Take(10)));
        return (applied, skipped, problems);
    }
}
