using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SkcaEnrol.Api.Data;

/// <summary>
/// Used only by `dotnet ef` when creating migrations. Without it, the tool
/// would boot the whole API (which needs a JWT key and a real connection string).
/// `migrations add` never connects, so a placeholder connection string is fine.
/// `database update` uses the SKCA_DB env var if you run it against a real DB.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SKCA_DB")
                               ?? "Host=localhost;Database=skca_design_time";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        return new AppDbContext(options);
    }
}
