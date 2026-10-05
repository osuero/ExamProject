using ExamPrep.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamPrep.Api.Modules;

public static class CatalogEndpoints
{
    public static async Task<Dictionary<string, int>> PublishedCountsByDomain(AppDbContext db, Guid certId, IReadOnlyCollection<string> locales, CancellationToken ct)
    {
        // Latest published version per question, in a verified locale, one per family.
        var rows = await db.QuestionVersions
            .Where(v => v.Question!.CertificationId == certId && v.Status == QuestionStatuses.Published && locales.Contains(v.Locale))
            .Select(v => new { v.DomainCode, v.Question!.FamilyId })
            .ToListAsync(ct);
        return rows.DistinctBy(r => r.FamilyId).GroupBy(r => r.DomainCode).ToDictionary(g => g.Key, g => g.Count());
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/catalog", async (AppDbContext db, CancellationToken ct) =>
        {
            var certs = await db.Certifications.Include(c => c.Domains).Include(c => c.Languages).Include(c => c.Profiles)
                .OrderBy(c => c.SortOrder).AsSplitQuery().ToListAsync(ct);
            var list = new List<object>();
            foreach (var c in certs)
            {
                var p = c.Profiles.Single(x => x.IsCurrent);
                var locales = c.Languages.Select(l => l.Locale).ToList();
                var counts = await PublishedCountsByDomain(db, c.Id, locales, ct);
                var published = counts.Values.Sum();
                list.Add(new
                {
                    code = c.Code, title = c.Title, level = c.Level, shortDescription = c.ShortDescription,
                    languages = locales, languageStatus = locales.Count == 0 ? "pending_verification" : "verified",
                    questionCount = p.QuestionCount, durationMinutes = p.ExamDurationMinutes,
                    profileVersion = p.Version, profileVerification = p.VerificationStatus,
                    domains = c.Domains.OrderBy(d => d.SortOrder).Select(d => new { d.Code, d.Name, weightPercent = p.DomainWeights.FirstOrDefault(w => w.Code == d.Code)?.WeightPercent }),
                    availability = new
                    {
                        publishedQuestions = published,
                        fullSimulationAvailable = published >= p.QuestionCount,
                        practiceAvailable = published > 0
                    }
                });
            }
            return Results.Ok(list);
        });

        app.MapGet("/api/catalog/{code}", async (string code, AppDbContext db, CancellationToken ct) =>
        {
            var c = await db.Certifications.Include(x => x.Domains).Include(x => x.Languages).Include(x => x.Profiles)
                .AsSplitQuery().SingleOrDefaultAsync(x => x.Code == code, ct);
            if (c is null) return Results.NotFound();
            var p = c.Profiles.Single(x => x.IsCurrent);
            var locales = c.Languages.Select(l => l.Locale).ToList();
            var counts = await PublishedCountsByDomain(db, c.Id, locales, ct);
            var sources = await db.Sources.Where(s => p.SourceIds.Contains(s.Id)).Select(s => new { s.Id, s.Title, s.Url, s.Kind, s.Confidence }).ToListAsync(ct);
            return Results.Ok(new
            {
                code = c.Code, title = c.Title, level = c.Level, shortDescription = c.ShortDescription, description = c.Description,
                disclaimer = c.Disclaimer,
                languages = c.Languages.Select(l => new { l.Locale, l.SourceId, l.VerifiedAt }),
                languageStatus = locales.Count == 0 ? "pending_verification" : "verified",
                profile = new
                {
                    version = p.Version, questionCount = p.QuestionCount, examDurationMinutes = p.ExamDurationMinutes,
                    appointmentDurationMinutes = p.AppointmentDurationMinutes, allowedQuestionTypes = p.AllowedQuestionTypes, scenariosPerForm = p.ScenariosPerForm,
                    simulatorPassPercent = p.SimulatorPassPercent, scoringPolicy = p.ScoringPolicy,
                    officialScoreReference = p.OfficialScoreReference, verificationStatus = p.VerificationStatus,
                    blueprintVersion = p.BlueprintVersion, notes = p.Notes, sources
                },
                domains = c.Domains.OrderBy(d => d.SortOrder).Select(d => new
                {
                    d.Code, d.Name, weightPercent = p.DomainWeights.FirstOrDefault(w => w.Code == d.Code)?.WeightPercent,
                    objectives = d.Objectives, publishedQuestions = counts.GetValueOrDefault(d.Code)
                }),
                modes = new[]
                {
                    new { id = AttemptModes.Practice, name = "Practice", description = "Untimed by default, optional immediate feedback with explanations for every option." },
                    new { id = AttemptModes.Simulation, name = "Simulation", description = $"{p.QuestionCount} questions in {p.ExamDurationMinutes} minutes, distributed by domain weights" + (p.ScenariosPerForm is int k ? $" and drawn from {k} randomly chosen scenarios" : "") + ". Feedback hidden until the end." },
                    new { id = AttemptModes.Custom, name = "Custom", description = "Choose domains, number of questions and time. Does not reproduce the real format." },
                },
                availability = new
                {
                    publishedQuestions = counts.Values.Sum(),
                    fullSimulationAvailable = counts.Values.Sum() >= p.QuestionCount
                }
            });
        });
    }
}
