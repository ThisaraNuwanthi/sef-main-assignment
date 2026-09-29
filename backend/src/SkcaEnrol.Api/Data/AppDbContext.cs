using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<ChessClass> Classes => Set<ChessClass>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<FeeRecord> FeeRecords => Set<FeeRecord>();
    public DbSet<EnrolmentStatusHistory> EnrolmentStatusHistory => Set<EnrolmentStatusHistory>();

    // Agent workflow state
    public DbSet<AgentWorkflow> Workflows => Set<AgentWorkflow>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    public DbSet<ToolCall> ToolCalls => Set<ToolCall>();
    public DbSet<WorkflowValidationResult> ValidationResults => Set<WorkflowValidationResult>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(u => u.Email).IsUnique();
        });

        b.Entity<Child>(e =>
        {
            e.Property(c => c.FullName).HasMaxLength(100).IsRequired();
            e.Property(c => c.LichessUsername).HasMaxLength(30);
            e.Property(c => c.PhotoPath).HasMaxLength(200);
            // Deleting a parent account removes their children too.
            e.HasOne(c => c.Parent).WithMany(u => u.Children)
                .HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.ParentId);
        });

        b.Entity<ChessClass>(e =>
        {
            e.ToTable("Classes", t =>
            {
                // Rules the database itself guarantees, even if a bug slips past the API.
                t.HasCheckConstraint("CK_Classes_Capacity_Positive", "\"Capacity\" > 0");
                t.HasCheckConstraint("CK_Classes_MonthlyFee_NonNegative", "\"MonthlyFee\" >= 0");
                t.HasCheckConstraint("CK_Classes_EndAfterStart", "\"EndTime\" > \"StartTime\"");
            });
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Level).HasConversion<string>().HasMaxLength(20);
            // DayOfWeek stays an integer (0 = Sunday) so "sort by day" gives week order, not alphabetical.
            e.Property(c => c.MonthlyFee).HasPrecision(10, 2);
            // A coach with classes cannot be deleted by accident.
            e.HasOne(c => c.Coach).WithMany(u => u.CoachedClasses)
                .HasForeignKey(c => c.CoachId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => c.Name).IsUnique();
            // The placement search filters by level and day together.
            e.HasIndex(c => new { c.Level, c.DayOfWeek });
            e.HasIndex(c => c.CoachId);
        });

        b.Entity<Enrolment>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.ParentNotes).HasMaxLength(Enrolment.ParentNotesMaxLength);
            // Stored as a PostgreSQL integer[] column (0 = Sunday ... 6 = Saturday).
            e.Property(x => x.PreferredDays);
            e.HasOne(x => x.Child).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.ChildId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RequestedClass).WithMany()
                .HasForeignKey(x => x.RequestedClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedClass).WithMany(c => c.AssignedEnrolments)
                .HasForeignKey(x => x.AssignedClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ChildId);
            // Seat counting looks up approved enrolments per class.
            e.HasIndex(x => new { x.AssignedClassId, x.Status });
        });

        b.Entity<FeeRecord>(e =>
        {
            e.ToTable("FeeRecords", t => t.HasCheckConstraint("CK_FeeRecords_Amount_NonNegative", "\"Amount\" >= 0"));
            e.Property(f => f.Amount).HasPrecision(10, 2);
            e.Property(f => f.Status).HasConversion<string>().HasMaxLength(10);
            e.HasOne(f => f.Enrolment).WithMany(x => x.FeeRecords)
                .HasForeignKey(f => f.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            // One fee row per enrolment per month: approving twice cannot double-bill.
            e.HasIndex(f => new { f.EnrolmentId, f.Month }).IsUnique();
        });

        b.Entity<EnrolmentStatusHistory>(e =>
        {
            e.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(30);
            e.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(30);
            e.Property(h => h.Note).HasMaxLength(500);
            e.HasOne(h => h.Enrolment).WithMany(x => x.History)
                .HasForeignKey(h => h.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.ChangedByUser).WithMany()
                .HasForeignKey(h => h.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(h => h.EnrolmentId);
        });

        b.Entity<Enrolment>().Property(x => x.Version).IsRowVersion();

        // ---------- Agent workflow state ----------
        b.Entity<AgentWorkflow>(e =>
        {
            e.ToTable("AgentWorkflows");
            e.Property(w => w.Objective).HasMaxLength(1000).IsRequired();
            e.Property(w => w.PlanJson).HasColumnType("jsonb");
            e.Property(w => w.FinalOutcome).HasColumnType("jsonb");
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(w => w.FailureReason).HasMaxLength(1000);
            e.HasOne(w => w.Enrolment).WithMany(x => x.Workflows)
                .HasForeignKey(w => w.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(w => w.EnrolmentId);
            e.HasIndex(w => w.Status);
        });

        b.Entity<AgentStep>(e =>
        {
            e.Property(s => s.StepName).HasMaxLength(50);
            e.Property(s => s.AgentName).HasMaxLength(50);
            e.Property(s => s.InputJson).HasColumnType("jsonb");
            e.Property(s => s.OutputJson).HasColumnType("jsonb");
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Error).HasMaxLength(1000);
            e.HasOne(s => s.Workflow).WithMany(w => w.Steps)
                .HasForeignKey(s => s.WorkflowId).OnDelete(DeleteBehavior.Cascade);
            // Step numbers are unique inside a workflow.
            e.HasIndex(s => new { s.WorkflowId, s.StepNo }).IsUnique();
        });

        b.Entity<ToolCall>(e =>
        {
            e.Property(t => t.ToolName).HasMaxLength(50);
            e.Property(t => t.InputJson).HasColumnType("jsonb");
            e.Property(t => t.OutputJson).HasColumnType("jsonb");
            e.Property(t => t.Error).HasMaxLength(1000);
            e.HasOne(t => t.Step).WithMany(s => s.ToolCalls)
                .HasForeignKey(t => t.StepId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => t.StepId);
        });

        b.Entity<WorkflowValidationResult>(e =>
        {
            e.ToTable("ValidationResults");
            e.Property(v => v.RuleName).HasMaxLength(50);
            e.Property(v => v.Severity).HasConversion<string>().HasMaxLength(10);
            e.Property(v => v.Message).HasMaxLength(500);
            e.HasOne(v => v.Workflow).WithMany(w => w.ValidationResults)
                .HasForeignKey(v => v.WorkflowId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(v => v.WorkflowId);
        });

        b.Entity<ApprovalDecision>(e =>
        {
            e.Property(d => d.Decision).HasConversion<string>().HasMaxLength(30);
            e.Property(d => d.Note).HasMaxLength(500);
            e.HasOne(d => d.Workflow).WithMany(w => w.ApprovalDecisions)
                .HasForeignKey(d => d.WorkflowId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.AdminUser).WithMany()
                .HasForeignKey(d => d.AdminUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(d => d.WorkflowId);
        });
    }

    /// <summary>
    /// Stamps CreatedAt/UpdatedAt on every save, so audit fields are never forgotten.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
