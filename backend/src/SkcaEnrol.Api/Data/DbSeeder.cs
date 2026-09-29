using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Data;

/// <summary>
/// Demo data for development and the marking demo. It only runs when the
/// Users table is empty, so restarting the API never duplicates anything.
/// Seeding in code (not EF HasData) because BCrypt hashes change every run.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, string demoPassword, ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            logger.LogInformation("Seed skipped: users already exist");
            return;
        }

        // Hash once and reuse: every demo account shares the same demo password.
        var hash = BCrypt.Net.BCrypt.HashPassword(demoPassword);

        var admin = new User { FullName = "Academy Admin", Email = "admin@skca.lk", Role = UserRole.Admin, PasswordHash = hash };
        var nimal = new User { FullName = "Nimal Perera", Email = "coach.nimal@skca.lk", Role = UserRole.Coach, PasswordHash = hash };
        var sanduni = new User { FullName = "Sanduni Silva", Email = "coach.sanduni@skca.lk", Role = UserRole.Coach, PasswordHash = hash };
        var kumari = new User { FullName = "Kumari Fernando", Email = "parent.kumari@skca.lk", Role = UserRole.Parent, PasswordHash = hash };
        var ruwan = new User { FullName = "Ruwan Jayasinghe", Email = "parent.ruwan@skca.lk", Role = UserRole.Parent, PasswordHash = hash };
        var dilani = new User { FullName = "Dilani Wickramasinghe", Email = "parent.dilani@skca.lk", Role = UserRole.Parent, PasswordHash = hash };
        db.Users.AddRange(admin, nimal, sanduni, kumari, ruwan, dilani);

        // Some children have real public Lichess accounts so the skill agent has live data to read.
        var kavindu = new Child { Parent = kumari, FullName = "Kavindu Fernando", DateOfBirth = new DateOnly(2017, 5, 10), LichessUsername = "thibault" };
        var sithmi = new Child { Parent = kumari, FullName = "Sithmi Fernando", DateOfBirth = new DateOnly(2019, 2, 20) };
        var tharindu = new Child { Parent = ruwan, FullName = "Tharindu Jayasinghe", DateOfBirth = new DateOnly(2013, 8, 1), LichessUsername = "penguingim1" };
        var nethmi = new Child { Parent = ruwan, FullName = "Nethmi Jayasinghe", DateOfBirth = new DateOnly(2016, 3, 14) };
        var ashen = new Child { Parent = dilani, FullName = "Ashen Wickramasinghe", DateOfBirth = new DateOnly(2014, 11, 15), LichessUsername = "Zhigalko_Sergei" };
        var dinuli = new Child { Parent = dilani, FullName = "Dinuli Wickramasinghe", DateOfBirth = new DateOnly(2018, 7, 3) };
        db.Children.AddRange(kavindu, sithmi, tharindu, nethmi, ashen, dinuli);

        ChessClass Class(string name, ClassLevel level, DayOfWeek day, string start, string end, int capacity, decimal fee, User coach, bool active = true) =>
            new() { Name = name, Level = level, DayOfWeek = day, StartTime = TimeOnly.Parse(start), EndTime = TimeOnly.Parse(end), Capacity = capacity, MonthlyFee = fee, Coach = coach, IsActive = active };

        var littleKnights = Class("Little Knights", ClassLevel.Beginner, DayOfWeek.Saturday, "09:00", "10:00", 10, 3500m, nimal);
        // Capacity 3 with 2 approved students below: one seat left, to demo the capacity rule.
        var pawnStars = Class("Pawn Stars", ClassLevel.Beginner, DayOfWeek.Saturday, "14:00", "15:00", 3, 3500m, nimal);
        var openingExplorers = Class("Opening Explorers", ClassLevel.Intermediate, DayOfWeek.Saturday, "15:30", "17:00", 8, 4500m, sanduni);
        var weekdayBeginners = Class("Weekday Beginners", ClassLevel.Beginner, DayOfWeek.Wednesday, "16:00", "17:00", 8, 3000m, sanduni);
        var tacticsLab = Class("Tactics Lab", ClassLevel.Intermediate, DayOfWeek.Sunday, "10:00", "11:30", 8, 4500m, nimal);
        var endgameMasters = Class("Endgame Masters", ClassLevel.Advanced, DayOfWeek.Sunday, "14:00", "16:00", 6, 6000m, sanduni);
        var tournamentPrep = Class("Tournament Prep", ClassLevel.Advanced, DayOfWeek.Friday, "17:00", "19:00", 6, 6500m, nimal, active: false);
        db.Classes.AddRange(littleKnights, pawnStars, openingExplorers, weekdayBeginners, tacticsLab, endgameMasters, tournamentPrep);

        // Already-approved placements, so seat counts, fees and the dashboard have data from day one.
        var thisMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        void Approved(Child child, ChessClass klass)
        {
            var enrolment = new Enrolment
            {
                Child = child,
                RequestedClass = klass,
                AssignedClass = klass,
                PreferredDays = new() { klass.DayOfWeek },
                Status = EnrolmentStatus.Approved
            };
            enrolment.History.Add(new EnrolmentStatusHistory { ToStatus = EnrolmentStatus.Submitted, ChangedAt = DateTime.UtcNow, Note = "Seed data" });
            enrolment.History.Add(new EnrolmentStatusHistory { FromStatus = EnrolmentStatus.Submitted, ToStatus = EnrolmentStatus.Approved, ChangedByUser = admin, ChangedAt = DateTime.UtcNow, Note = "Seed data" });
            // Each of these is the family's first placement, so no sibling discount applies.
            enrolment.FeeRecords.Add(new FeeRecord { Month = thisMonth, Amount = klass.MonthlyFee, SiblingDiscountApplied = false });
            db.Enrolments.Add(enrolment);
        }
        Approved(sithmi, pawnStars);
        Approved(dinuli, pawnStars);
        Approved(nethmi, weekdayBeginners);

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo data: {Users} users, {Classes} classes", 6, 7);
    }
}
