using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HopInLine.Data;

/// <summary>
/// Same model as <see cref="ApplicationDbContext"/>, but a separate type so EF keeps SQLite
/// migrations (Migrations/Sqlite) apart from the SQL Server ones (Migrations).
/// The app still asks for IDbContextFactory&lt;ApplicationDbContext&gt;; see <see cref="SqliteDbContextFactory"/>.
/// </summary>
public class SqliteApplicationDbContext : ApplicationDbContext
{
    public SqliteApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}

/// <summary>Hands out the SQLite context wherever the app asks for an ApplicationDbContext.</summary>
public sealed class SqliteDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public SqliteDbContextFactory(DbContextOptions<ApplicationDbContext> options)
    {
        _options = options;
    }

    public ApplicationDbContext CreateDbContext() => new SqliteApplicationDbContext(_options);
}

/// <summary>Used by "dotnet ef migrations add ... --context SqliteApplicationDbContext".</summary>
public class SqliteApplicationDbContextDesignFactory : IDesignTimeDbContextFactory<SqliteApplicationDbContext>
{
    public SqliteApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=hopinline.db")
            .Options;
        return new SqliteApplicationDbContext(options);
    }
}
