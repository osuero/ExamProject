using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ExamPrep.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<LoginToken> LoginTokens => Set<LoginToken>();
    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<VerifiedLanguage> VerifiedLanguages => Set<VerifiedLanguage>();
    public DbSet<ExamProfile> ExamProfiles => Set<ExamProfile>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptItem> AttemptItems => Set<AttemptItem>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ValueComparer<List<T>> ListComparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
        v => JsonSerializer.Serialize(v, Json).GetHashCode(),
        v => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(v, Json), Json)!);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.NormalizedEmail).HasMaxLength(320);
            e.Property(x => x.Role).HasMaxLength(20);
        });
        b.Entity<LoginToken>(e =>
        {
            e.ToTable("login_tokens");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.NormalizedEmail, x.CreatedAt });
        });
        b.Entity<OutboxEmail>(e => e.ToTable("dev_outbox"));

        b.Entity<Certification>(e =>
        {
            e.ToTable("certifications");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Domains).WithOne().HasForeignKey(d => d.CertificationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Profiles).WithOne().HasForeignKey(d => d.CertificationId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Languages).WithOne().HasForeignKey(d => d.CertificationId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Domain>(e =>
        {
            e.ToTable("domains");
            e.HasIndex(x => new { x.CertificationId, x.Code }).IsUnique();
        });
        b.Entity<VerifiedLanguage>(e =>
        {
            e.ToTable("verified_languages");
            e.HasIndex(x => new { x.CertificationId, x.Locale }).IsUnique();
        });
        b.Entity<ExamProfile>(e =>
        {
            e.ToTable("exam_profiles");
            e.HasIndex(x => new { x.CertificationId, x.Version }).IsUnique();
            e.HasIndex(x => x.CertificationId).HasFilter("\"IsCurrent\" = true").IsUnique().HasDatabaseName("ux_exam_profiles_current");
            e.Property(x => x.DomainWeights).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, Json), v => JsonSerializer.Deserialize<List<DomainWeight>>(v, Json)!, ListComparer<DomainWeight>());
            e.Property(x => x.SimulatorPassPercent).HasPrecision(5, 2);
        });
        b.Entity<Source>(e => { e.ToTable("sources"); e.HasKey(x => x.Id); });
        b.Entity<Scenario>(e => { e.ToTable("scenarios"); e.HasKey(x => x.Id); });

        b.Entity<Question>(e =>
        {
            e.ToTable("questions");
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.HasIndex(x => x.FamilyId);
            e.HasMany(x => x.Versions).WithOne(v => v.Question).HasForeignKey(v => v.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<QuestionVersion>(e =>
        {
            e.ToTable("question_versions");
            e.HasIndex(x => new { x.QuestionId, x.VersionNo }).IsUnique();
            e.HasIndex(x => x.ContentHash);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Options).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, Json), v => JsonSerializer.Deserialize<List<QuestionOption>>(v, Json)!, ListComparer<QuestionOption>());
        });

        b.Entity<Attempt>(e =>
        {
            e.ToTable("attempts");
            e.HasIndex(x => new { x.UserId, x.StartedAt });
            e.HasIndex(x => new { x.Status, x.DeadlineAt });
            e.Property(x => x.ProfileSnapshotJson).HasColumnType("jsonb");
            e.Property(x => x.PassPercent).HasPrecision(5, 2);
            e.Property(x => x.Percent).HasPrecision(6, 2);
            e.Ignore(x => x.Classification);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.AttemptId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AttemptItem>(e =>
        {
            e.ToTable("attempt_items");
            e.HasIndex(x => new { x.AttemptId, x.Position }).IsUnique();
            e.HasOne(x => x.QuestionVersion).WithMany().HasForeignKey(x => x.QuestionVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ImportBatch>(e =>
        {
            e.ToTable("import_batches");
            e.Property(x => x.ReportJson).HasColumnType("jsonb");
        });
        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_log");
            e.Property(x => x.DataJson).HasColumnType("jsonb");
            e.HasIndex(x => x.At);
        });
    }
}
