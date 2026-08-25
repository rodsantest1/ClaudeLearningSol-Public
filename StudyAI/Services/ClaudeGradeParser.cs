using System.Linq;
using System.Text.Json;
using StudyAI.Models;

namespace StudyAI.Services;

// Pure parsing of a Messages API response body into a GradeResult, pulled out
// of ClaudeQuizProvider.GradeAsync for the same reason ClaudeResponseParser
// exists: no HttpClient, no network — just a response body string in, a
// GradeResult out. This is specifically the "what if the tool call is missing,
// incomplete, or the response got cut off" logic. See
// StudyAI.Tests/ClaudeGradeParserTests.cs, including the max_tokens truncation
// scenarios.
public static class ClaudeGradeParser
{
    // fallbackExplanation is what the caller already knows about the question
    // (question.Explanation) — used when Claude's own explanation field is
    // missing, e.g. because generation got cut off after isCorrect but before
    // explanation was written.
    public static GradeResult Parse(string responseBody, string fallbackExplanation)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var toolUse = doc.RootElement.GetProperty("content")
                .EnumerateArray()
                .FirstOrDefault(block => block.GetProperty("type").GetString() == "tool_use");

            if (toolUse.ValueKind == JsonValueKind.Undefined)
                return new GradeResult(false, "Claude didn't return a grade. Try submitting again.", GradingFailed: true);

            var input = toolUse.GetProperty("input");
            var isCorrect = input.GetProperty("isCorrect").GetBoolean();
            var explanation = input.TryGetProperty("explanation", out var exp)
                ? exp.GetString() ?? fallbackExplanation
                : fallbackExplanation;

            return new GradeResult(isCorrect, explanation);
        }
        catch (Exception ex)
        {
            // Covers: the body isn't valid JSON at all (response cut off
            // mid-document), or the tool_use block is missing required fields
            // (e.g. isCorrect never got written before truncation). Either way,
            // fail closed with a message instead of throwing into GradeAsync's
            // caller.
            return new GradeResult(false, $"Grading failed: {ex.Message}", GradingFailed: true);
        }
    }

    // True only when the response was cut off by the token cap — the one
    // failure mode a retry with a higher max_tokens can actually fix. Every
    // other case (a different stop_reason, a missing one, unparseable JSON)
    // falls through to false, same as the stub: "not confirmed truncated" is
    // still the safe default when the signal isn't there.
    public static bool WasTruncated(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.TryGetProperty("stop_reason", out var stopReason)
                && stopReason.GetString() == "max_tokens";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
