using ExamPrep.Api.Data;

namespace ExamPrep.Api.DomainLogic;

/// <summary>Simulator scoring policy v1: one point per item, exact set required, no penalty, no partial credit.
/// This is a configurable simulator rule, not an official Pearson VUE / Anthropic policy.</summary>
public static class Scoring
{
    public static bool IsExactMatch(IReadOnlyCollection<string>? selected, IReadOnlyCollection<string> correct)
    {
        if (selected is null || selected.Count == 0) return false;
        var s = new HashSet<string>(selected, StringComparer.Ordinal);
        return s.Count == selected.Count && s.SetEquals(correct);
    }

    public record Totals(int Earned, int Max, int Correct, int Incorrect, int Omitted, decimal Percent, bool Passed);

    public static Totals Compute(IEnumerable<(IReadOnlyCollection<string>? scored, IReadOnlyCollection<string> correct, int points)> items, decimal passPercent)
    {
        int earned = 0, max = 0, ok = 0, bad = 0, omitted = 0;
        foreach (var (scored, correct, points) in items)
        {
            max += points;
            if (scored is null || scored.Count == 0) { omitted++; continue; }
            if (IsExactMatch(scored, correct)) { ok++; earned += points; } else bad++;
        }
        var pct = max == 0 ? 0m : Math.Round(100m * earned / max, 2);
        return new Totals(earned, max, ok, bad, omitted, pct, max > 0 && pct >= passPercent);
    }
}

public static class Allocation
{
    /// <summary>Proportional allocation with the largest-remainder method. Ties broken by input order.</summary>
    public static Dictionary<string, int> LargestRemainder(IReadOnlyList<DomainWeight> weights, int total)
    {
        var result = new Dictionary<string, int>();
        if (weights.Count == 0 || total <= 0) return result;
        var sum = weights.Sum(w => w.WeightPercent);
        if (sum <= 0) throw new ArgumentException("Weights must sum to a positive number.");
        var quotas = weights.Select((w, i) => (w.Code, i, exact: total * w.WeightPercent / sum)).ToList();
        foreach (var q in quotas) result[q.Code] = (int)Math.Floor(q.exact);
        var remaining = total - result.Values.Sum();
        foreach (var q in quotas.OrderByDescending(q => q.exact - Math.Floor(q.exact)).ThenBy(q => q.i).Take(remaining))
            result[q.Code]++;
        return result;
    }
}

public record Candidate(Guid VersionId, string DomainCode, string FamilyId, string? ScenarioId, string ExternalId);

public record AssemblyResult(bool Ok, List<Candidate> Items, Dictionary<string, int> Target, Dictionary<string, int> Actual, Dictionary<string, int> Available, string? Error);

public static class ExamAssembler
{
    /// <summary>Select questions per domain target. One item per family. If a domain lacks items, the deficit is
    /// redistributed to domains with spare items (largest weight first) and reported. Never duplicates items.</summary>
    public static AssemblyResult Assemble(IReadOnlyList<Candidate> pool, IReadOnlyList<DomainWeight> weights, int total, Random rng)
    {
        // one candidate per family (random representative)
        var byFamily = pool.GroupBy(c => c.FamilyId).Select(g => g.OrderBy(_ => rng.Next()).First()).ToList();
        var available = weights.ToDictionary(w => w.Code, w => byFamily.Count(c => c.DomainCode == w.Code));
        var target = Allocation.LargestRemainder(weights, total);
        var eligible = byFamily.Count(c => target.ContainsKey(c.DomainCode));
        if (eligible < total)
            return new AssemblyResult(false, new(), target, new(), available,
                $"Only {eligible} eligible questions are available; {total} are required. No items were duplicated.");

        var actual = target.ToDictionary(kv => kv.Key, kv => Math.Min(kv.Value, available[kv.Key]));
        var deficit = total - actual.Values.Sum();
        foreach (var w in weights.OrderByDescending(w => w.WeightPercent))
        {
            if (deficit == 0) break;
            var spare = available[w.Code] - actual[w.Code];
            var add = Math.Min(spare, deficit);
            actual[w.Code] += add; deficit -= add;
        }

        var chosen = new List<Candidate>();
        foreach (var w in weights)
            chosen.AddRange(byFamily.Where(c => c.DomainCode == w.Code).OrderBy(_ => rng.Next()).Take(actual[w.Code]));

        return new AssemblyResult(true, Order(chosen, rng), target, actual, available, null);
    }

    /// <summary>Random order, but items sharing a scenario stay contiguous in a stable order (by external id).</summary>
    public static List<Candidate> Order(List<Candidate> items, Random rng)
    {
        var blocks = items.GroupBy(c => c.ScenarioId ?? ("__" + c.VersionId))
            .Select(g => g.OrderBy(c => c.ExternalId, StringComparer.Ordinal).ToList())
            .OrderBy(_ => rng.Next())
            .ToList();
        return blocks.SelectMany(b => b).ToList();
    }
}

public static class Lifecycle
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [QuestionStatuses.Draft] = new[] { QuestionStatuses.TechnicalReview, QuestionStatuses.Quarantined, QuestionStatuses.Retired },
        [QuestionStatuses.TechnicalReview] = new[] { QuestionStatuses.EditorialReview, QuestionStatuses.Draft, QuestionStatuses.Quarantined, QuestionStatuses.Retired },
        [QuestionStatuses.EditorialReview] = new[] { QuestionStatuses.Approved, QuestionStatuses.Draft, QuestionStatuses.Quarantined, QuestionStatuses.Retired },
        [QuestionStatuses.Approved] = new[] { QuestionStatuses.Published, QuestionStatuses.Quarantined, QuestionStatuses.Retired },
        [QuestionStatuses.Published] = new[] { QuestionStatuses.Quarantined, QuestionStatuses.Retired },
        [QuestionStatuses.Quarantined] = new[] { QuestionStatuses.Draft, QuestionStatuses.Retired },
        [QuestionStatuses.Retired] = Array.Empty<string>(),
    };

    public static bool CanTransition(string from, string to) => Allowed.TryGetValue(from, out var t) && t.Contains(to);
    public static IReadOnlyList<string> NextStates(string from) => Allowed.TryGetValue(from, out var t) ? t : Array.Empty<string>();
}

public record ValidationIssue(string Severity, string Code, string Message);

public static class QuestionValidator
{
    public const int MaxStem = 4000, MaxOption = 1000, MaxExplanation = 4000;

    public static List<ValidationIssue> Validate(QuestionVersion q, IReadOnlySet<string> domainCodes, IReadOnlySet<string> knownSources, IReadOnlySet<string>? scenarioIds = null)
    {
        var issues = new List<ValidationIssue>();
        void Err(string c, string m) => issues.Add(new("error", c, m));
        void Warn(string c, string m) => issues.Add(new("warning", c, m));

        if (string.IsNullOrWhiteSpace(q.Stem)) Err("stem_missing", "Stem is required.");
        if (q.Stem.Length > MaxStem) Err("stem_too_long", $"Stem exceeds {MaxStem} characters.");
        if (!domainCodes.Contains(q.DomainCode)) Err("domain_unknown", $"Domain '{q.DomainCode}' is not part of the certification.");
        if (q.QuestionType is not (QuestionTypes.Single or QuestionTypes.Multiple)) Err("type_unknown", $"Question type '{q.QuestionType}' is not supported.");
        if (q.Options.Count is < 4 or > 5) Err("option_count", "Questions need four or five options.");
        var ids = q.Options.Select(o => o.Id).ToList();
        if (ids.Distinct().Count() != ids.Count) Err("option_id_duplicate", "Option ids must be unique.");
        if (q.Options.Any(o => string.IsNullOrWhiteSpace(o.Text))) Err("option_text_missing", "Every option needs text.");
        if (q.Options.Any(o => o.Text.Length > MaxOption)) Err("option_too_long", $"Option text exceeds {MaxOption} characters.");
        if (q.Options.Any(o => string.IsNullOrWhiteSpace(o.Rationale))) Err("rationale_missing", "Every option needs its own explanation.");
        var texts = q.Options.Select(o => Normalize(o.Text)).ToList();
        if (texts.Distinct().Count() != texts.Count) Err("option_text_duplicate", "Options must be distinct.");
        if (q.CorrectOptionIds.Count == 0) Err("key_missing", "A correct option set is required.");
        if (q.CorrectOptionIds.Any(c => !ids.Contains(c))) Err("key_inconsistent", "Correct options reference unknown option ids.");
        if (q.CorrectOptionIds.Distinct().Count() != q.CorrectOptionIds.Count) Err("key_duplicate", "Correct option ids repeat.");
        if (q.SelectCount != q.CorrectOptionIds.Count) Err("select_count_mismatch", "selectCount must equal the number of correct options.");
        if (q.QuestionType == QuestionTypes.Single && q.SelectCount != 1) Err("single_select_count", "Single choice questions select exactly one option.");
        if (q.QuestionType == QuestionTypes.Multiple && (q.SelectCount < 2 || q.SelectCount >= q.Options.Count)) Err("multi_select_count", "Multiple response needs at least two correct options and at least one incorrect option.");
        if (string.IsNullOrWhiteSpace(q.Explanation)) Err("explanation_missing", "General explanation is required.");
        if (q.Explanation.Length > MaxExplanation) Err("explanation_too_long", "Explanation is too long.");
        if (q.Points < 1) Err("points_invalid", "Points must be positive.");
        if (q.SourceIds.Count == 0) Err("sources_missing", "At least one specific source is required.");
        foreach (var s in q.SourceIds.Where(s => !knownSources.Contains(s))) Warn("source_unregistered", $"Source '{s}' is not registered in the source catalog.");
        if (scenarioIds is not null && q.ScenarioId is not null && !scenarioIds.Contains(q.ScenarioId)) Err("scenario_unknown", $"Scenario '{q.ScenarioId}' does not exist.");
        if (ContainsMarkup(q.Stem) || q.Options.Any(o => ContainsMarkup(o.Text) || ContainsMarkup(o.Rationale)) || ContainsMarkup(q.Explanation))
            Err("markup_not_allowed", "HTML or script markup is not allowed in question content.");

        // Editorial heuristic: correct option is the strictly longest.
        if (q.Options.Count > 0 && q.CorrectOptionIds.Count == 1)
        {
            var longest = q.Options.OrderByDescending(o => o.Text.Length).First();
            var second = q.Options.OrderByDescending(o => o.Text.Length).Skip(1).FirstOrDefault();
            if (q.CorrectOptionIds.Contains(longest.Id) && second is not null && longest.Text.Length > second.Text.Length * 1.15)
                Warn("length_cue", "The correct option is noticeably longer than every distractor.");
        }
        return issues;
    }

    public static string Normalize(string s) => string.Join(' ', s.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Rejects active or rendering HTML. Plain XML-style tags are legitimate content in prompt-engineering
    /// questions (e.g. "&lt;example&gt; tags"); the UI always renders content as escaped text.</summary>
    public static bool ContainsMarkup(string s) =>
        System.Text.RegularExpressions.Regex.IsMatch(s ?? "",
            @"<\s*/?\s*(script|style|iframe|frame|object|embed|img|svg|math|link|meta|base|form|input|button|a|video|audio|source|template)\b|<[^>]*\bon[a-z]+\s*=|javascript:|data:text/html",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Jaccard similarity on word 3-shingles of stem+options, for near-duplicate detection.</summary>
    public static double Similarity(QuestionVersion a, QuestionVersion b)
    {
        static HashSet<string> Shingles(QuestionVersion q)
        {
            var words = Normalize(q.Stem + " " + string.Join(' ', q.Options.Select(o => o.Text))).Split(' ');
            var set = new HashSet<string>();
            for (int i = 0; i + 2 < words.Length; i++) set.Add(words[i] + " " + words[i + 1] + " " + words[i + 2]);
            return set;
        }
        var sa = Shingles(a); var sb = Shingles(b);
        if (sa.Count == 0 || sb.Count == 0) return 0;
        var inter = sa.Count(sb.Contains);
        return (double)inter / (sa.Count + sb.Count - inter);
    }
}
