using HopInLine.Data.Line;
using Xunit;

namespace HopInLine.Tests;

/// <summary>
/// Tests for IdGenerator.GenerateUniqueId, GenerateManageToken, and NormalizeId.
/// </summary>
public class IdGeneratorTests
{
    private const string AllowedAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// GenerateUniqueId must be exactly 8 characters from the allowed alphabet.
    /// It must not contain 0, O, 1, I, or L.
    /// </summary>
    [Fact]
    public void GenerateUniqueId_Is8CharsFromAllowedAlphabet()
    {
        var id = IdGenerator.GenerateUniqueId();

        // Assert length
        Assert.Equal(8, id.Length);

        // Assert all characters are in the allowed alphabet
        foreach (var c in id)
        {
            Assert.Contains(c, AllowedAlphabet);
        }

        // Explicitly assert that none of the ambiguous characters are present
        Assert.DoesNotContain('0', id);
        Assert.DoesNotContain('O', id);
        Assert.DoesNotContain('1', id);
        Assert.DoesNotContain('I', id);
        Assert.DoesNotContain('L', id);
    }

    /// <summary>
    /// GenerateUniqueId must produce distinct IDs across 1000 calls.
    /// The original used a shared static Random which is documented to return garbage under concurrent use.
    /// </summary>
    [Fact]
    public void GenerateUniqueId_IsDistinctAcross1000Calls()
    {
        var ids = new HashSet<string>();

        for (int i = 0; i < 1000; i++)
        {
            var id = IdGenerator.GenerateUniqueId();
            ids.Add(id);
        }

        // All 1000 IDs must be distinct
        Assert.Equal(1000, ids.Count);
    }

    /// <summary>
    /// GenerateManageToken must be URL-safe: no +, /, or = characters.
    /// Must be at least 22 characters (base64-encoded 16 bytes without padding is typically 22 chars).
    /// </summary>
    [Fact]
    public void GenerateManageToken_IsUrlSafe()
    {
        var token = IdGenerator.GenerateManageToken();

        // Assert minimum length
        Assert.True(token.Length >= 22, $"Token must be at least 22 chars, got {token.Length}");

        // Assert no forbidden URL characters
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    /// <summary>
    /// GenerateManageToken must produce distinct tokens across 1000 calls.
    /// </summary>
    [Fact]
    public void GenerateManageToken_IsDistinctAcross1000Calls()
    {
        var tokens = new HashSet<string>();

        for (int i = 0; i < 1000; i++)
        {
            var token = IdGenerator.GenerateManageToken();
            tokens.Add(token);
        }

        // All 1000 tokens must be distinct
        Assert.Equal(1000, tokens.Count);
    }

    /// <summary>
    /// NormalizeId trims whitespace and converts to uppercase.
    /// Normalizing an already-normalized id is a no-op.
    /// </summary>
    [Fact]
    public void NormalizeId_TrimsAndUppercases()
    {
        // Test trimming and uppercasing
        var normalized = IdGenerator.NormalizeId("  abcd1234  ");
        Assert.Equal("ABCD1234", normalized);

        // Test normalizing an already-normalized id is a no-op
        var idempotent = IdGenerator.NormalizeId(normalized);
        Assert.Equal("ABCD1234", idempotent);
    }

    /// <summary>
    /// NormalizeId returns empty string for null, empty, or whitespace-only input.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeId_ReturnsEmptyForNullOrWhitespace(string? input)
    {
        var normalized = IdGenerator.NormalizeId(input);
        Assert.Empty(normalized);
    }
}
