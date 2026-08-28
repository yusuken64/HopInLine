using Bunit;
using Bunit.TestDoubles;
using HopInLine.Data.Line;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Component DI integration tests using bUnit's TestContext.
/// Tests that Index.razor, EditLine.razor, ViewLine.razor, and StartLine.razor
/// can render against a real IServiceProvider built the same way Program.cs builds it.
/// This is a regression test for the DI-resolution bug class fixed in INDEX-001/EDIT-001.
/// </summary>
public class ComponentDiTests : TestContext, IDisposable
{
    private readonly SqliteDbFixture _fixture;

    public ComponentDiTests()
    {
        _fixture = new SqliteDbFixture();

        // Register services to mirror HopInLine/Program.cs exactly
        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(_fixture);
        Services.AddScoped<ILineRepository, SQLLineRepository>();
        Services.AddScoped<ILineService, LineService>();
        Services.AddSingleton<ILineChangeNotifier, LineChangeNotifier>();
        Services.AddScoped<OverlayService>();

        // Set up JSInterop to no-op JS calls
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    void IDisposable.Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public void Index_Renders_Without_Throwing()
    {
        // Act: Render the Index component. This will throw if DI dependencies cannot be resolved
        // or if OnInitialized/OnInitializedAsync throws synchronously.
        var cut = Render<HopInLine.Pages.Index>();

        // Assert: Component rendered successfully without throwing
        Assert.NotNull(cut);
    }

    [Fact]
    public void EditLine_Renders_Without_Throwing()
    {
        // Arrange: Insert a test line into the fixture
        var testLine = TestData.Line(id: "EDIT-TEST-1", manageToken: "test-token");
        using (var context = _fixture.CreateContext())
        {
            context.Lines.Add(testLine);
            context.SaveChanges();
        }

        // Act: Render the EditLine component with the line ID and manage token
        var cut = Render<HopInLine.Pages.Line.EditLine>(parameters =>
            parameters
                .Add(p => p.LineId, testLine.Id)
                .Add(p => p.ManageToken, testLine.ManageToken));

        // Assert: Component rendered successfully and permission-denied message is NOT shown
        // (meaning the token validated and the edit form rendered instead of throwing)
        var markup = cut.Markup;
        Assert.DoesNotContain("You do not have permission to edit this line.", markup);
    }

    [Fact]
    public void ViewLine_Renders_Without_Throwing()
    {
        // Arrange: Insert a test line into the fixture
        var testLine = TestData.Line(id: "VIEW-TEST-1");
        using (var context = _fixture.CreateContext())
        {
            context.Lines.Add(testLine);
            context.SaveChanges();
        }

        // Act: Render the ViewLine component with only the line ID (no manage token)
        var cut = Render<HopInLine.Pages.Line.ViewLine>(parameters =>
            parameters.Add(p => p.LineId, testLine.Id));

        // Assert: Component rendered successfully without throwing
        Assert.NotNull(cut);
    }

    [Fact]
    public void StartLine_Renders_Without_Throwing()
    {
        // Act: Render the StartLine component. This will throw if DI dependencies cannot be resolved
        // or if OnInitialized/OnInitializedAsync throws synchronously.
        var cut = Render<HopInLine.Pages.Line.StartLine>();

        // Assert: Component rendered successfully without throwing
        Assert.NotNull(cut);
    }
}
