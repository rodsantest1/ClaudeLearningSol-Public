using Microsoft.Extensions.Configuration;
using StudyAI.Models;
using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// ClaudeQuizPlanner's two deterministic fail-closed paths (no API key, no
// history yet) don't need a network call, so they're covered directly here —
// same pattern as ClaudeQuizProvider_NoApiKey_FailsClosedInsteadOfThrowing in
// ProviderGradingContractTests. The live HTTP round trip and
// ClaudeRecommendationParser's own parsing logic are covered separately (the
// latter in ClaudeRecommendationParserTests, without needing HttpClient at all).
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%).
public class ClaudeQuizPlannerTests
{
    private static readonly IReadOnlyList<TopicPerformance> SampleHistory = new[]
    {
        new TopicPerformance("Databases", 1, 3),
    };

    [Fact]
    public async Task RecommendNextAsync_NoApiKey_FailsClosedInsteadOfThrowing()
    {
        var planner = new ClaudeQuizPlanner(new HttpClient(), new ConfigurationBuilder().Build());

        var result = await planner.RecommendNextAsync(SampleHistory);

        Assert.Equal("Databases", result.Topic);
        Assert.Contains("API key", result.Reason);
    }

    [Fact]
    public async Task RecommendNextAsync_NoHistoryYet_FailsClosedWithoutCallingApi()
    {
        // Checked before the API key in RecommendNextAsync, so an empty
        // config here proves this path doesn't depend on configuration —
        // it's true on the very first question of a session either way.
        var planner = new ClaudeQuizPlanner(new HttpClient(), new ConfigurationBuilder().Build());

        var result = await planner.RecommendNextAsync(Array.Empty<TopicPerformance>());

        Assert.Equal("C# Basics", result.Topic);
        Assert.Contains("Answer a question first", result.Reason);
    }

    // → Regression coverage for the reported bug: a student practicing at
    // Hard would see "Suggested next" recommend Medium anyway, because every
    // fail-closed fallback in RecommendNextAsync was hardcoded to "Medium"
    // regardless of what QuizSettings.DifficultyFilter was actually set to.
    [Fact]
    public async Task RecommendNextAsync_NoHistoryYet_WithHardBaseline_FallsBackToHardNotMedium()
    {
        var planner = new ClaudeQuizPlanner(new HttpClient(), new ConfigurationBuilder().Build());

        var result = await planner.RecommendNextAsync(Array.Empty<TopicPerformance>(), baselineDifficulty: "Hard");

        Assert.Equal("Hard", result.Difficulty);
    }

    [Fact]
    public async Task RecommendNextAsync_NoApiKey_WithHardBaseline_FallsBackToHardNotMedium()
    {
        var planner = new ClaudeQuizPlanner(new HttpClient(), new ConfigurationBuilder().Build());

        var result = await planner.RecommendNextAsync(SampleHistory, baselineDifficulty: "Hard");

        Assert.Equal("Hard", result.Difficulty);
    }

    [Fact]
    public async Task RecommendNextAsync_NoApiKey_WithMixedBaseline_StillFallsBackToMedium()
    {
        // "Mixed" means the student wants variety, not a fixed target — the
        // original gradual-progression-from-Medium fallback is still correct
        // here, unlike a specific Easy/Medium/Hard baseline.
        var planner = new ClaudeQuizPlanner(new HttpClient(), new ConfigurationBuilder().Build());

        var result = await planner.RecommendNextAsync(SampleHistory, baselineDifficulty: "Mixed");

        Assert.Equal("Medium", result.Difficulty);
    }

    [Fact]
    public async Task RecommendNextAsync_NoApiKey_WithNoBaseline_StillFallsBackToMedium()
    {
        // Omitting baselineDifficulty entirely (the pre-existing call shape)
        // must keep behaving exactly as it did before this parameter existed.
        var planner = new ClaudeQuizPlanner(new HttpClient(), new ConfigurationBuilder().Build());

        var result = await planner.RecommendNextAsync(SampleHistory);

        Assert.Equal("Medium", result.Difficulty);
    }

    #region BuildProgressionGuidance / ResolveFallbackDifficulty (pure)

    [Theory]
    [InlineData("Easy")]
    [InlineData("Medium")]
    [InlineData("Hard")]
    public void BuildProgressionGuidance_WithSpecificBaseline_RecommendsItByDefault(string baseline)
    {
        var guidance = ClaudeQuizPlanner.BuildProgressionGuidance(baseline);

        Assert.Contains($"set their practice difficulty to {baseline}", guidance);
        Assert.Contains($"Recommend {baseline} by default", guidance);
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Medium")]
    [InlineData("Hard")]
    public void BuildProgressionGuidance_WithSpecificBaseline_OnlyDropsWhenMissingMostRecentQuestions(string baseline)
    {
        // The actual bug fix: a single wrong answer used to be enough to
        // knock the recommendation off the student's chosen difficulty. Now
        // it takes a clear "missing most of their recent questions" signal
        // (visible in the correct/total counts already in the prompt), and
        // the guidance explicitly rules out reacting to one wrong answer.
        var guidance = ClaudeQuizPlanner.BuildProgressionGuidance(baseline);

        Assert.Contains("missing most of their recent questions", guidance);
        Assert.Contains("don't drop down just because of one wrong answer", guidance);
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Medium")]
    [InlineData("Hard")]
    public void BuildProgressionGuidance_WithSpecificBaseline_AllowsRecommendingHigher(string baseline)
    {
        // Still adaptive upward, not just a fixed floor — a student doing
        // very well at their configured difficulty can still be routed to
        // something harder.
        var guidance = ClaudeQuizPlanner.BuildProgressionGuidance(baseline);

        Assert.Contains($"Only recommend higher than {baseline} if they're doing very well", guidance);
    }

    [Theory]
    [InlineData("Mixed")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildProgressionGuidance_WithNoSpecificBaseline_UsesGradualProgression(string? baseline)
    {
        var guidance = ClaudeQuizPlanner.BuildProgressionGuidance(baseline);

        Assert.Contains("Progression should be gradual", guidance);
        Assert.DoesNotContain("set their practice difficulty", guidance);
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Medium")]
    [InlineData("Hard")]
    public void ResolveFallbackDifficulty_WithSpecificBaseline_ReturnsIt(string baseline)
    {
        Assert.Equal(baseline, ClaudeQuizPlanner.ResolveFallbackDifficulty(baseline));
    }

    [Theory]
    [InlineData("Mixed")]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveFallbackDifficulty_WithNoSpecificBaseline_ReturnsMedium(string? baseline)
    {
        Assert.Equal("Medium", ClaudeQuizPlanner.ResolveFallbackDifficulty(baseline));
    }

    #endregion
}
