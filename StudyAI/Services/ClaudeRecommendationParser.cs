using System.Linq;
using System.Text.Json;
using StudyAI.Models;

namespace StudyAI.Services;

// Pure parsing of a Messages API response body into a NextStepRecommendation —
// same shape as ClaudeGradeParser, applied to the recommend_next tool call
// instead of submit_grade. No HttpClient, no network. See
// StudyAI.Tests/ClaudeRecommendationParserTests.cs.
//
// Agents and Workflows (CCDV-F, 14.7%): this is the parsing half of the
// "routing" workflow pattern (see ClaudeQuizPlanner) — a forced tool call
// classifies the input (a student's performance history) and directs the next
// step (topic + difficulty), so the output is a guaranteed shape instead of
// free text to hope-parse.
public static class ClaudeRecommendationParser
{
    private const string DefaultDifficulty = "Medium";
    private const string DefaultReason = "Recommended based on your performance so far.";

    // fallbackTopic is the topic already selected in the UI — used whenever
    // Claude's tool call is missing, incomplete, or the body isn't valid JSON
    // at all, so a failed recommendation still leaves the app on a real topic
    // instead of an empty one.
    public static NextStepRecommendation Parse(string responseBody, string fallbackTopic)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var toolUse = doc.RootElement.GetProperty("content")
                .EnumerateArray()
                .FirstOrDefault(block => block.GetProperty("type").GetString() == "tool_use");

            if (toolUse.ValueKind == JsonValueKind.Undefined)
                return new NextStepRecommendation(fallbackTopic, DefaultDifficulty, DefaultReason);

            var input = toolUse.GetProperty("input");
            var topic = input.TryGetProperty("topic", out var t) ? t.GetString() ?? fallbackTopic : fallbackTopic;
            var difficulty = input.TryGetProperty("difficulty", out var d) ? d.GetString() ?? DefaultDifficulty : DefaultDifficulty;
            var reason = input.TryGetProperty("reason", out var r) ? r.GetString() ?? DefaultReason : DefaultReason;

            return new NextStepRecommendation(topic, difficulty, reason);
        }
        catch (Exception)
        {
            // Covers: the body isn't valid JSON at all (response cut off
            // mid-document), or the tool_use block is missing entirely.
            // Fail closed to the fallback instead of throwing into
            // RecommendNextAsync's caller.
            return new NextStepRecommendation(fallbackTopic, DefaultDifficulty, DefaultReason);
        }
    }
}
