using System.Security.Claims;
using ExamPrep.Api.Data;
using ExamPrep.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExamPrep.Api.Modules;

public static class AttemptEndpoints
{
    private static readonly string[] Labels = { "A", "B", "C", "D", "E", "F" };

    public static object ItemView(Attempt a, AttemptItem i, IReadOnlyDictionary<string, Scenario> scenarios, IReadOnlyDictionary<string, Source> sources)
    {
        var v = i.QuestionVersion!;
        var finished = a.Status != AttemptStatuses.InProgress;
        var reveal = finished || i.FeedbackRevealed;
        var opts = i.OptionOrder.Select((id, idx) =>
        {
            var o = v.Options.Single(x => x.Id == id);
            return reveal
                ? (object)new { id = o.Id, label = Labels[idx], text = o.Text, isCorrect = v.CorrectOptionIds.Contains(o.Id), rationale = o.Rationale }
                : new { id = o.Id, label = Labels[idx], text = o.Text };
        }).ToList();
        Scenario? sc = i.ScenarioId is not null && scenarios.TryGetValue(i.ScenarioId, out var s) ? s : null;
        return new
        {
            position = i.Position,
            domainCode = i.DomainCode,
            questionType = v.QuestionType,
            selectCount = v.SelectCount,
            scenario = sc is null ? null : new { id = sc.Id, title = sc.Title, text = sc.Text },
            stem = v.Stem,
            options = opts,
            selected = i.FeedbackRevealed && i.LearningOptionIds is not null ? i.LearningOptionIds : i.SelectedOptionIds,
            flagged = i.Flagged,
            feedbackRevealed = i.FeedbackRevealed,
            solution = reveal ? new
            {
                correctOptionIds = v.CorrectOptionIds,
                scoredOptionIds = i.ScoredOptionIds ?? i.SelectedOptionIds,
                learningOptionIds = i.LearningOptionIds,
                isCorrect = i.IsCorrect,
                explanation = v.Explanation,
                sources = v.SourceIds.Select(id => sources.TryGetValue(id, out var src)
                    ? new { id, title = src.Title, url = src.Url }
                    : new { id, title = id, url = "" })
            } : null,
            questionRef = new { externalId = v.Question?.ExternalId, version = v.VersionNo }
        };
    }

    public static object Summary(Attempt a) => new
    {
        id = a.Id, certificationCode = a.CertificationCode, mode = a.Mode, preview = a.IsPreview, status = a.Status,
        classification = a.Classification, feedbackEnabled = a.FeedbackEnabled, profileVersion = a.ProfileVersion,
        startedAt = a.StartedAt, deadlineAt = a.DeadlineAt, finishedAt = a.FinishedAt,
        questionCount = a.Items.Count > 0 ? a.Items.Count : (int?)null,
        score = a.Status == AttemptStatuses.InProgress ? null : new
        {
            pointsEarned = a.PointsEarned, pointsMax = a.PointsMax, percent = a.Percent, correct = a.CorrectCount,
            incorrect = a.IncorrectCount, omitted = a.OmittedCount, passed = a.Passed, passPercent = a.PassPercent,
            timeUsedSeconds = a.TimeUsedSeconds, scoringPolicy = a.ScoringPolicy
        }
    };

    private static async Task<(Dictionary<string, Scenario>, Dictionary<string, Source>)> Lookups(AppDbContext db, Attempt a, CancellationToken ct)
    {
        var scIds = a.Items.Where(i => i.ScenarioId != null).Select(i => i.ScenarioId!).Distinct().ToList();
        var srcIds = a.Items.SelectMany(i => i.QuestionVersion!.SourceIds).Distinct().ToList();
        var sc = await db.Scenarios.AsNoTracking().Where(s => scIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var src = await db.Sources.AsNoTracking().Where(s => srcIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        return (sc, src);
    }

    public static object State(Attempt a, Dictionary<string, Scenario> sc, Dictionary<string, Source> src, DateTimeOffset now) => new
    {
        attempt = Summary(a),
        serverNow = now,
        remainingSeconds = a.DeadlineAt is null ? (int?)null : Math.Max(0, (int)(a.DeadlineAt.Value - now).TotalSeconds),
        items = a.Items.OrderBy(i => i.Position).Select(i => ItemView(a, i, sc, src))
    };

    private static async Task<IResult> Guarded(Func<Task<IResult>> f)
    {
        try { return await f(); }
        catch (AttemptError e) { return Problems.Error(e.Status, e.Code, e.Message, e.Details); }
    }

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/attempts").RequireAuthorization();

        g.MapPost("", (StartAttemptDto dto, ClaimsPrincipal p, AttemptService svc, CancellationToken ct) => Guarded(async () =>
        {
            var a = await svc.StartAsync(p.UserId(), p.IsAdmin(), dto, null, ct);
            return Results.Created($"/api/attempts/{a.Id}", new { id = a.Id });
        }));

        g.MapGet("", async (string? certificationCode, ClaimsPrincipal p, AppDbContext db, CancellationToken ct) =>
        {
            var uid = p.UserId();
            var q = db.Attempts.AsNoTracking().Include(a => a.Items).Where(a => a.UserId == uid);
            if (!string.IsNullOrEmpty(certificationCode)) q = q.Where(a => a.CertificationCode == certificationCode);
            var list = await q.OrderByDescending(a => a.StartedAt).Take(200).ToListAsync(ct);
            return Results.Ok(list.Select(Summary));
        });

        g.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal p, AttemptService svc, AppDbContext db, TimeProvider clock, CancellationToken ct) => Guarded(async () =>
        {
            var a = await svc.LoadForReadAsync(id, p.UserId(), ct);
            var (sc, src) = await Lookups(db, a, ct);
            return Results.Ok(State(a, sc, src, clock.GetUtcNow()));
        }));

        g.MapPut("/{id:guid}/items/{position:int}/answer", (Guid id, int position, AnswerDto dto, ClaimsPrincipal p, AttemptService svc, CancellationToken ct) => Guarded(async () =>
        {
            var i = await svc.SaveAnswerAsync(id, p.UserId(), position, dto.Selected, ct);
            return Results.Ok(new { position = i.Position, selected = i.FeedbackRevealed && i.LearningOptionIds is not null ? i.LearningOptionIds : i.SelectedOptionIds, learning = i.LearningOptionIds, savedAt = i.AnsweredAt });
        }));

        g.MapPut("/{id:guid}/items/{position:int}/flag", (Guid id, int position, FlagDto dto, ClaimsPrincipal p, AttemptService svc, CancellationToken ct) => Guarded(async () =>
        {
            var i = await svc.SetFlagAsync(id, p.UserId(), position, dto.Flagged, ct);
            return Results.Ok(new { position = i.Position, flagged = i.Flagged });
        }));

        g.MapPost("/{id:guid}/items/{position:int}/check", (Guid id, int position, CheckDto? dto, ClaimsPrincipal p, AttemptService svc, AppDbContext db, CancellationToken ct) => Guarded(async () =>
        {
            await svc.CheckAsync(id, p.UserId(), position, dto?.RevealWithoutAnswer == true, ct);
            db.ChangeTracker.Clear();
            var a = await svc.LoadForReadAsync(id, p.UserId(), ct);
            var (sc, src) = await Lookups(db, a, ct);
            return Results.Ok(ItemView(a, a.Items.Single(x => x.Position == position), sc, src));
        }));

        g.MapPost("/{id:guid}/feedback", (Guid id, FeedbackToggleDto dto, ClaimsPrincipal p, AttemptService svc, CancellationToken ct) => Guarded(async () =>
        {
            var a = await svc.SetFeedbackAsync(id, p.UserId(), dto.Enabled, ct);
            return Results.Ok(new { feedbackEnabled = a.FeedbackEnabled, classification = a.Classification });
        }));

        g.MapPost("/{id:guid}/finish", (Guid id, ClaimsPrincipal p, AttemptService svc, CancellationToken ct) => Guarded(async () =>
        {
            var a = await svc.FinishAsync(id, p.UserId(), ct);
            return Results.Ok(Summary(a));
        }));

        g.MapGet("/{id:guid}/result", (Guid id, ClaimsPrincipal p, AttemptService svc, AppDbContext db, TimeProvider clock, CancellationToken ct) => Guarded(async () =>
        {
            var a = await svc.LoadForReadAsync(id, p.UserId(), ct);
            if (a.Status == AttemptStatuses.InProgress) return Problems.Error(409, "attempt_in_progress", "Finish the attempt to see the result.");
            var (sc, src) = await Lookups(db, a, ct);
            var domains = await db.Domains.AsNoTracking().Where(d => d.CertificationId == a.CertificationId).OrderBy(d => d.SortOrder).ToListAsync(ct);
            var byDomain = domains.Select(d =>
            {
                var items = a.Items.Where(i => i.DomainCode == d.Code).ToList();
                return new
                {
                    code = d.Code, name = d.Name, total = items.Count,
                    correct = items.Count(i => i.IsCorrect == true),
                    pointsEarned = items.Where(i => i.IsCorrect == true).Sum(i => i.QuestionVersion!.Points),
                    pointsMax = items.Sum(i => i.QuestionVersion!.Points)
                };
            }).Where(x => x.total > 0);
            return Results.Ok(new
            {
                attempt = Summary(a),
                disclaimer = "Practice score computed by this simulator. It is not an official Pearson VUE or Anthropic result and is not converted to the official scaled score.",
                byDomain,
                review = a.Items.OrderBy(i => i.Position).Select(i => ItemView(a, i, sc, src)),
                toReinforce = byDomain.Where(x => x.total > 0 && x.correct < x.total).OrderBy(x => (double)x.correct / x.total).Select(x => x.code)
            });
        }));

        app.MapGet("/api/progress", async (string certificationCode, ClaimsPrincipal p, AppDbContext db, CancellationToken ct) =>
        {
            var uid = p.UserId();
            var cert = await db.Certifications.Include(c => c.Domains).SingleOrDefaultAsync(c => c.Code == certificationCode, ct);
            if (cert is null) return Results.NotFound();
            var attempts = await db.Attempts.AsNoTracking().Include(a => a.Items).ThenInclude(i => i.QuestionVersion)
                .Where(a => a.UserId == uid && a.CertificationId == cert.Id && a.Status != AttemptStatuses.InProgress && !a.IsPreview)
                .OrderBy(a => a.FinishedAt).AsSplitQuery().ToListAsync(ct);
            object Agg(IEnumerable<Attempt> set)
            {
                var items = set.SelectMany(a => a.Items).ToList();
                return cert.Domains.OrderBy(d => d.SortOrder).Select(d =>
                {
                    var di = items.Where(i => i.DomainCode == d.Code).ToList();
                    return new
                    {
                        code = d.Code, name = d.Name, answered = di.Count, correct = di.Count(i => i.IsCorrect == true),
                        sampleNote = di.Count < 10 ? "Small sample: interpret with caution." : null
                    };
                });
            }
            var clean = attempts.Where(a => !a.FeedbackExposed).ToList();
            var assisted = attempts.Where(a => a.FeedbackExposed).ToList();
            return Results.Ok(new
            {
                certificationCode,
                note = "Clean attempts and assisted attempts are reported separately. No readiness guarantee is implied.",
                clean = new { attempts = clean.Count, byDomain = Agg(clean) },
                assisted = new { attempts = assisted.Count, byDomain = Agg(assisted) },
                timeline = attempts.Select(a => new { a.Id, a.FinishedAt, a.Mode, classification = a.Classification, a.Percent, a.PointsEarned, a.PointsMax, a.ProfileVersion })
            });
        }).RequireAuthorization();
    }
}
