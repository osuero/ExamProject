using System.Text.Json;
using ExamPrep.Api.Data;
using ExamPrep.Api.DomainLogic;
using ExamPrep.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExamPrep.Api.Modules;

public record StartAttemptDto(string CertificationCode, string Mode, bool? Feedback, int? QuestionCount, int? DurationMinutes,
    List<string>? DomainCodes, string? Locale, bool? Preview);

public record AnswerDto(List<string>? Selected);
public record FlagDto(bool Flagged);
public record CheckDto(bool? RevealWithoutAnswer);
public record FeedbackToggleDto(bool Enabled);

public class AttemptError(int status, string code, string message, object? details = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public object? Details { get; } = details;
}

public class AttemptService(AppDbContext db, TimeProvider clock, Audit audit)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const int MaxCustomQuestions = 200;
    public const int MaxDurationMinutes = 600;

    private static readonly string[] PreviewStatuses =
    {
        QuestionStatuses.Draft, QuestionStatuses.TechnicalReview, QuestionStatuses.EditorialReview,
        QuestionStatuses.Approved, QuestionStatuses.Published
    };

    public async Task<Attempt> StartAsync(Guid userId, bool isAdmin, StartAttemptDto dto, int? seed, CancellationToken ct)
    {
        if (!AttemptModes.All.Contains(dto.Mode)) throw new AttemptError(400, "invalid_mode", "Mode must be practice, simulation or custom.");
        var preview = dto.Preview == true;
        if (preview && !isAdmin) throw new AttemptError(403, "preview_forbidden", "Only administrators can start preview attempts with unpublished content.");

        var cert = await db.Certifications.Include(c => c.Domains).Include(c => c.Languages).Include(c => c.Profiles).AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Code == dto.CertificationCode, ct)
            ?? throw new AttemptError(404, "certification_not_found", "Certification not found.");
        var profile = cert.Profiles.Single(p => p.IsCurrent);

        var verified = cert.Languages.Select(l => l.Locale).ToList();
        string locale;
        if (preview) locale = dto.Locale ?? "en";
        else
        {
            if (verified.Count == 0)
                throw new AttemptError(409, "language_pending_verification",
                    "No exam language has been verified for this certification yet, so no content is published. Content stays in administrative preview.");
            locale = dto.Locale ?? verified[0];
            if (!verified.Contains(locale)) throw new AttemptError(400, "locale_not_verified", $"Locale '{locale}' is not verified for this exam.");
        }

        int count; int? duration; bool feedback; List<DomainWeight> weights;
        var selectedDomains = dto.DomainCodes is { Count: > 0 } ? dto.DomainCodes.Distinct().ToList() : null;
        if (selectedDomains is not null && selectedDomains.Any(d => cert.Domains.All(x => x.Code != d)))
            throw new AttemptError(400, "domain_unknown", "Unknown domain code.");

        switch (dto.Mode)
        {
            case AttemptModes.Simulation:
                count = profile.QuestionCount; duration = profile.ExamDurationMinutes; feedback = false;
                weights = profile.DomainWeights.ToList();
                break;
            default:
                count = dto.QuestionCount ?? (dto.Mode == AttemptModes.Practice ? 10 : throw new AttemptError(400, "count_required", "Choose a number of questions."));
                if (count < 1 || count > MaxCustomQuestions) throw new AttemptError(400, "count_invalid", $"Number of questions must be between 1 and {MaxCustomQuestions}.");
                duration = dto.DurationMinutes;
                if (duration is not null && (duration < 1 || duration > MaxDurationMinutes)) throw new AttemptError(400, "duration_invalid", $"Duration must be between 1 and {MaxDurationMinutes} minutes.");
                feedback = dto.Feedback ?? dto.Mode == AttemptModes.Practice;
                weights = profile.DomainWeights.Where(w => selectedDomains is null || selectedDomains.Contains(w.Code)).ToList();
                break;
        }

        var statuses = preview ? PreviewStatuses : new[] { QuestionStatuses.Published };
        var pool = await LatestEligibleVersions(cert.Id, statuses, locale, ct);
        var rng = seed is null ? Random.Shared : new Random(seed.Value);
        var result = dto.Mode == AttemptModes.Simulation && profile.ScenariosPerForm is int k && k > 0
            ? ExamAssembler.AssembleWithScenarios(pool, weights, count, k, rng)
            : ExamAssembler.Assemble(pool, weights, count, rng);
        if (!result.Ok)
            throw new AttemptError(409, "insufficient_questions", result.Error!, new { required = count, available = result.Available, target = result.Target });

        var now = clock.GetUtcNow();
        var attempt = new Attempt
        {
            UserId = userId, CertificationId = cert.Id, CertificationCode = cert.Code, ProfileId = profile.Id, ProfileVersion = profile.Version,
            Mode = dto.Mode, IsPreview = preview, FeedbackEnabled = feedback, FeedbackExposed = false, StartedAsClean = !feedback,
            Locale = locale, StartedAt = now, DeadlineAt = duration is null ? null : now.AddMinutes(duration.Value),
            PassPercent = profile.SimulatorPassPercent, ScoringPolicy = profile.ScoringPolicy,
            ProfileSnapshotJson = JsonSerializer.Serialize(new
            {
                profileId = profile.Id, profile.Version, profile.QuestionCount, profile.ExamDurationMinutes, profile.DomainWeights,
                profile.SimulatorPassPercent, profile.ScoringPolicy, profile.VerificationStatus,
                attemptQuestionCount = count, attemptDurationMinutes = duration, domains = weights.Select(w => w.Code),
                allocationTarget = result.Target, allocationActual = result.Actual, preview,
                scenariosPerForm = profile.ScenariosPerForm, scenarios = result.Scenarios, scenarioNote = result.ScenarioNote
            }, Json)
        };
        var versionIds = result.Items.Select(i => i.VersionId).ToList();
        var versions = await db.QuestionVersions.Where(v => versionIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var pos = 1;
        foreach (var c in result.Items)
        {
            var v = versions[c.VersionId];
            attempt.Items.Add(new AttemptItem
            {
                Position = pos++, QuestionVersionId = v.Id, DomainCode = v.DomainCode, ScenarioId = v.ScenarioId,
                OptionOrder = v.Options.Select(o => o.Id).OrderBy(_ => rng.Next()).ToList()
            });
        }
        db.Attempts.Add(attempt);
        audit.Record(userId, "attempt.started", "attempt", attempt.Id.ToString(), new { attempt.Mode, attempt.CertificationCode, count, preview });
        await db.SaveChangesAsync(ct);
        return attempt;
    }

    public async Task<List<Candidate>> LatestEligibleVersions(Guid certId, string[] statuses, string locale, CancellationToken ct)
    {
        // Per question, take the newest version that is in an eligible state.
        var rows = await db.QuestionVersions
            .Where(v => v.Question!.CertificationId == certId && statuses.Contains(v.Status) && v.Locale == locale)
            .Select(v => new { v.Id, v.QuestionId, v.VersionNo, v.DomainCode, v.Question!.FamilyId, v.ScenarioId, v.Question.ExternalId })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.QuestionId).Select(g => g.OrderByDescending(r => r.VersionNo).First())
            .Select(r => new Candidate(r.Id, r.DomainCode, r.FamilyId, r.ScenarioId, r.ExternalId)).ToList();
    }

    /// <summary>Loads an attempt owned by the user under a row lock (inside the caller's transaction).</summary>
    private async Task<Attempt> LoadLockedAsync(Guid attemptId, Guid userId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM attempts WHERE \"Id\" = {attemptId} FOR UPDATE", ct);
        var a = await db.Attempts.Include(x => x.Items).ThenInclude(i => i.QuestionVersion)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId, ct);
        return a ?? throw new AttemptError(404, "attempt_not_found", "Attempt not found.");
    }

    private bool IsPastDeadline(Attempt a) => a.DeadlineAt is not null && clock.GetUtcNow() >= a.DeadlineAt;

    private async Task<T> InLockedTx<T>(Guid attemptId, Guid userId, Func<Attempt, Task<T>> body, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var a = await LoadLockedAsync(attemptId, userId, ct);
            if (a.Status == AttemptStatuses.InProgress && IsPastDeadline(a))
            {
                FinalizeInPlace(a, AttemptStatuses.Expired);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                throw new AttemptError(409, "attempt_expired", "Time is over. Answers saved before the deadline were scored.");
            }
            var r = await body(a);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return r;
        });
    }

    private static void RequireInProgress(Attempt a)
    {
        if (a.Status != AttemptStatuses.InProgress) throw new AttemptError(409, "attempt_closed", "This attempt is already finished.");
    }

    private static AttemptItem ItemAt(Attempt a, int position) =>
        a.Items.SingleOrDefault(i => i.Position == position) ?? throw new AttemptError(404, "item_not_found", "Question not found in this attempt.");

    public Task<AttemptItem> SaveAnswerAsync(Guid attemptId, Guid userId, int position, List<string>? selected, CancellationToken ct) =>
        InLockedTx(attemptId, userId, a =>
        {
            RequireInProgress(a);
            var item = ItemAt(a, position);
            var v = item.QuestionVersion!;
            var sel = (selected ?? new()).Distinct(StringComparer.Ordinal).ToList();
            if (sel.Any(s => v.Options.All(o => o.Id != s))) throw new AttemptError(400, "option_unknown", "Unknown option.");
            if (sel.Count > v.SelectCount) throw new AttemptError(400, "too_many_selections", $"Select at most {v.SelectCount} options.");
            if (item.FeedbackRevealed) item.LearningOptionIds = sel; // learning correction, never scored
            else { item.SelectedOptionIds = sel; item.AnsweredAt = clock.GetUtcNow(); }
            return Task.FromResult(item);
        }, ct);

    public Task<AttemptItem> SetFlagAsync(Guid attemptId, Guid userId, int position, bool flagged, CancellationToken ct) =>
        InLockedTx(attemptId, userId, a =>
        {
            RequireInProgress(a);
            var item = ItemAt(a, position);
            item.Flagged = flagged;
            return Task.FromResult(item);
        }, ct);

    public Task<AttemptItem> CheckAsync(Guid attemptId, Guid userId, int position, bool revealWithoutAnswer, CancellationToken ct) =>
        InLockedTx(attemptId, userId, a =>
        {
            RequireInProgress(a);
            if (!a.FeedbackEnabled) throw new AttemptError(409, "feedback_disabled", "Feedback is hidden for this attempt.");
            var item = ItemAt(a, position);
            if (item.FeedbackRevealed) return Task.FromResult(item); // idempotent
            var v = item.QuestionVersion!;
            if (item.SelectedOptionIds.Count == 0 && !revealWithoutAnswer)
                throw new AttemptError(400, "no_answer", "Select an answer before checking, or choose to reveal the solution.");
            if (item.SelectedOptionIds.Count > 0 && item.SelectedOptionIds.Count != v.SelectCount)
                throw new AttemptError(400, "incomplete_selection", $"Select exactly {v.SelectCount} options before checking.");
            item.FeedbackRevealed = true;
            item.RevealedAt = clock.GetUtcNow();
            item.ScoredOptionIds = item.SelectedOptionIds.ToList(); // initial answer is frozen for scoring
            item.IsCorrect = Scoring.IsExactMatch(item.ScoredOptionIds, v.CorrectOptionIds);
            a.FeedbackExposed = true;
            return Task.FromResult(item);
        }, ct);

    public Task<Attempt> SetFeedbackAsync(Guid attemptId, Guid userId, bool enabled, CancellationToken ct) =>
        InLockedTx(attemptId, userId, a =>
        {
            RequireInProgress(a);
            if (enabled && !a.FeedbackEnabled)
            {
                a.FeedbackEnabled = true;
                a.FeedbackExposed = true; // irreversible reclassification as assisted
                audit.Record(userId, "attempt.feedback_enabled", "attempt", a.Id.ToString());
            }
            else if (!enabled) a.FeedbackEnabled = false; // exposure is not erased
            return Task.FromResult(a);
        }, ct);

    public async Task<Attempt> FinishAsync(Guid attemptId, Guid userId, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var a = await LoadLockedAsync(attemptId, userId, ct);
            if (a.Status == AttemptStatuses.InProgress)
            {
                FinalizeInPlace(a, IsPastDeadline(a) ? AttemptStatuses.Expired : AttemptStatuses.Submitted);
                audit.Record(userId, "attempt.finished", "attempt", a.Id.ToString(), new { a.Status });
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return a;
        });
    }

    /// <summary>Closes overdue attempts even when no browser is connected. Safe to run concurrently.</summary>
    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.Attempts.Where(a => a.Status == AttemptStatuses.InProgress && a.DeadlineAt != null && a.DeadlineAt <= now)
            .Select(a => new { a.Id, a.UserId }).Take(200).ToListAsync(ct);
        var n = 0;
        foreach (var d in due)
        {
            db.ChangeTracker.Clear();
            var r = await FinishAsync(d.Id, d.UserId, ct);
            if (r.Status == AttemptStatuses.Expired) n++;
        }
        return n;
    }

    private void FinalizeInPlace(Attempt a, string status)
    {
        var now = clock.GetUtcNow();
        foreach (var i in a.Items)
        {
            i.ScoredOptionIds ??= i.SelectedOptionIds.ToList();
            i.IsCorrect = i.ScoredOptionIds.Count == 0 ? null : Scoring.IsExactMatch(i.ScoredOptionIds, i.QuestionVersion!.CorrectOptionIds);
        }
        var totals = Scoring.Compute(a.Items.Select(i => ((IReadOnlyCollection<string>?)i.ScoredOptionIds, (IReadOnlyCollection<string>)i.QuestionVersion!.CorrectOptionIds, i.QuestionVersion.Points)), a.PassPercent);
        a.PointsEarned = totals.Earned; a.PointsMax = totals.Max; a.CorrectCount = totals.Correct; a.IncorrectCount = totals.Incorrect;
        a.OmittedCount = totals.Omitted; a.Percent = totals.Percent; a.Passed = totals.Passed;
        var end = a.DeadlineAt is not null && now > a.DeadlineAt ? a.DeadlineAt.Value : now;
        a.TimeUsedSeconds = (int)Math.Max(0, (end - a.StartedAt).TotalSeconds);
        a.FinishedAt = now;
        a.Status = status;
    }

    public async Task<Attempt> LoadForReadAsync(Guid attemptId, Guid userId, CancellationToken ct)
    {
        var a = await db.Attempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId, ct)
                ?? throw new AttemptError(404, "attempt_not_found", "Attempt not found.");
        if (a.Status == AttemptStatuses.InProgress && IsPastDeadline(a))
            await FinishAsync(attemptId, userId, ct);
        db.ChangeTracker.Clear();
        return await db.Attempts.AsNoTracking().Include(x => x.Items.OrderBy(i => i.Position)).ThenInclude(i => i.QuestionVersion).ThenInclude(v => v!.Question)
            .AsSplitQuery().SingleAsync(x => x.Id == attemptId, ct);
    }
}
