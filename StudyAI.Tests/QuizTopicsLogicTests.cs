using Xunit;
using StudyAI.Services;

namespace StudyAI.Tests;

/// <summary>
/// Unit tests for QuizTopicsLogic — the pure add/rename/remove logic behind
/// Home.razor's "Manage Topics" CRUD section. No Blazor component, no
/// HttpClient, no filesystem — just lists in, lists (or null) out.
/// </summary>
public class QuizTopicsLogicTests
{
    private static readonly List<string> Seed = new() { "C# Basics", "Web Fundamentals", "Databases" };

    #region AddTopic

    [Fact]
    public void AddTopic_WithNewName_AppendsIt()
    {
        var result = QuizTopicsLogic.AddTopic(Seed, "Security");

        Assert.NotNull(result);
        Assert.Equal(new[] { "C# Basics", "Web Fundamentals", "Databases", "Security" }, result);
    }

    [Fact]
    public void AddTopic_TrimsWhitespace()
    {
        var result = QuizTopicsLogic.AddTopic(Seed, "  Security  ");

        Assert.NotNull(result);
        Assert.Equal("Security", result[^1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddTopic_WithEmptyOrWhitespaceName_ReturnsNull(string? newTopic)
    {
        var result = QuizTopicsLogic.AddTopic(Seed, newTopic);

        Assert.Null(result);
    }

    [Fact]
    public void AddTopic_WithExactDuplicate_ReturnsNull()
    {
        var result = QuizTopicsLogic.AddTopic(Seed, "Databases");

        Assert.Null(result);
    }

    [Fact]
    public void AddTopic_WithCaseInsensitiveDuplicate_ReturnsNull()
    {
        var result = QuizTopicsLogic.AddTopic(Seed, "databases");

        Assert.Null(result);
    }

    [Fact]
    public void AddTopic_DoesNotMutateOriginalList()
    {
        var original = new List<string>(Seed);

        QuizTopicsLogic.AddTopic(original, "Security");

        Assert.Equal(Seed, original);
    }

    #endregion

    #region RemoveTopic

    [Fact]
    public void RemoveTopic_WithExistingName_RemovesIt()
    {
        var result = QuizTopicsLogic.RemoveTopic(Seed, "Web Fundamentals");

        Assert.Equal(new[] { "C# Basics", "Databases" }, result);
    }

    [Fact]
    public void RemoveTopic_WithNonExistentName_ReturnsListUnchanged()
    {
        var result = QuizTopicsLogic.RemoveTopic(Seed, "Nonexistent Topic");

        Assert.Equal(Seed, result);
    }

    [Fact]
    public void RemoveTopic_IsCaseSensitive()
    {
        // "databases" (lowercase) shouldn't remove "Databases" — exact match only,
        // unlike AddTopic's duplicate check which is deliberately case-insensitive.
        var result = QuizTopicsLogic.RemoveTopic(Seed, "databases");

        Assert.Equal(Seed, result);
    }

    [Fact]
    public void RemoveTopic_DoesNotMutateOriginalList()
    {
        var original = new List<string>(Seed);

        QuizTopicsLogic.RemoveTopic(original, "Databases");

        Assert.Equal(Seed, original);
    }

    #endregion

    #region RenameTopic

    [Fact]
    public void RenameTopic_WithValidNewName_RenamesInPlace()
    {
        var result = QuizTopicsLogic.RenameTopic(Seed, "Web Fundamentals", "Web Development");

        Assert.NotNull(result);
        Assert.Equal(new[] { "C# Basics", "Web Development", "Databases" }, result);
    }

    [Fact]
    public void RenameTopic_TrimsWhitespace()
    {
        var result = QuizTopicsLogic.RenameTopic(Seed, "Databases", "  SQL  ");

        Assert.NotNull(result);
        Assert.Contains("SQL", result);
    }

    [Fact]
    public void RenameTopic_ToSameNameDifferentCasing_Succeeds()
    {
        // Renaming an entry to a different casing of ITSELF isn't a collision.
        var result = QuizTopicsLogic.RenameTopic(Seed, "Databases", "databases");

        Assert.NotNull(result);
        Assert.Contains("databases", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RenameTopic_WithEmptyOrWhitespaceNewName_ReturnsNull(string? newName)
    {
        var result = QuizTopicsLogic.RenameTopic(Seed, "Databases", newName);

        Assert.Null(result);
    }

    [Fact]
    public void RenameTopic_WithNonExistentOldName_ReturnsNull()
    {
        var result = QuizTopicsLogic.RenameTopic(Seed, "Nonexistent Topic", "New Name");

        Assert.Null(result);
    }

    [Fact]
    public void RenameTopic_CollidingWithDifferentExistingTopic_ReturnsNull()
    {
        var result = QuizTopicsLogic.RenameTopic(Seed, "Databases", "C# Basics");

        Assert.Null(result);
    }

    [Fact]
    public void RenameTopic_CollidingCaseInsensitivelyWithDifferentTopic_ReturnsNull()
    {
        var result = QuizTopicsLogic.RenameTopic(Seed, "Databases", "c# basics");

        Assert.Null(result);
    }

    [Fact]
    public void RenameTopic_DoesNotMutateOriginalList()
    {
        var original = new List<string>(Seed);

        QuizTopicsLogic.RenameTopic(original, "Databases", "SQL");

        Assert.Equal(Seed, original);
    }

    #endregion
}
