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
