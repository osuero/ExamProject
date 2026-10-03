using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ExamPrep.Api.Data;
using ExamPrep.Api.DomainLogic;
using ExamPrep.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExamPrep.Api.Modules;

public class BankFile
{
    public string? SchemaVersion { get; set; }
    public string ExamCode { get; set; } = "";
    public List<BankCase> Cases { get; set; } = new();
    public List<BankQuestion> Questions { get; set; } = new();
}

public class BankCase
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Locale { get; set; }
}

public class BankQuestion
{
    public string Id { get; set; } = "";
    public int? Version { get; set; }
    public string? ExamCode { get; set; }
    public string Locale { get; set; } = "en";
    public string DomainId { get; set; } = "";
    public string Objective { get; set; } = "";
    public string? ScenarioId { get; set; }
    public string QuestionType { get; set; } = QuestionTypes.Single;
    public int SelectCount { get; set; } = 1;
    public string Stem { get; set; } = "";
    public List<QuestionOption> Options { get; set; } = new();
    public List<string> CorrectOptionIds { get; set; } = new();
    public string Explanation { get; set; } = "";
    public int Points { get; set; } = 1;
    public string Difficulty { get; set; } = "";
    public string? DifficultyBasis { get; set; }
    public List<string> SourceIds { get; set; } = new();
    public string? SourceCheckedAt { get; set; }
    public string? Originality { get; set; }
    public string? FamilyId { get; set; }
    public List<string> Tags { get; set; } = new();
}

public record ImportItemReport(string ExternalId, string Action, List<ValidationIssue> Issues, string? NearDuplicateOf = null, double? Similarity = null);

public record ImportReport(string FileName, string Format, string Sha256, string ExamCode, bool Valid, List<string> FileErrors,
    List<ImportItemReport> Items, int CasesNew, int CasesUnchanged, int ToCreate, int Unchanged, int ToVersion, int Rejected);

public class ContentImporter(AppDbContext db, TimeProvider clock, Audit audit)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxQuestions = 1000;
    public const double NearDuplicateThreshold = 0.6;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    public static (BankFile? file, List<string> errors) Parse(string fileName, byte[] bytes)
    {
        var errors = new List<string>();
        if (bytes.Length == 0) return (null, new() { "File is empty." });
        if (bytes.Length > MaxBytes) return (null, new() { $"File exceeds {MaxBytes / 1024 / 1024} MB." });
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return (null, new() { "File is not valid UTF-8 text." }); }
        try
        {
            BankFile? f = ext switch
            {
                ".json" => JsonSerializer.Deserialize<BankFile>(text, Json),
                ".md" or ".markdown" => MarkdownBank.Parse(text),
                ".pdf" or ".docx" => throw new NotSupportedException("PDF and DOCX import is designed but not implemented yet. Convert to JSON or Markdown."),
                _ => throw new NotSupportedException("Only .json and .md files are accepted.")
            };
            if (f is null) return (null, new() { "File has no content." });
            if (f.Questions.Count > MaxQuestions) errors.Add($"Too many questions (max {MaxQuestions}).");
            if (string.IsNullOrWhiteSpace(f.ExamCode)) errors.Add("examCode is required.");
            return (f, errors);
        }
        catch (JsonException e) { return (null, new() { $"Invalid JSON: {e.Message}" }); }
        catch (NotSupportedException e) { return (null, new() { e.Message }); }
        catch (FormatException e) { return (null, new() { $"Invalid Markdown bank: {e.Message}" }); }
    }

    public static string ContentHash(QuestionVersion v) => Crypto.Sha256Hex(JsonSerializer.Serialize(new
    {
        v.DomainCode, v.Objective, v.Locale, v.ScenarioId, v.QuestionType, v.SelectCount, v.Stem,
        options = v.Options.Select(o => new { o.Id, o.Text, o.Rationale }),
        key = v.CorrectOptionIds.OrderBy(x => x, StringComparer.Ordinal), v.Explanation, v.Points,
        sources = v.SourceIds.OrderBy(x => x, StringComparer.Ordinal)
    }, Json));

    private static QuestionVersion ToVersion(BankQuestion q) => new()
    {
        DomainCode = q.DomainId, Objective = q.Objective ?? "", Locale = string.IsNullOrWhiteSpace(q.Locale) ? "en" : q.Locale,
        ScenarioId = string.IsNullOrWhiteSpace(q.ScenarioId) ? null : q.ScenarioId, QuestionType = q.QuestionType, SelectCount = q.SelectCount,
        Stem = q.Stem?.Trim() ?? "", Options = q.Options.Select(o => new QuestionOption { Id = o.Id, Text = o.Text?.Trim() ?? "", Rationale = o.Rationale?.Trim() ?? "" }).ToList(),
        CorrectOptionIds = q.CorrectOptionIds.ToList(), Explanation = q.Explanation?.Trim() ?? "", Points = q.Points, Difficulty = q.Difficulty ?? "",
        DifficultyBasis = q.DifficultyBasis ?? "editorial_estimate_not_calibrated", SourceIds = q.SourceIds.ToList(),
        SourceCheckedAt = q.SourceCheckedAt, Tags = q.Tags.ToList(), Originality = q.Originality ?? ""
    };

    public async Task<ImportReport> PreviewAsync(string fileName, byte[] bytes, CancellationToken ct) => (await Plan(fileName, bytes, ct)).report;

    private record PlannedItem(BankQuestion Source, QuestionVersion Version, Question? Existing, string Action);

    private async Task<(ImportReport report, BankFile? file, Certification? cert, List<PlannedItem> plan, List<BankCase> newCases)> Plan(string fileName, byte[] bytes, CancellationToken ct)
    {
        var sha = Crypto.Sha256Hex(bytes);
        var format = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        var (file, errors) = Parse(fileName, bytes);
        ImportReport Fail(List<string> e) => new(fileName, format, sha, file?.ExamCode ?? "", false, e, new(), 0, 0, 0, 0, 0, 0);
        if (file is null || errors.Count > 0) return (Fail(errors), null, null, new(), new());

        var cert = await db.Certifications.Include(c => c.Domains).SingleOrDefaultAsync(c => c.Code == file.ExamCode, ct);
        if (cert is null) return (Fail(new() { $"Unknown certification '{file.ExamCode}'." }), null, null, new(), new());

        var domainCodes = cert.Domains.Select(d => d.Code).ToHashSet();
        var knownSources = (await db.Sources.Select(s => s.Id).ToListAsync(ct)).ToHashSet();
        var existingScenarios = await db.Scenarios.Where(s => s.CertificationId == cert.Id).ToDictionaryAsync(s => s.Id, ct);
        var fileErrors = new List<string>();
        var newCases = new List<BankCase>();
        int casesUnchanged = 0;
        foreach (var c in file.Cases)
        {
            if (string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Text)) { fileErrors.Add("Every case needs an id and text."); continue; }
            if (QuestionValidator.ContainsMarkup(c.Text) || QuestionValidator.ContainsMarkup(c.Title)) { fileErrors.Add($"Case {c.Id} contains markup."); continue; }
            if (existingScenarios.TryGetValue(c.Id, out var ex))
            {
                if (ex.Text.Trim() == c.Text.Trim() && ex.Title.Trim() == c.Title.Trim()) casesUnchanged++;
                else fileErrors.Add($"Case {c.Id} already exists with different text. Cases are immutable once imported; use a new case id.");
            }
            else newCases.Add(c);
        }
        if (file.Cases.Select(c => c.Id).Distinct().Count() != file.Cases.Count) fileErrors.Add("Duplicate case ids in file.");
        var scenarioIds = existingScenarios.Keys.Concat(newCases.Select(c => c.Id)).ToHashSet();

        var ids = file.Questions.Select(q => q.Id).ToList();
        if (ids.Distinct().Count() != ids.Count) fileErrors.Add("Duplicate question ids in file.");

        var existing = await db.Questions.Include(q => q.Versions).Where(q => ids.Contains(q.ExternalId)).ToDictionaryAsync(q => q.ExternalId, ct);
        // latest versions of all other questions of this cert, for near-duplicate detection
        var corpus = await db.QuestionVersions.Include(v => v.Question).Where(v => v.Question!.CertificationId == cert.Id).ToListAsync(ct);
        var latestCorpus = corpus.GroupBy(v => v.QuestionId).Select(g => g.OrderByDescending(v => v.VersionNo).First()).ToList();

        var plan = new List<PlannedItem>();
        var items = new List<ImportItemReport>();
        var accepted = new List<(string id, QuestionVersion v)>();
        foreach (var q in file.Questions)
        {
            var v = ToVersion(q);
            var issues = QuestionValidator.Validate(v, domainCodes, knownSources, scenarioIds);
            if (string.IsNullOrWhiteSpace(q.Id) || q.Id.Length > 80 || !Regex.IsMatch(q.Id, "^[A-Za-z0-9._-]+$"))
                issues.Add(new("error", "id_invalid", "Question id must be 1-80 characters of letters, digits, '.', '_' or '-'."));
            if (q.ExamCode is not null && q.ExamCode != file.ExamCode) issues.Add(new("error", "exam_mismatch", "Question examCode differs from the file examCode."));
            v.ContentHash = ContentHash(v);

            existing.TryGetValue(q.Id, out var ex);
            string action;
            if (ex is not null)
            {
                if (ex.CertificationId != cert.Id) issues.Add(new("error", "exam_mismatch", "This id belongs to another certification."));
                var latest = ex.Versions.OrderByDescending(x => x.VersionNo).First();
                if (latest.ContentHash == v.ContentHash) action = "unchanged";
                else
                {
                    var sameItem = latest.Stem == v.Stem && latest.Options.Select(o => o.Text).SequenceEqual(v.Options.Select(o => o.Text));
                    if (sameItem && !latest.CorrectOptionIds.OrderBy(x => x).SequenceEqual(v.CorrectOptionIds.OrderBy(x => x)))
                        issues.Add(new("error", "key_changed_same_content", "Same stem and options but a different answer key. Resolve the key conflict with evidence before importing."));
                    action = "new_version";
                }
                if (q.FamilyId is not null && q.FamilyId != ex.FamilyId) issues.Add(new("warning", "family_changed_ignored", "Family id differs from the stored family; the stored family is kept."));
            }
            else action = "create";

            string? dupOf = null; double? sim = null;
            if (action != "unchanged")
            {
                var exact = latestCorpus.FirstOrDefault(c => c.ContentHash == v.ContentHash && c.Question!.ExternalId != q.Id);
                if (exact is not null) issues.Add(new("error", "exact_duplicate", $"Identical content already exists as {exact.Question!.ExternalId}."));
                foreach (var c in latestCorpus.Where(c => c.Question!.ExternalId != q.Id).Select(c => (c.Question!.ExternalId, v: c))
                             .Concat(accepted.Select(a => (ExternalId: a.id, a.v))))
                {
                    var s = QuestionValidator.Similarity(v, c.v);
                    if (s >= NearDuplicateThreshold && (sim is null || s > sim)) { sim = Math.Round(s, 3); dupOf = c.ExternalId; }
                }
                if (dupOf is not null) issues.Add(new("warning", "near_duplicate", $"Very similar to {dupOf} (similarity {sim}). Group as a family variant or rewrite."));
            }
            if (issues.Any(i => i.Severity == "error")) action = "reject";
            else accepted.Add((q.Id, v));
            items.Add(new ImportItemReport(q.Id, action, issues, dupOf, sim));
            plan.Add(new PlannedItem(q, v, ex, action));
        }

        var report = new ImportReport(fileName, format, sha, file.ExamCode, fileErrors.Count == 0 && items.All(i => i.Action != "reject"), fileErrors, items,
            newCases.Count, casesUnchanged, items.Count(i => i.Action == "create"), items.Count(i => i.Action == "unchanged"),
            items.Count(i => i.Action == "new_version"), items.Count(i => i.Action == "reject"));
        return (report, file, cert, plan, newCases);
    }

    /// <summary>Commits only a fully valid file (all-or-nothing). Re-running the same file is a no-op.</summary>
    public async Task<(ImportReport report, Guid? batchId)> CommitAsync(string fileName, byte[] bytes, Guid? actor, string provenance, CancellationToken ct)
    {
        var (report, file, cert, plan, newCases) = await Plan(fileName, bytes, ct);
        if (!report.Valid || file is null || cert is null) return (report, null);
        var now = clock.GetUtcNow();
        var strategy = db.Database.CreateExecutionStrategy();
        Guid? batchId = null;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var batch = new ImportBatch
            {
                FileName = Path.GetFileName(fileName), Format = report.Format, Sha256 = report.Sha256, CertificationCode = cert.Code,
                Created = report.ToCreate, Unchanged = report.Unchanged, NewVersions = report.ToVersion, Rejected = 0,
                ReportJson = JsonSerializer.Serialize(report, Json), CreatedByUserId = actor, CreatedAt = now
            };
            db.ImportBatches.Add(batch);
            foreach (var c in newCases)
                db.Scenarios.Add(new Scenario { Id = c.Id, CertificationId = cert.Id, Title = c.Title, Text = c.Text, Locale = c.Locale ?? "en" });
            foreach (var p in plan.Where(p => p.Action is "create" or "new_version"))
            {
                var v = p.Version;
                v.CreatedAt = now; v.UpdatedAt = now; v.ImportBatchId = batch.Id; v.Status = QuestionStatuses.Draft;
                v.Provenance = $"{provenance}; file={Path.GetFileName(fileName)}; sha256={report.Sha256}";
                if (p.Existing is null)
                {
                    var q = new Question { ExternalId = p.Source.Id, CertificationId = cert.Id, FamilyId = p.Source.FamilyId ?? p.Source.Id, CreatedAt = now };
                    v.VersionNo = 1;
                    q.Versions.Add(v);
                    db.Questions.Add(q);
                }
                else
                {
                    v.QuestionId = p.Existing.Id;
                    v.VersionNo = p.Existing.Versions.Max(x => x.VersionNo) + 1;
                    db.QuestionVersions.Add(v);
                }
            }
            audit.Record(actor, "import.committed", "import_batch", batch.Id.ToString(), new { report.ToCreate, report.ToVersion, report.Unchanged, report.Sha256 });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            batchId = batch.Id;
        });
        return (report, batchId);
    }
}

/// <summary>Markdown bank format, documented in docs/content-format.md.</summary>
public static class MarkdownBank
{
    public static BankFile Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var file = new BankFile();
        int i = 0;
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            i = 1;
            for (; i < lines.Length && lines[i].Trim() != "---"; i++)
            {
                var (k, v) = KeyValue(lines[i]);
                if (k == "examcode") file.ExamCode = v;
                if (k == "schemaversion") file.SchemaVersion = v;
            }
            if (i >= lines.Length) throw new FormatException("Front matter is not closed with '---'.");
            i++;
        }
        BankQuestion? q = null; BankCase? c = null; string section = "stem";
        var buf = new StringBuilder();
        void Flush()
        {
            var t = buf.ToString().Trim(); buf.Clear();
            if (c is not null) { c.Text = t; c = null; return; }
            if (q is null) return;
            if (section == "stem") q.Stem = t;
            else if (section == "explanation") q.Explanation = t;
        }
        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            var caseM = Regex.Match(line, @"^##\s+Case\s+([A-Za-z0-9._-]+)\s*\|\s*(.+)$");
            var qM = Regex.Match(line, @"^##\s+Question\s+([A-Za-z0-9._-]+)\s*$");
            if (caseM.Success)
            {
                Flush(); q = null;
                c = new BankCase { Id = caseM.Groups[1].Value, Title = caseM.Groups[2].Value.Trim() };
                file.Cases.Add(c);
                continue;
            }
            if (qM.Success)
            {
                Flush(); c = null;
                q = new BankQuestion { Id = qM.Groups[1].Value };
                file.Questions.Add(q); section = "meta";
                continue;
            }
            if (q is null) { if (c is not null) buf.AppendLine(line); continue; }
            if (Regex.IsMatch(line, @"^###\s+Options\s*$", RegexOptions.IgnoreCase)) { Flush(); section = "options"; continue; }
            if (Regex.IsMatch(line, @"^###\s+Explanation\s*$", RegexOptions.IgnoreCase)) { Flush(); section = "explanation"; continue; }
            switch (section)
            {
                case "meta":
                    if (string.IsNullOrWhiteSpace(line)) { section = "stem"; continue; }
                    var mm = Regex.Match(line, @"^-\s*([A-Za-z]+)\s*:\s*(.*)$");
                    if (!mm.Success) { section = "stem"; buf.AppendLine(line); continue; }
                    var key = mm.Groups[1].Value.ToLowerInvariant(); var val = mm.Groups[2].Value.Trim();
                    switch (key)
                    {
                        case "domain": q.DomainId = val; break;
                        case "objective": q.Objective = val; break;
                        case "type": q.QuestionType = val; break;
                        case "select": q.SelectCount = int.TryParse(val, out var n) ? n : throw new FormatException($"{q.Id}: select must be a number."); break;
                        case "scenario": q.ScenarioId = val; break;
                        case "difficulty": q.Difficulty = val; break;
                        case "sources": q.SourceIds = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); break;
                        case "family": q.FamilyId = val; break;
                        case "locale": q.Locale = val; break;
                        case "points": q.Points = int.TryParse(val, out var p) ? p : 1; break;
                        case "tags": q.Tags = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); break;
                        case "checked": q.SourceCheckedAt = val; break;
                        default: throw new FormatException($"{q.Id}: unknown field '{key}'.");
                    }
                    break;
                case "options":
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var om = Regex.Match(line, @"^-\s*\[( |x|X)\]\s*([A-Z])\s*:\s*(.+?)\s*(?:\|\s*rationale\s*:\s*(.+))?$");
                    if (!om.Success) throw new FormatException($"{q.Id}: option line not understood: '{line.Trim()}'.");
                    q.Options.Add(new QuestionOption { Id = om.Groups[2].Value, Text = om.Groups[3].Value, Rationale = om.Groups[4].Value });
                    if (om.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase)) q.CorrectOptionIds.Add(om.Groups[2].Value);
                    break;
                default:
                    buf.AppendLine(line);
                    break;
            }
        }
        Flush();
        return file;
    }

    private static (string, string) KeyValue(string line)
    {
        var idx = line.IndexOf(':');
        return idx < 0 ? ("", "") : (line[..idx].Trim().ToLowerInvariant(), line[(idx + 1)..].Trim());
    }
}
