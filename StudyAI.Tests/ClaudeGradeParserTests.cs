using StudyAI.Models;
using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeGradeParser — the piece of GradeAsync that turns a raw Messages
// API response body into a GradeResult. No network, no mocking: just canned
// response bodies in, GradeResult out. Includes the max_tokens truncation
// scenarios discussed while reviewing iteration 3 — these test whether the code
// does the *right* thing (fail closed, don't silently mis-score), not just
// whether it avoids throwing.
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%).
public class ClaudeGradeParserTests
{
    private const string FallbackExplanation = "Reference explanation from the question.";

    [Fact]
    public void Parse_CompleteToolUse_ReturnsGrade()
    {
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"submit_grade\"," +
            "\"input\":{\"isCorrect\":true,\"explanation\":\"Matches the reference answer.\"}}]}";

        var result = ClaudeGradeParser.Parse(body, FallbackExplanation);

        Assert.True(result.IsCorrect);
        Assert.Equal("Matches the reference answer.", result.Explanation);
        Assert.Equal(GradeOutcome.Correct, result.Outcome);
    }

    [Fact]
    public void Parse_TruncatedBeforeExplanation_FallsBackToQuestionExplanation()
    {
        // Simulates hitting max_tokens right after "isCorrect" finished but
        // before "explanation" was written. Still valid JSON, just missing a
        // field — must not throw, and must not lose the grading result.
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"submit_grade\",\"input\":{\"isCorrect\":false}}]}";

        var result = ClaudeGradeParser.Parse(body, FallbackExplanation);

        Assert.False(result.IsCorrect);
        Assert.Equal(FallbackExplanation, result.Explanation);
        // isCorrect parsed fine — this is a real (negative) grade, not a
        // grading failure, even though the explanation field was cut off.
        Assert.Equal(GradeOutcome.Incorrect, result.Outcome);
    }

    [Fact]
    public void Parse_TruncatedBeforeIsCorrect_FailsClosedInsteadOfThrowing()
    {
        // A more severe truncation: the tool call started but no fields
        // finished writing at all before the cap hit.
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"submit_grade\",\"input\":{}}]}";

        var result = ClaudeGradeParser.Parse(body, FallbackExplanation);

        Assert.False(result.IsCorrect);
        Assert.Contains("Grading failed", result.Explanation);
        // isCorrect never finished writing — this must not be trusted as a
        // real "incorrect" grade, or it silently costs the user a point for
        // a parsing failure that isn't their fault.
        Assert.Equal(GradeOutcome.GradingFailed, result.Outcome);
    }

    [Fact]
    public void Parse_ResponseCutOffMidDocument_FailsClosedInsteadOfThrowing()
    {
        // Worst case: the HTTP response body itself is incomplete JSON.
        const string body = "{\"content\":[{\"type\":\"tool_use\",\"name\":\"submit_grade\",\"input\":{\"isCorrect\":";

        var result = ClaudeGradeParser.Parse(body, FallbackExplanation);

        Assert.False(result.IsCorrect);
        Assert.Contains("Grading failed", result.Explanation);
        Assert.Equal(GradeOutcome.GradingFailed, result.Outcome);
    }

    [Fact]
    public void Parse_NoToolUseBlock_ReturnsFriendlyMessage()
    {
        // Claude replied with plain text instead of calling the tool — shouldn't
        // happen with tool_choice forced, but the code doesn't assume that holds.
        const string body = "{\"content\":[{\"type\":\"text\",\"text\":\"I'm not sure.\"}]}";

        var result = ClaudeGradeParser.Parse(body, FallbackExplanation);

        Assert.False(result.IsCorrect);
        Assert.Contains("didn't return a grade", result.Explanation);
        Assert.Equal(GradeOutcome.GradingFailed, result.Outcome);
    }
}

// TDD spec for the retry-on-truncation idea: WasTruncated should say yes only
// for the one case a retry can actually fix (stop_reason == "max_tokens"), and
// no for everything else — a different stop_reason, a missing one, or a body
// that isn't even valid JSON. ClaudeGradeParser.WasTruncated currently exists
// only as a stub that always returns false, so 4 of these 5 pass by accident
// (false is the safe default for all the "should be false" cases) — the
// StopReasonMaxTokens test is the one that's actually red right now.
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%) — writing the spec before
// the implementation, so "done" has a checkable definition.
public class ClaudeGradeParserTruncationTests
{
    [Fact]
    public void WasTruncated_StopReasonMaxTokens_ReturnsTrue()
    {
        const string body = "{\"stop_reason\":\"max_tokens\",\"content\":[]}";

        Assert.True(ClaudeGradeParser.WasTruncated(body));
    }

    [Fact]
    public void WasTruncated_StopReasonToolUse_ReturnsFalse()
    {
        const string body = "{\"stop_reason\":\"tool_use\",\"content\":[]}";

        Assert.False(ClaudeGradeParser.WasTruncated(body));
    }

    [Fact]
    public void WasTruncated_StopReasonEndTurn_ReturnsFalse()
    {
        const string body = "{\"stop_reason\":\"end_turn\",\"content\":[]}";

        Assert.False(ClaudeGradeParser.WasTruncated(body));
    }

    [Fact]
    public void WasTruncated_MissingStopReason_ReturnsFalse()
    {
        const string body = "{\"content\":[]}";

        Assert.False(ClaudeGradeParser.WasTruncated(body));
    }

    [Fact]
    public void WasTruncated_MalformedBody_ReturnsFalseInsteadOfThrowing()
    {
        const string body = "not json at all";

        Assert.False(ClaudeGradeParser.WasTruncated(body));
    }
}
