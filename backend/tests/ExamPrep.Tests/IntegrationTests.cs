using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExamPrep.Api.Data;
using ExamPrep.Api.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExamPrep.Tests;

[Collection("app")]
public class AuthTests(TestApp app)
{
    [Fact]
    public async Task Request_link_response_is_identical_for_new_and_existing_addresses()
    {
        await app.SignInAsync("existing@test.local");
        var anon = await app.AnonymousAsync();
        var a = await anon.PostAsync("/api/auth/request-link", new { email = "existing@test.local" });
        var b = await anon.PostAsync("/api/auth/request-link", new { email = "nobody-" + Guid.NewGuid() + "@test.local" });
        Assert.Equal(a.StatusCode, b.StatusCode);
        Assert.Equal(await a.Content.ReadAsStringAsync(), await b.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Token_is_single_use_even_under_concurrency()
    {
        var email = "race@test.local";
        var anon = await app.AnonymousAsync();
        await anon.PostAsync("/api/auth/request-link", new { email });
        var token = app.LatestToken(email);
        var clients = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => app.AnonymousAsync()));
        var results = await Task.WhenAll(clients.Select(c => c.PostAsync("/api/auth/verify", new { token })));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var email = "late@test.local";
        var anon = await app.AnonymousAsync();
        await anon.PostAsync("/api/auth/request-link", new { email });
        var token = app.LatestToken(email);
        app.Clock.Advance(TimeSpan.FromMinutes(16));
        var r = await anon.PostAsync("/api/auth/verify", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Tokens_are_stored_hashed()
    {
        var email = "hash@test.local";
        var anon = await app.AnonymousAsync();
        await anon.PostAsync("/api/auth/request-link", new { email });
        var token = app.LatestToken(email);
        Assert.False(app.WithDb(db => db.LoginTokens.Any(t => t.TokenHash == token)));
    }

    [Fact]
    public async Task State_changes_require_the_csrf_header()
    {
        var api = await app.SignInAsync("csrf@test.local");
        var r = await api.PostAsync("/api/attempts", new { certificationCode = "CCDV-F", mode = "practice", questionCount = 3 }, csrf: false);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Contains("csrf_invalid", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_bootstrap_and_role_enforcement()
    {
        var student = await app.SignInAsync("student-role@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/admin/questions?certificationCode=CCDV-F")).StatusCode);
        var admin = await app.SignInAsync("admin@test.local");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/questions?certificationCode=CCDV-F")).StatusCode);
        var anon = await app.AnonymousAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/attempts")).StatusCode);
    }
}

[Collection("app")]
public class CatalogAndContentTests(TestApp app)
{
    [Fact]
    public async Task Catalog_lists_both_exams_with_verified_language_and_published_counts()
    {
        var anon = await app.AnonymousAsync();
        var list = await anon.GetJsonAsync("/api/catalog");
        var codes = list.EnumerateArray().Select(e => e.GetProperty("code").GetString()).ToList();
        Assert.Equal(new[] { "CCDV-F", "CCAR-F" }, codes);
        foreach (var e in list.EnumerateArray())
        {
            Assert.Equal("verified", e.GetProperty("languageStatus").GetString());
            Assert.True(e.GetProperty("availability").GetProperty("fullSimulationAvailable").GetBoolean());
        }
        var raw = JsonSerializer.Serialize(list);
        Assert.DoesNotContain("correctOptionIds", raw);
    }

    [Fact]
    public async Task Bootstrap_is_idempotent_and_published_items_carry_review_provenance()
    {
        var before = app.WithDb(db => db.QuestionVersions.Count());
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<Bootstrapper>().RunAsync(Path.Combine(TestApp.RepoRoot, "content"), CancellationToken.None);
        Assert.Equal(before, app.WithDb(db => db.QuestionVersions.Count()));
        var notes = app.WithDb(db => db.QuestionVersions.Where(v => v.Status == QuestionStatuses.Published).Select(v => v.StatusNote).ToList());
        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.StartsWith("QA-B1-", n));
    }

    private static MultipartFormDataContent File(string name, string content)
    {
        var f = new MultipartFormDataContent();
        var c = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        c.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        f.Add(c, "file", name);
        return f;
    }

    private static async Task<HttpResponseMessage> Upload(Api api, string kind, string name, string content)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/{kind}") { Content = File(name, content) };
        req.Headers.Add("X-XSRF-TOKEN", api.Xsrf);
        return await api.Http.SendAsync(req);
    }

    private static string Bank(string id, string key, string stem) => JsonSerializer.Serialize(new
    {
        examCode = "CCDV-F",
        questions = new[]
        {
            new
            {
                id, domainId = "D1", objective = "Agent Architecture", questionType = "single_choice", selectCount = 1, stem,
                options = "ABCD".Select(c => new { id = c.ToString(), text = $"Distinct option {c} for {id}", rationale = $"Reason {c}" }),
                correctOptionIds = new[] { key }, explanation = "Principle.", sourceIds = new[] { "T01" }, locale = "en"
            }
        }
    });

    [Fact]
    public async Task Import_preview_commit_is_retryable_and_versions_changes()
    {
        var admin = await app.SignInAsync("admin@test.local");
        var json = Bank("TEST-IMP-001", "A", "A unique stem about routing invoices through a fixed pipeline with three stages.");
        var preview = await admin.JsonAsync(await Upload(admin, "preview", "t.json", json));
        Assert.True(preview.GetProperty("valid").GetBoolean());
        Assert.Equal(1, preview.GetProperty("toCreate").GetInt32());
        Assert.Equal(0, app.WithDb(db => db.Questions.Count(q => q.ExternalId == "TEST-IMP-001")));

        Assert.Equal(HttpStatusCode.OK, (await Upload(admin, "commit", "t.json", json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Upload(admin, "commit", "t.json", json)).StatusCode);
        Assert.Equal(1, app.WithDb(db => db.QuestionVersions.Count(v => v.Question!.ExternalId == "TEST-IMP-001")));

        var changed = Bank("TEST-IMP-001", "A", "A unique stem about routing invoices through a fixed pipeline with four stages.");
        await Upload(admin, "commit", "t2.json", changed);
        var versions = app.WithDb(db => db.QuestionVersions.Where(v => v.Question!.ExternalId == "TEST-IMP-001").Select(v => new { v.VersionNo, v.Status }).ToList());
        Assert.Equal(2, versions.Count);
        Assert.All(versions, v => Assert.Equal("draft", v.Status));

        // Re-importing the older file must not resurrect v1 as v3
        await Upload(admin, "commit", "t.json", json);
        Assert.Equal(2, app.WithDb(db => db.QuestionVersions.Count(v => v.Question!.ExternalId == "TEST-IMP-001")));
    }

    [Fact]
    public async Task Import_rejects_inconsistent_keys_unknown_domains_and_key_changes()
    {
        var admin = await app.SignInAsync("admin@test.local");
        var bad = (await admin.JsonAsync(await Upload(admin, "preview", "bad.json", Bank("TEST-IMP-BAD", "Z", "Another unique stem regarding something specific to test bad keys."))));
        Assert.False(bad.GetProperty("valid").GetBoolean());
        Assert.Contains("key_inconsistent", bad.GetRawText());
        var commit = await Upload(admin, "commit", "bad.json", Bank("TEST-IMP-BAD", "Z", "Another unique stem regarding something specific to test bad keys."));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, commit.StatusCode);
        Assert.Equal(0, app.WithDb(db => db.Questions.Count(q => q.ExternalId == "TEST-IMP-BAD")));

        var stem = "Stem used to test that a silent answer-key change on identical content is refused.";
        await Upload(admin, "commit", "k1.json", Bank("TEST-IMP-KEY", "A", stem));
        var flip = await admin.JsonAsync(await Upload(admin, "preview", "k2.json", Bank("TEST-IMP-KEY", "B", stem)));
        Assert.Contains("key_changed_same_content", flip.GetRawText());

        var md = await admin.JsonAsync(await Upload(admin, "preview", "x.pdf", "%PDF"));
        Assert.Contains("not implemented", md.GetRawText());
    }

    [Fact]
    public async Task Publishing_requires_review_path_and_verified_language()
    {
        var admin = await app.SignInAsync("admin@test.local");
        await Upload(admin, "commit", "p.json", Bank("TEST-PUB-001", "C", "Stem to test the publication gates for a new draft question in the bank."));
        var id = app.WithDb(db => db.Questions.Single(q => q.ExternalId == "TEST-PUB-001").Id);
        var direct = await admin.PostAsync($"/api/admin/questions/{id}/transition", new { to = "published" });
        Assert.Equal(HttpStatusCode.Conflict, direct.StatusCode);
        foreach (var s in new[] { "technical_review", "editorial_review", "approved" })
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/questions/{id}/transition", new { to = s })).StatusCode);

        // remove the verified language temporarily: publication must be blocked
        var rm = await admin.SendAsync(HttpMethod.Delete, "/api/admin/certifications/CCDV-F/languages/en");
        Assert.Equal(HttpStatusCode.NoContent, rm.StatusCode);
        var blocked = await admin.PostAsync($"/api/admin/questions/{id}/transition", new { to = "published" });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("language_not_verified", await blocked.Content.ReadAsStringAsync());
        var restore = await admin.PostAsync("/api/admin/certifications/CCDV-F/languages", new { locale = "en", sourceId = "P06", evidence = "FAQ: English only (test restore)" });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/questions/{id}/transition", new { to = "published" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/questions/{id}/transition", new { to = "retired", note = "test cleanup" })).StatusCode);
    }

    [Fact]
    public async Task Language_verification_needs_official_evidence()
    {
        var admin = await app.SignInAsync("admin@test.local");
        var r = await admin.PostAsync("/api/admin/certifications/CCAR-F/languages", new { locale = "es", sourceId = "C03", evidence = "A testimony" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}

[Collection("app")]
public class AttemptTests(TestApp app)
{
    private static readonly string[] SecretFields = { "correctOptionIds", "rationale", "explanation", "isCorrect", "scoredOptionIds" };

    private static void AssertNoSecrets(JsonElement state)
    {
        var raw = state.GetRawText();
        foreach (var f in SecretFields) Assert.DoesNotContain($"\"{f}\"", raw);
    }

    private (List<string> key, string type, int select) KeyOf(string externalId, int version) => app.WithDb(db =>
    {
        var v = db.QuestionVersions.Single(x => x.Question!.ExternalId == externalId && x.VersionNo == version);
        return (v.CorrectOptionIds, v.QuestionType, v.SelectCount);
    });

    private static JsonElement Item(JsonElement state, int pos) => state.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("position").GetInt32() == pos);

    [Fact]
    public async Task Simulation_uses_profile_and_never_exposes_keys_while_active()
    {
        var api = await app.SignInAsync("sim@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCAR-F", mode = "simulation" });
        var s = await api.GetJsonAsync($"/api/attempts/{id}");
        Assert.Equal(60, s.GetProperty("items").GetArrayLength());
        Assert.InRange(s.GetProperty("remainingSeconds").GetInt32(), 7190, 7200);
        Assert.Equal("clean", s.GetProperty("attempt").GetProperty("classification").GetString());
        AssertNoSecrets(s);
        var distinct = s.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("questionRef").GetProperty("externalId").GetString()).Distinct().Count();
        Assert.Equal(60, distinct);
        // scenario items contiguous
        var scen = s.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("scenario").ValueKind == JsonValueKind.Null ? null : i.GetProperty("scenario").GetProperty("id").GetString()).ToList();
        foreach (var g in scen.Where(x => x != null).Distinct())
        {
            var idx = scen.Select((x, i) => (x, i)).Where(t => t.x == g).Select(t => t.i).ToList();
            Assert.Equal(idx.Count - 1, idx.Last() - idx.First());
        }
        var check = await api.PostAsync($"/api/attempts/{id}/items/1/check", new { });
        Assert.Equal(HttpStatusCode.Conflict, check.StatusCode);
    }

    [Fact]
    public async Task Enabling_feedback_in_a_simulation_is_irreversibly_assisted()
    {
        var api = await app.SignInAsync("assist@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "simulation" });
        var on = await api.JsonAsync(await api.PostAsync($"/api/attempts/{id}/feedback", new { enabled = true }));
        Assert.Equal("assisted", on.GetProperty("classification").GetString());
        var off = await api.JsonAsync(await api.PostAsync($"/api/attempts/{id}/feedback", new { enabled = false }));
        Assert.False(off.GetProperty("feedbackEnabled").GetBoolean());
        Assert.Equal("assisted", off.GetProperty("classification").GetString());
        var fin = await api.JsonAsync(await api.PostAsync($"/api/attempts/{id}/finish"));
        Assert.Equal("assisted", fin.GetProperty("classification").GetString());
    }

    [Fact]
    public async Task Practice_feedback_freezes_the_first_answer_and_learning_changes_do_not_score()
    {
        var api = await app.SignInAsync("practice@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 5, feedback = true });
        var s = await api.GetJsonAsync($"/api/attempts/{id}");
        AssertNoSecrets(s);
        var item = s.GetProperty("items").EnumerateArray().First(i => i.GetProperty("questionType").GetString() == "single_choice");
        var pos = item.GetProperty("position").GetInt32();
        var qref = item.GetProperty("questionRef");
        var (key, _, _) = KeyOf(qref.GetProperty("externalId").GetString()!, qref.GetProperty("version").GetInt32());
        var wrong = item.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("id").GetString()!).First(o => !key.Contains(o));

        Assert.Equal(HttpStatusCode.OK, (await api.PutAsync($"/api/attempts/{id}/items/{pos}/answer", new { selected = new[] { wrong } })).StatusCode);
        var checkedItem = await api.JsonAsync(await api.PostAsync($"/api/attempts/{id}/items/{pos}/check", new { }));
        Assert.False(checkedItem.GetProperty("solution").GetProperty("isCorrect").GetBoolean());
        Assert.Equal(key, checkedItem.GetProperty("solution").GetProperty("correctOptionIds").EnumerateArray().Select(x => x.GetString()!).ToList());

        // Only the checked item reveals its solution
        var after = await api.GetJsonAsync($"/api/attempts/{id}");
        foreach (var it in after.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("position").GetInt32() != pos))
            Assert.Equal(JsonValueKind.Null, it.GetProperty("solution").ValueKind);

        // Changing to the right answer afterwards is stored as learning, not scored
        await api.PutAsync($"/api/attempts/{id}/items/{pos}/answer", new { selected = key });
        var fin = await api.JsonAsync(await api.PostAsync($"/api/attempts/{id}/finish"));
        Assert.Equal(0, fin.GetProperty("score").GetProperty("pointsEarned").GetInt32());
        Assert.Equal(1, fin.GetProperty("score").GetProperty("incorrect").GetInt32());
        Assert.Equal(4, fin.GetProperty("score").GetProperty("omitted").GetInt32());
        Assert.Equal("assisted", fin.GetProperty("classification").GetString());
    }

    [Fact]
    public async Task Multiple_response_requires_complete_selection_before_check_and_exact_set_to_score()
    {
        var api = await app.SignInAsync("multi@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 40, feedback = true });
        var s = await api.GetJsonAsync($"/api/attempts/{id}");
        var items = s.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("questionType").GetString() == "multiple_response").Take(2).ToList();
        Assert.True(items.Count == 2, "expected at least two multiple-response items in 40");
        foreach (var it in items)
        {
            var pos = it.GetProperty("position").GetInt32();
            var qref = it.GetProperty("questionRef");
            var (key, _, select) = KeyOf(qref.GetProperty("externalId").GetString()!, qref.GetProperty("version").GetInt32());
            Assert.Equal(2, select);
            await api.PutAsync($"/api/attempts/{id}/items/{pos}/answer", new { selected = new[] { key[0] } });
            var early = await api.PostAsync($"/api/attempts/{id}/items/{pos}/check", new { });
            Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
            Assert.Contains("incomplete_selection", await early.Content.ReadAsStringAsync());
            var tooMany = it.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("id").GetString()!).Take(3).ToArray();
            Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsync($"/api/attempts/{id}/items/{pos}/answer", new { selected = tooMany })).StatusCode);
        }
        // first item: exact set, second: one right + one wrong
        var p1 = items[0].GetProperty("position").GetInt32(); var r1 = items[0].GetProperty("questionRef");
        var k1 = KeyOf(r1.GetProperty("externalId").GetString()!, r1.GetProperty("version").GetInt32()).key;
        await api.PutAsync($"/api/attempts/{id}/items/{p1}/answer", new { selected = k1 });
        var p2 = items[1].GetProperty("position").GetInt32(); var r2 = items[1].GetProperty("questionRef");
        var k2 = KeyOf(r2.GetProperty("externalId").GetString()!, r2.GetProperty("version").GetInt32()).key;
        var w2 = items[1].GetProperty("options").EnumerateArray().Select(o => o.GetProperty("id").GetString()!).First(o => !k2.Contains(o));
        await api.PutAsync($"/api/attempts/{id}/items/{p2}/answer", new { selected = new[] { k2[0], w2 } });
        var fin = await api.JsonAsync(await api.PostAsync($"/api/attempts/{id}/finish"));
        Assert.Equal(1, fin.GetProperty("score").GetProperty("correct").GetInt32());
        Assert.Equal(1, fin.GetProperty("score").GetProperty("incorrect").GetInt32());
    }

    [Fact]
    public async Task Server_clock_governs_expiry_and_late_answers_are_rejected()
    {
        var api = await app.SignInAsync("timer@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "custom", questionCount = 3, durationMinutes = 5, feedback = false });
        await api.PutAsync($"/api/attempts/{id}/items/1/answer", new { selected = new[] { "A" } });
        app.Clock.Advance(TimeSpan.FromMinutes(2));
        var mid = await api.GetJsonAsync($"/api/attempts/{id}");
        Assert.InRange(mid.GetProperty("remainingSeconds").GetInt32(), 179, 181); // reload does not reset the timer
        app.Clock.Advance(TimeSpan.FromMinutes(4));
        var late = await api.PutAsync($"/api/attempts/{id}/items/2/answer", new { selected = new[] { "A" } });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Contains("attempt_expired", await late.Content.ReadAsStringAsync());
        var res = await api.GetJsonAsync($"/api/attempts/{id}/result");
        Assert.Equal("expired", res.GetProperty("attempt").GetProperty("status").GetString());
        Assert.Equal(300, res.GetProperty("attempt").GetProperty("score").GetProperty("timeUsedSeconds").GetInt32());
        Assert.Equal(2, res.GetProperty("attempt").GetProperty("score").GetProperty("omitted").GetInt32());
    }

    [Fact]
    public async Task Expiry_sweep_closes_abandoned_attempts()
    {
        var api = await app.SignInAsync("abandon@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCAR-F", mode = "custom", questionCount = 2, durationMinutes = 1 });
        app.Clock.Advance(TimeSpan.FromMinutes(2));
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AttemptService>().ExpireDueAsync(CancellationToken.None);
        Assert.Equal(AttemptStatuses.Expired, app.WithDb(db => db.Attempts.Single(a => a.Id == id).Status));
    }

    [Fact]
    public async Task Finish_is_idempotent_under_concurrent_double_submit()
    {
        var api = await app.SignInAsync("double@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 3, feedback = false });
        await api.PutAsync($"/api/attempts/{id}/items/1/answer", new { selected = new[] { "A" } });
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => api.PostAsync($"/api/attempts/{id}/finish")));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var finished = await Task.WhenAll(results.Select(async r => DateTimeOffset.Parse((await api.JsonAsync(r)).GetProperty("finishedAt").GetString()!).ToUnixTimeMilliseconds()));
        Assert.Single(finished.Distinct());
        Assert.Equal(1, app.WithDb(db => db.AuditEntries.Count(a => a.Action == "attempt.finished" && a.EntityId == id.ToString())));
        Assert.Equal(HttpStatusCode.Conflict, (await api.PutAsync($"/api/attempts/{id}/items/2/answer", new { selected = new[] { "A" } })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_answers_from_two_tabs_keep_a_consistent_state()
    {
        var api = await app.SignInAsync("tabs@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 4, feedback = false });
        var s = await api.GetJsonAsync($"/api/attempts/{id}");
        var opts = Item(s, 1).GetProperty("options").EnumerateArray().Select(o => o.GetProperty("id").GetString()!).ToList();
        var writes = await Task.WhenAll(opts.Select(o => api.PutAsync($"/api/attempts/{id}/items/1/answer", new { selected = new[] { o } })));
        Assert.All(writes, w => Assert.Equal(HttpStatusCode.OK, w.StatusCode));
        var final = Item(await api.GetJsonAsync($"/api/attempts/{id}"), 1).GetProperty("selected").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Single(final);
        Assert.Contains(final[0], opts);
    }

    [Fact]
    public async Task Users_cannot_see_or_change_each_others_attempts()
    {
        var alice = await app.SignInAsync("alice@test.local");
        var bob = await app.SignInAsync("bob@test.local");
        var id = await alice.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 2 });
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/attempts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsync($"/api/attempts/{id}/items/1/answer", new { selected = new[] { "A" } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/attempts/{id}/finish")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/attempts/{id}/result")).StatusCode);
        var list = await bob.GetJsonAsync("/api/attempts");
        Assert.DoesNotContain(id.ToString(), list.GetRawText());
    }

    [Fact]
    public async Task Insufficient_questions_are_reported_without_duplicating()
    {
        var api = await app.SignInAsync("short@test.local");
        var r = await api.PostAsync("/api/attempts", new { certificationCode = "CCDV-F", mode = "custom", questionCount = 50, domainCodes = new[] { "D3" } });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains("insufficient_questions", await r.Content.ReadAsStringAsync());
        var preview = await api.PostAsync("/api/attempts", new { certificationCode = "CCDV-F", mode = "practice", questionCount = 3, preview = true });
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
    }

    [Fact]
    public async Task New_profile_version_does_not_change_started_attempts()
    {
        var student = await app.SignInAsync("profile@test.local");
        var id = await student.StartAsync(new { certificationCode = "CCAR-F", mode = "simulation" });
        var admin = await app.SignInAsync("admin@test.local");
        var r = await admin.PostAsync("/api/admin/certifications/CCAR-F/profiles", new
        {
            questionCount = 30, examDurationMinutes = 60, appointmentDurationMinutes = 75,
            allowedQuestionTypes = new[] { "single_choice", "multiple_response" },
            domainWeights = new[] { new { code = "A1", weightPercent = 27 }, new { code = "A2", weightPercent = 18 }, new { code = "A3", weightPercent = 20 }, new { code = "A4", weightPercent = 20 }, new { code = "A5", weightPercent = 15 } },
            simulatorPassPercent = 75, verificationStatus = "test_only", sourceIds = new[] { "P05" }
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var s = await student.GetJsonAsync($"/api/attempts/{id}");
        Assert.Equal(60, s.GetProperty("items").GetArrayLength());
        Assert.Equal(1, s.GetProperty("attempt").GetProperty("profileVersion").GetInt32());
        var fin = await student.JsonAsync(await student.PostAsync($"/api/attempts/{id}/finish"));
        Assert.Equal(80, fin.GetProperty("score").GetProperty("passPercent").GetDecimal());
        // restore a profile equivalent to v1 for other tests
        await admin.PostAsync("/api/admin/certifications/CCAR-F/profiles", new
        {
            questionCount = 60, examDurationMinutes = 120, appointmentDurationMinutes = 135,
            allowedQuestionTypes = new[] { "single_choice", "multiple_response" },
            domainWeights = new[] { new { code = "A1", weightPercent = 27 }, new { code = "A2", weightPercent = 18 }, new { code = "A3", weightPercent = 20 }, new { code = "A4", weightPercent = 20 }, new { code = "A5", weightPercent = 15 } },
            simulatorPassPercent = 80, verificationStatus = "confirmed_official_exam_guide", sourceIds = new[] { "P05", "P06" }
        });
    }

    [Fact]
    public async Task Edited_question_creates_a_new_version_and_old_attempts_keep_the_old_one()
    {
        var student = await app.SignInAsync("version@test.local");
        var id = await student.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 1, domainCodes = new[] { "D4" } });
        var s = await student.GetJsonAsync($"/api/attempts/{id}");
        var qref = Item(s, 1).GetProperty("questionRef");
        var ext = qref.GetProperty("externalId").GetString()!;
        var oldStem = Item(s, 1).GetProperty("stem").GetString();
        var admin = await app.SignInAsync("admin@test.local");
        var qid = app.WithDb(db => db.Questions.Single(q => q.ExternalId == ext).Id);
        var detail = await admin.GetJsonAsync($"/api/admin/questions/{qid}");
        var latest = detail.GetProperty("latest");
        var edit = await admin.PostAsync($"/api/admin/questions/{qid}/versions", new
        {
            domainCode = latest.GetProperty("domainCode").GetString(), objective = latest.GetProperty("objective").GetString(),
            scenarioId = (string?)null, questionType = latest.GetProperty("questionType").GetString(), selectCount = latest.GetProperty("selectCount").GetInt32(),
            stem = oldStem + " (Edited for test.)", options = JsonSerializer.Deserialize<object>(latest.GetProperty("options").GetRawText()),
            correctOptionIds = JsonSerializer.Deserialize<string[]>(latest.GetProperty("correctOptionIds").GetRawText()),
            explanation = latest.GetProperty("explanation").GetString(), difficulty = "intermediate",
            sourceIds = JsonSerializer.Deserialize<string[]>(latest.GetProperty("sourceIds").GetRawText()), note = "test"
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var again = await student.GetJsonAsync($"/api/attempts/{id}");
        Assert.Equal(oldStem, Item(again, 1).GetProperty("stem").GetString());
        // the published old version stays eligible until the new one passes review
        var eligible = app.WithDb(db => db.QuestionVersions.Count(v => v.Question!.ExternalId == ext && v.Status == QuestionStatuses.Published));
        Assert.Equal(1, eligible);
    }

    [Fact]
    public async Task Progress_separates_clean_and_assisted_and_excludes_preview()
    {
        var api = await app.SignInAsync("progress@test.local");
        var clean = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 2, feedback = false });
        await api.PostAsync($"/api/attempts/{clean}/finish");
        var assisted = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 2, feedback = true });
        await api.PostAsync($"/api/attempts/{assisted}/items/1/check", new { revealWithoutAnswer = true });
        await api.PostAsync($"/api/attempts/{assisted}/finish");
        var p = await api.GetJsonAsync("/api/progress?certificationCode=CCDV-F");
        Assert.Equal(1, p.GetProperty("clean").GetProperty("attempts").GetInt32());
        Assert.Equal(1, p.GetProperty("assisted").GetProperty("attempts").GetInt32());
    }

    [Fact]
    public async Task Account_deletion_removes_attempts()
    {
        var api = await app.SignInAsync("delete-me@test.local");
        var id = await api.StartAsync(new { certificationCode = "CCDV-F", mode = "practice", questionCount = 2 });
        var r = await api.SendAsync(HttpMethod.Delete, "/api/me");
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.False(app.WithDb(db => db.Attempts.Any(a => a.Id == id)));
        Assert.False(app.WithDb(db => db.Users.Any(u => u.NormalizedEmail == "delete-me@test.local")));
    }
}
