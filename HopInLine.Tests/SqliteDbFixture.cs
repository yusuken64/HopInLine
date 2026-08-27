using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HopInLine.Tests;

/// <summary>
/// Provides a disposable SQLite in-memory fixture for testing.
/// Uses the SQLite provider with an in-memory connection string, not the EF InMemory provider,
/// because the InMemory provider does not enforce relational constraints or produce realistic ordering.
/// </summary>
public sealed class SqliteDbFixture : IDisposable, IDbContextFactory<ApplicationDbContext>
{
    private readonly SqliteConnection _connection;

    public SqliteDbFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();            // MUST stay open — closing it destroys the database

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the schema with EnsureCreated, not Migrate. The migrations target SQL Server
        // and contain raw T-SQL that SQLite cannot run.
        using (var context = new ApplicationDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        _options = options;
    }

    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ApplicationDbContext CreateContext() => new(_options);

    ApplicationDbContext IDbContextFactory<ApplicationDbContext>.CreateDbContext() => CreateContext();

    public void Dispose() => _connection.Dispose();
}
