using Microsoft.Data.Sqlite;
using HopInLine.Data;
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

        // Apply the real SQLite migrations so the tests also catch a schema that drifted from the model.
        using (var context = new SqliteApplicationDbContext(options))
        {
            context.Database.Migrate();
        }

        _options = options;
    }

    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ApplicationDbContext CreateContext() => new SqliteApplicationDbContext(_options);

    ApplicationDbContext IDbContextFactory<ApplicationDbContext>.CreateDbContext() => CreateContext();

    public void Dispose() => _connection.Dispose();
}
