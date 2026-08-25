using Xunit;
using StudyAI.Services;

namespace StudyAI.Tests;

/// <summary>
/// Unit tests for RecentHistoryLogic — the pure per-topic history logic
/// behind Quiz.razor's avoid-list (RecentPromptsByTopic) and format history
/// (RecentFormatsByTopic). No Blazor component, no IJSRuntime, no
/// HttpClient — just dictionaries in, dictionaries out.
///
/// The cross-topic-isolation tests below (*_LeavesOtherTopicsUntouched,
/// AddToTopic_AcrossTwoTopics_KeepsThemIndependent) are the actual
/// regression coverage for the bug this class was extracted to fix: a
/// single flat list used to be shared across every topic, so playing a
/// newly-added topic (e.g. "Claude Certified Architect") fed prompts into
/// the SAME avoid-list Claude saw for every other topic, including "Claude
/// Certified Developer" — and a longer, less-relevant avoid-list measurably
/// pushes Claude toward longer, more-elaborate questions to differentiate
/// against it (see ClaudeQuizProvider.BuildAvoidClause's own comment).
/// </summary>
public class RecentHistoryLogicTests
{
    #region AddToTopic

    [Fact]
    public void AddToTopic_ToNewTopic_CreatesItsList()
    {
        var byTopic = new Dictionary<string, List<string>>();

        var result = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Developer", "Prompt 1", maxItems: 5);

        Assert.Equal(new[] { "Prompt 1" }, result["Claude Certified Developer"]);
    }

    [Fact]
    public void AddToTopic_ToExistingTopic_AppendsToItsList()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Prompt 1", "Prompt 2" }
        };

        var result = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Developer", "Prompt 3", maxItems: 5);

        Assert.Equal(new[] { "Prompt 1", "Prompt 2", "Prompt 3" }, result["Claude Certified Developer"]);
    }

    [Fact]
    public void AddToTopic_BeyondMaxItems_DropsOldestFirst()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Prompt 1", "Prompt 2", "Prompt 3" }
        };

        var result = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Developer", "Prompt 4", maxItems: 3);

        Assert.Equal(new[] { "Prompt 2", "Prompt 3", "Prompt 4" }, result["Claude Certified Developer"]);
    }

    // → The actual bug: adding a prompt to a NEW topic ("Claude Certified
    // Architect") must never appear in, or affect the length of, an
    // unrelated existing topic's ("Claude Certified Developer") list.
    [Fact]
    public void AddToTopic_ToOneTopic_LeavesOtherTopicsUntouched()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Dev Prompt 1" }
        };

        var result = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Architect", "Architect Prompt 1", maxItems: 5);

        Assert.Equal(new[] { "Dev Prompt 1" }, result["Claude Certified Developer"]);
        Assert.Equal(new[] { "Architect Prompt 1" }, result["Claude Certified Architect"]);
    }

    // → Same shape as AddToTopic_ToOneTopic_LeavesOtherTopicsUntouched but
    // simulating the actual reported sequence: several Architect questions
    // answered in a row, then check Developer's list is still exactly what
    // it was before Architect was ever touched.
    [Fact]
    public void AddToTopic_AcrossTwoTopics_KeepsThemIndependent()
    {
        var byTopic = new Dictionary<string, List<string>>();

        byTopic = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Developer", "Dev Prompt 1", maxItems: 5);
        byTopic = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Architect", "Architect Prompt 1", maxItems: 5);
        byTopic = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Architect", "Architect Prompt 2", maxItems: 5);
        byTopic = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Architect", "Architect Prompt 3", maxItems: 5);

        Assert.Equal(new[] { "Dev Prompt 1" }, byTopic["Claude Certified Developer"]);
        Assert.Equal(
            new[] { "Architect Prompt 1", "Architect Prompt 2", "Architect Prompt 3" },
            byTopic["Claude Certified Architect"]);
    }

    [Fact]
    public void AddToTopic_DoesNotMutateOriginalDictionaryOrLists()
    {
        var originalList = new List<string> { "Prompt 1" };
        var byTopic = new Dictionary<string, List<string>> { ["Claude Certified Developer"] = originalList };

        RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Developer", "Prompt 2", maxItems: 5);

        Assert.Equal(new[] { "Prompt 1" }, originalList);
        Assert.Single(byTopic["Claude Certified Developer"]);
    }

    [Fact]
    public void AddToTopic_WorksForBoolLists_SameAsStrings()
    {
        var byTopic = new Dictionary<string, List<bool>>
        {
            ["Claude Certified Developer"] = new() { true, true }
        };

        var result = RecentHistoryLogic.AddToTopic(byTopic, "Claude Certified Developer", false, maxItems: 5);

        Assert.Equal(new[] { true, true, false }, result["Claude Certified Developer"]);
    }

    #endregion

    #region RemoveTopic

    [Fact]
    public void RemoveTopic_WithExistingTopic_RemovesItsEntry()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Dev Prompt 1", "Dev Prompt 2" },
            ["Claude Certified Architect"] = new() { "Architect Prompt 1" }
        };

        var result = RecentHistoryLogic.RemoveTopic(byTopic, "Claude Certified Developer");

        Assert.False(result.ContainsKey("Claude Certified Developer"));
        Assert.Equal(new[] { "Architect Prompt 1" }, result["Claude Certified Architect"]);
    }

    [Fact]
    public void RemoveTopic_LeavesOtherTopicsUntouched()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Dev Prompt 1" },
            ["Claude Certified Architect"] = new() { "Architect Prompt 1", "Architect Prompt 2" }
        };

        var result = RecentHistoryLogic.RemoveTopic(byTopic, "Claude Certified Developer");

        Assert.Equal(new[] { "Architect Prompt 1", "Architect Prompt 2" }, result["Claude Certified Architect"]);
    }

    [Fact]
    public void RemoveTopic_WithNonExistentTopic_IsNoOp()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Dev Prompt 1" }
        };

        var result = RecentHistoryLogic.RemoveTopic(byTopic, "Nonexistent Topic");

        Assert.Equal(new[] { "Dev Prompt 1" }, result["Claude Certified Developer"]);
        Assert.Single(result);
    }

    [Fact]
    public void RemoveTopic_ThenForTopic_ReturnsEmptyList()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Dev Prompt 1", "Dev Prompt 2" }
        };

        var afterRemove = RecentHistoryLogic.RemoveTopic(byTopic, "Claude Certified Developer");
        var result = RecentHistoryLogic.ForTopic(afterRemove, "Claude Certified Developer");

        Assert.Empty(result);
    }

    [Fact]
    public void RemoveTopic_DoesNotMutateOriginalDictionary()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Dev Prompt 1" }
        };

        RecentHistoryLogic.RemoveTopic(byTopic, "Claude Certified Developer");

        Assert.True(byTopic.ContainsKey("Claude Certified Developer"));
    }

    #endregion

    #region ForTopic

    [Fact]
    public void ForTopic_WithExistingTopic_ReturnsItsList()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Developer"] = new() { "Prompt 1", "Prompt 2" },
            ["Claude Certified Architect"] = new() { "Architect Prompt 1" }
        };

        var result = RecentHistoryLogic.ForTopic(byTopic, "Claude Certified Developer");

        Assert.Equal(new[] { "Prompt 1", "Prompt 2" }, result);
    }

    [Fact]
    public void ForTopic_WithUnknownTopic_ReturnsEmptyListNotNull()
    {
        var byTopic = new Dictionary<string, List<string>>
        {
            ["Claude Certified Architect"] = new() { "Architect Prompt 1" }
        };

        var result = RecentHistoryLogic.ForTopic(byTopic, "Claude Certified Developer");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ForTopic_WithEmptyDictionary_ReturnsEmptyList()
    {
        var byTopic = new Dictionary<string, List<string>>();

        var result = RecentHistoryLogic.ForTopic(byTopic, "Claude Certified Developer");

        Assert.Empty(result);
    }

    #endregion
}
