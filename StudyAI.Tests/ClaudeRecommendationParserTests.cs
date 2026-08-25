using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeRecommendationParser — the piece of ClaudeQuizPlanner.RecommendNextAsync
// that turns a raw Messages API response body into a NextStepRecommendation.
// No network, no mocking: same shape as ClaudeGradeParserTests, applied to the
// recommend_next tool instead of submit_grade.
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%).
public class ClaudeRecommendationParserTests
{
    private const string FallbackTopic = "C# Basics";

    [Fact]
    public void Parse_CompleteToolUse_ReturnsRecommendation()
    {
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"recommend_next\"," +
            "\"input\":{\"topic\":\"Databases\",\"difficulty\":\"Hard\"," +
            "\"reason\":\"You've missed the last two Databases questions.\"}}]}";

        var result = ClaudeRecommendationParser.Parse(body, FallbackTopic);

        Assert.Equal("Databases", result.Topic);
        Assert.Equal("Hard", result.Difficulty);
        Assert.Equal("You've missed the last two Databases questions.", result.Reason);
    }

    [Fact]
    public void Parse_TruncatedBeforeReason_FallsBackToGenericReason()
    {
        // Simulates hitting max_tokens after topic/difficulty finished but
        // before reason was written. Still valid JSON, just missing a field.
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"recommend_next\"," +
            "\"input\":{\"topic\":\"Databases\",\"difficulty\":\"Hard\"}}]}";

        var result = ClaudeRecommendationParser.Parse(body, FallbackTopic);

        Assert.Equal("Databases", result.Topic);
        Assert.Equal("Hard", result.Difficulty);
        Assert.Equal("Recommended based on your performance so far.", result.Reason);
    }

    [Fact]
    public void Parse_TruncatedBeforeTopic_FailsClosedToFallbackTopicAndMediumDifficulty()
    {
        // A more severe truncation: the tool call started but no fields
        // finished writing at all before the cap hit.
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"recommend_next\",\"input\":{}}]}";

        var result = ClaudeRecommendationParser.Parse(body, FallbackTopic);

        Assert.Equal(FallbackTopic, result.Topic);
        Assert.Equal("Medium", result.Difficulty);
    }

    [Fact]
    public void Parse_ResponseCutOffMidDocument_FailsClosedToFallbackTopic()
    {
        // Worst case: the HTTP response body itself is incomplete JSON.
        const string body = "{\"content\":[{\"type\":\"tool_use\",\"name\":\"recommend_next\",\"input\":{\"topic\":";

        var result = ClaudeRecommendationParser.Parse(body, FallbackTopic);

        Assert.Equal(FallbackTopic, result.Topic);
        Assert.Equal("Medium", result.Difficulty);
    }

    [Fact]
    public void Parse_NoToolUseBlock_FailsClosedToFallbackTopic()
    {
        // Claude replied with plain text instead of calling the tool —
        // shouldn't happen with tool_choice forced, but the code doesn't
        // assume that holds.
        const string body = "{\"content\":[{\"type\":\"text\",\"text\":\"I'm not sure.\"}]}";

        var result = ClaudeRecommendationParser.Parse(body, FallbackTopic);

        Assert.Equal(FallbackTopic, result.Topic);
        Assert.Equal("Medium", result.Difficulty);
    }
}
