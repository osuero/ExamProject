using ExamPrep.Api.Data;
using ExamPrep.Api.DomainLogic;
using ExamPrep.Api.Modules;

namespace ExamPrep.Tests;

public class ScoringTests
{
    [Fact] public void Exact_set_required_for_multiple_response()
    {
        Assert.True(Scoring.IsExactMatch(new[] { "B", "E" }, new[] { "E", "B" }));
        Assert.False(Scoring.IsExactMatch(new[] { "B" }, new[] { "B", "E" }));
        Assert.False(Scoring.IsExactMatch(new[] { "B", "E", "A" }, new[] { "B", "E" }));
        Assert.False(Scoring.IsExactMatch(new[] { "B", "B" }, new[] { "B", "E" }));
        Assert.False(Scoring.IsExactMatch(Array.Empty<string>(), new[] { "A" }));
    }

    [Fact] public void Totals_have_no_penalty_and_count_omissions()
    {
        var t = Scoring.Compute(new (IReadOnlyCollection<string>?, IReadOnlyCollection<string>, int)[]
        {
            (new[] { "A" }, new[] { "A" }, 1), (new[] { "B" }, new[] { "A" }, 1), (null, new[] { "C" }, 1), (Array.Empty<string>(), new[] { "C" }, 1), (new[] { "A", "B" }, new[] { "A", "B" }, 1)
        }, 80m);
        Assert.Equal((2, 5, 2, 1, 2), (t.Earned, t.Max, t.Correct, t.Incorrect, t.Omitted));
        Assert.Equal(40m, t.Percent);
        Assert.False(t.Passed);
    }

    [Fact] public void Pass_at_threshold_inclusive()
    {
        var items = Enumerable.Range(0, 5).Select(i => ((IReadOnlyCollection<string>?)new[] { i < 4 ? "A" : "B" }, (IReadOnlyCollection<string>)new[] { "A" }, 1));
        Assert.True(Scoring.Compute(items, 80m).Passed);
    }
}

public class AllocationTests
{
    private static readonly List<DomainWeight> Dev = new()
    {
        new() { Code = "D1", WeightPercent = 14.7m }, new() { Code = "D2", WeightPercent = 33.1m }, new() { Code = "D3", WeightPercent = 3.1m },
        new() { Code = "D4", WeightPercent = 2.6m }, new() { Code = "D5", WeightPercent = 16.8m }, new() { Code = "D6", WeightPercent = 11m },
        new() { Code = "D7", WeightPercent = 8.1m }, new() { Code = "D8", WeightPercent = 10.6m },
    };

    [Fact] public void Largest_remainder_sums_to_total()
    {
        var a = Allocation.LargestRemainder(Dev, 53);
        Assert.Equal(53, a.Values.Sum());
        Assert.Equal(new[] { 8, 17, 2, 1, 9, 6, 4, 6 }, Dev.Select(d => a[d.Code]).ToArray());
    }

    [Theory, InlineData(1), InlineData(7), InlineData(60), InlineData(300)]
    public void Allocation_is_exact_for_any_total(int total) => Assert.Equal(total, Allocation.LargestRemainder(Dev, total).Values.Sum());
}

public class AssemblerTests
{
    private static List<Candidate> Pool(params (string domain, int n)[] spec)
    {
        var list = new List<Candidate>();
        foreach (var (d, n) in spec)
            for (var i = 0; i < n; i++) list.Add(new Candidate(Guid.NewGuid(), d, $"{d}-F{i}", null, $"{d}-{i:000}"));
        return list;
    }
    private static readonly List<DomainWeight> W = new() { new() { Code = "A", WeightPercent = 50 }, new() { Code = "B", WeightPercent = 50 } };

    [Fact] public void Never_duplicates_and_rejects_when_insufficient()
    {
        var r = ExamAssembler.Assemble(Pool(("A", 3), ("B", 3)), W, 7, new Random(1));
        Assert.False(r.Ok);
        Assert.Contains("6 eligible", r.Error);
    }

    [Fact] public void Redistributes_deficit_without_duplicates()
    {
        var r = ExamAssembler.Assemble(Pool(("A", 2), ("B", 10)), W, 8, new Random(1));
        Assert.True(r.Ok);
        Assert.Equal(8, r.Items.Count);
        Assert.Equal(8, r.Items.Select(i => i.VersionId).Distinct().Count());
        Assert.Equal(2, r.Actual["A"]);
        Assert.Equal(6, r.Actual["B"]);
    }

    [Fact] public void One_item_per_family()
    {
        var pool = new List<Candidate>
        {
            new(Guid.NewGuid(), "A", "fam1", null, "x1"), new(Guid.NewGuid(), "A", "fam1", null, "x2"),
            new(Guid.NewGuid(), "A", "fam2", null, "x3"), new(Guid.NewGuid(), "B", "fam3", null, "x4"),
        };
        var r = ExamAssembler.Assemble(pool, W, 4, new Random(2));
        Assert.False(r.Ok); // only 3 families exist
        var ok = ExamAssembler.Assemble(pool, W, 3, new Random(2));
        Assert.True(ok.Ok);
        Assert.Equal(3, ok.Items.Select(i => i.FamilyId).Distinct().Count());
    }

    [Fact] public void Scenario_items_stay_contiguous_in_stable_order()
    {
        var items = new List<Candidate>
        {
            new(Guid.NewGuid(), "A", "f1", "CASE-1", "Q-03"), new(Guid.NewGuid(), "A", "f2", null, "Q-10"),
            new(Guid.NewGuid(), "A", "f3", "CASE-1", "Q-01"), new(Guid.NewGuid(), "A", "f4", "CASE-2", "Q-05"),
            new(Guid.NewGuid(), "A", "f5", "CASE-1", "Q-02"),
        };
        for (var seed = 0; seed < 20; seed++)
        {
            var o = ExamAssembler.Order(items, new Random(seed)).Select(c => c.ExternalId).ToList();
            var i = o.IndexOf("Q-01");
            Assert.Equal(new[] { "Q-01", "Q-02", "Q-03" }, o.Skip(i).Take(3));
        }
    }
}

public class LifecycleAndValidationTests
{
    [Theory]
    [InlineData("draft", "technical_review", true)]
    [InlineData("draft", "published", false)]
    [InlineData("approved", "published", true)]
    [InlineData("retired", "draft", false)]
    [InlineData("published", "quarantined", true)]
    public void Transitions(string from, string to, bool ok) => Assert.Equal(ok, Lifecycle.CanTransition(from, to));

    private static QuestionVersion Valid() => new()
    {
        DomainCode = "D1", QuestionType = QuestionTypes.Single, SelectCount = 1, Stem = "A team needs X. What should it do?",
        Options = "ABCD".Select(c => new QuestionOption { Id = c.ToString(), Text = "Option text " + c, Rationale = "Because " + c }).ToList(),
        CorrectOptionIds = new() { "B" }, Explanation = "Principle.", Points = 1, SourceIds = new() { "T01" }
    };

    private static List<ValidationIssue> V(QuestionVersion q) => QuestionValidator.Validate(q, new HashSet<string> { "D1" }, new HashSet<string> { "T01" });

    [Fact] public void Valid_question_has_no_errors() => Assert.DoesNotContain(V(Valid()), i => i.Severity == "error");

    [Fact] public void Inconsistent_key_is_rejected()
    {
        var q = Valid(); q.CorrectOptionIds = new() { "Z" };
        Assert.Contains(V(q), i => i.Code == "key_inconsistent");
    }

    [Fact] public void Multiple_response_needs_matching_select_count()
    {
        var q = Valid(); q.QuestionType = QuestionTypes.Multiple; q.CorrectOptionIds = new() { "A", "B" }; q.SelectCount = 1;
        Assert.Contains(V(q), i => i.Code == "select_count_mismatch");
    }

    [Fact] public void Active_markup_rejected_but_xml_style_tags_allowed()
    {
        var q = Valid(); q.Stem = "Wrap the examples in <example> tags.";
        Assert.DoesNotContain(V(q), i => i.Code == "markup_not_allowed");
        q.Stem = "Look <img src=x onerror=alert(1)>";
        Assert.Contains(V(q), i => i.Code == "markup_not_allowed");
        q.Stem = "<script>alert(1)</script>";
        Assert.Contains(V(q), i => i.Code == "markup_not_allowed");
    }

    [Fact] public void Length_cue_is_a_warning()
    {
        var q = Valid(); q.Options[1].Text = new string('x', 200);
        Assert.Contains(V(q), i => i.Code == "length_cue" && i.Severity == "warning");
    }

    [Fact] public void Content_hash_is_stable_and_key_sensitive()
    {
        var a = Valid(); var b = Valid();
        Assert.Equal(ContentImporter.ContentHash(a), ContentImporter.ContentHash(b));
        b.CorrectOptionIds = new() { "C" };
        Assert.NotEqual(ContentImporter.ContentHash(a), ContentImporter.ContentHash(b));
    }
}

public class MarkdownBankTests
{
    private const string Md = """
---
examCode: CCAR-F
---
## Case MD-CASE-1 | Test case
A case text.

## Question MD-001
- domain: A1
- objective: 1.1 Loops
- type: single_choice
- select: 1
- scenario: MD-CASE-1
- sources: T13

What should the loop check?

### Options
- [ ] A: The text of the reply | rationale: Not reliable.
- [x] B: The stop_reason value | rationale: Documented signal.
- [ ] C: The number of tokens | rationale: Not a completion signal.
- [ ] D: A fixed iteration cap only | rationale: Safety net, not the main signal.

### Explanation
Use stop_reason.
""";

    [Fact] public void Parses_cases_questions_options_and_key()
    {
        var f = MarkdownBank.Parse(Md);
        Assert.Equal("CCAR-F", f.ExamCode);
        Assert.Single(f.Cases);
        Assert.Equal("A case text.", f.Cases[0].Text);
        var q = Assert.Single(f.Questions);
        Assert.Equal("What should the loop check?", q.Stem);
        Assert.Equal(new[] { "B" }, q.CorrectOptionIds);
        Assert.Equal(4, q.Options.Count);
        Assert.Equal("Documented signal.", q.Options[1].Rationale);
        Assert.Equal("Use stop_reason.", q.Explanation);
        Assert.Equal("MD-CASE-1", q.ScenarioId);
    }

    [Fact] public void Unknown_field_is_a_format_error() =>
        Assert.Throws<FormatException>(() => MarkdownBank.Parse("## Question X-1\n- colour: blue\n"));

    [Fact] public void Pdf_import_reports_not_implemented()
    {
        var (file, errors) = ContentImporter.Parse("bank.pdf", new byte[] { 1, 2, 3 });
        Assert.Null(file);
        Assert.Contains(errors, e => e.Contains("not implemented"));
    }
}
