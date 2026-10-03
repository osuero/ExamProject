namespace ExamPrep.Api.Data;

public static class Roles
{
    public const string Student = "Student";
    public const string Admin = "Admin";
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string Role { get; set; } = Roles.Student;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public class LoginToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NormalizedEmail { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public class OutboxEmail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class Certification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Level { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string Description { get; set; } = "";
    public string Disclaimer { get; set; } = "";
    public int SortOrder { get; set; }
    public List<Domain> Domains { get; set; } = new();
    public List<ExamProfile> Profiles { get; set; } = new();
    public List<VerifiedLanguage> Languages { get; set; } = new();
}

public class Domain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CertificationId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int BankTarget { get; set; }
    public int SortOrder { get; set; }
    public List<string> Objectives { get; set; } = new();
}

/// <summary>Language verified for a specific exam with evidence. Only verified locales can be published.</summary>
public class VerifiedLanguage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CertificationId { get; set; }
    public string Locale { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Evidence { get; set; } = "";
    public Guid VerifiedByUserId { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
}

/// <summary>Immutable versioned exam profile. A change creates a new version; attempts snapshot the version they used.</summary>
public class ExamProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CertificationId { get; set; }
    public int Version { get; set; }
    public bool IsCurrent { get; set; }
    public int QuestionCount { get; set; }
    /// <summary>Time allowed to answer, in minutes. Not the appointment duration and not an accommodation.</summary>
    public int ExamDurationMinutes { get; set; }
    public int? AppointmentDurationMinutes { get; set; }
    public List<string> AllowedQuestionTypes { get; set; } = new();
    public List<DomainWeight> DomainWeights { get; set; } = new();
    public decimal SimulatorPassPercent { get; set; } = 80m;
    public string ScoringPolicy { get; set; } = ScoringPolicies.OnePointExactSetNoPenalty;
    public string? OfficialScoreReference { get; set; }
    public string VerificationStatus { get; set; } = "provisional_secondary_source";
    public List<string> SourceIds { get; set; } = new();
    public string? BlueprintVersion { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class DomainWeight
{
    public string Code { get; set; } = "";
    public decimal WeightPercent { get; set; }
}

public static class ScoringPolicies
{
    public const string OnePointExactSetNoPenalty = "simulator_v1_one_point_exact_set_no_penalty";
}

public class Source
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string Kind { get; set; } = "";   // official_exam, provider, technical_doc, community, testimony
    public string Confidence { get; set; } = ""; // high, medium, low
    public string? PublishedAt { get; set; }
    public string? CheckedAt { get; set; }
    public string? Topic { get; set; }
    public string? Restrictions { get; set; }
    public string? Conflicts { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "reachable"; // reachable, blocked, unverified
}

public class Scenario
{
    public string Id { get; set; } = "";
    public Guid CertificationId { get; set; }
    public string Locale { get; set; } = "en";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

public static class QuestionStatuses
{
    public const string Draft = "draft";
    public const string TechnicalReview = "technical_review";
    public const string EditorialReview = "editorial_review";
    public const string Approved = "approved";
    public const string Published = "published";
    public const string Quarantined = "quarantined";
    public const string Retired = "retired";

    public static readonly string[] All = { Draft, TechnicalReview, EditorialReview, Approved, Published, Quarantined, Retired };
}

public static class QuestionTypes
{
    public const string Single = "single_choice";
    public const string Multiple = "multiple_response";
}

/// <summary>Stable question identity. Content lives in immutable versions.</summary>
public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; } = "";
    public Guid CertificationId { get; set; }
    public string FamilyId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public List<QuestionVersion> Versions { get; set; } = new();
}

public class QuestionVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }
    public int VersionNo { get; set; }
    public string DomainCode { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Locale { get; set; } = "en";
    public string? ScenarioId { get; set; }
    public string QuestionType { get; set; } = QuestionTypes.Single;
    public int SelectCount { get; set; } = 1;
    public string Stem { get; set; } = "";
    public List<QuestionOption> Options { get; set; } = new();
    public List<string> CorrectOptionIds { get; set; } = new();
    public string Explanation { get; set; } = "";
    public int Points { get; set; } = 1;
    public string Difficulty { get; set; } = "";
    public string DifficultyBasis { get; set; } = "editorial_estimate_not_calibrated";
    public List<string> SourceIds { get; set; } = new();
    public string? SourceCheckedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Originality { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string Status { get; set; } = QuestionStatuses.Draft;
    public string? StatusNote { get; set; }
    public Guid? ImportBatchId { get; set; }
    public string? Provenance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class QuestionOption
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string Rationale { get; set; } = "";
}

public static class AttemptModes
{
    public const string Practice = "practice";
    public const string Simulation = "simulation";
    public const string Custom = "custom";
    public static readonly string[] All = { Practice, Simulation, Custom };
}

public static class AttemptStatuses
{
    public const string InProgress = "in_progress";
    public const string Submitted = "submitted";
    public const string Expired = "expired";
}

public class Attempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CertificationId { get; set; }
    public string CertificationCode { get; set; } = "";
    public Guid ProfileId { get; set; }
    public int ProfileVersion { get; set; }
    public string Mode { get; set; } = AttemptModes.Practice;
    public bool IsPreview { get; set; }
    public bool FeedbackEnabled { get; set; }
    /// <summary>Irreversible: once true the attempt is assisted.</summary>
    public bool FeedbackExposed { get; set; }
    public bool StartedAsClean { get; set; }
    public string Locale { get; set; } = "en";
    public string Status { get; set; } = AttemptStatuses.InProgress;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? DeadlineAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string ProfileSnapshotJson { get; set; } = "{}";
    public decimal PassPercent { get; set; }
    public string ScoringPolicy { get; set; } = "";
    public int PointsEarned { get; set; }
    public int PointsMax { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int OmittedCount { get; set; }
    public decimal? Percent { get; set; }
    public bool? Passed { get; set; }
    public int? TimeUsedSeconds { get; set; }
    public List<AttemptItem> Items { get; set; } = new();

    public string Classification => FeedbackExposed ? "assisted" : "clean";
}

public class AttemptItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttemptId { get; set; }
    public int Position { get; set; }
    public Guid QuestionVersionId { get; set; }
    public QuestionVersion? QuestionVersion { get; set; }
    public string DomainCode { get; set; } = "";
    public string? ScenarioId { get; set; }
    public List<string> OptionOrder { get; set; } = new();
    public List<string> SelectedOptionIds { get; set; } = new();
    /// <summary>Answer that counts for the score. Frozen when feedback is revealed for this item.</summary>
    public List<string>? ScoredOptionIds { get; set; }
    /// <summary>Selections made after the solution was seen. Never scored.</summary>
    public List<string>? LearningOptionIds { get; set; }
    public bool Flagged { get; set; }
    public bool FeedbackRevealed { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public DateTimeOffset? RevealedAt { get; set; }
    public bool? IsCorrect { get; set; }
}

public class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string Format { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string CertificationCode { get; set; } = "";
    public int Created { get; set; }
    public int Unchanged { get; set; }
    public int NewVersions { get; set; }
    public int Rejected { get; set; }
    public string ReportJson { get; set; } = "{}";
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AuditEntry
{
    public long Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string? DataJson { get; set; }
    public DateTimeOffset At { get; set; }
}
