using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Smoke tests proving the harness works, and nothing about app behavior.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void Fixture_CreatesSchema()
    {
        // Arrange: Create a fixture and insert a Line
        using var fixture = new SqliteDbFixture();
        var testLine = TestData.Line(id: "SMOKE-TEST-1");

        // Act: Insert in one context
        using (var context = fixture.CreateContext())
        {
            context.Lines.Add(testLine);
            context.SaveChanges();
        }

        // Assert: Read it back in a second context from the same fixture
        using (var context = fixture.CreateContext())
        {
            var retrieved = context.Lines.FirstOrDefault(l => l.Id == "SMOKE-TEST-1");
            Assert.NotNull(retrieved);
            Assert.Equal("SMOKE-TEST-1", retrieved.Id);
        }
    }

    [Fact]
    public void Fixture_IsIsolatedPerInstance()
    {
        // Arrange: Create two separate fixtures
        using var fixture1 = new SqliteDbFixture();
        using var fixture2 = new SqliteDbFixture();

        var line1 = TestData.Line(id: "ISOLATED-1");
        var line2 = TestData.Line(id: "ISOLATED-2");

        // Act: Insert into each fixture's database
        using (var context1 = fixture1.CreateContext())
        {
            context1.Lines.Add(line1);
            context1.SaveChanges();
        }

        using (var context2 = fixture2.CreateContext())
        {
            context2.Lines.Add(line2);
            context2.SaveChanges();
        }

        // Assert: Each fixture sees only its own data
        using (var context1 = fixture1.CreateContext())
        {
            var count = context1.Lines.Count();
            Assert.Equal(1, count);
            var retrieved = context1.Lines.FirstOrDefault(l => l.Id == "ISOLATED-1");
            Assert.NotNull(retrieved);
        }

        using (var context2 = fixture2.CreateContext())
        {
            var count = context2.Lines.Count();
            Assert.Equal(1, count);
            var retrieved = context2.Lines.FirstOrDefault(l => l.Id == "ISOLATED-2");
            Assert.NotNull(retrieved);
        }
    }
}
