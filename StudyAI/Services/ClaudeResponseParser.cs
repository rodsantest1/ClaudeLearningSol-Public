using System.Text.Json;
using StudyAI.Models;

namespace StudyAI.Services;

// Pulled out of ClaudeQuizProvider on purpose: everything in this file is pure
// (text in, QuizQuestion or null out) — no HttpClient, no network, no I/O. That's
// what makes it unit testable without mocking anything; see
// StudyAI.Tests/ClaudeResponseParserTests.cs. Applications and Integration
// (CCDV-F, 33.1%) includes validating model output before trusting it — this is
// that validation, isolated from the API call that produced the text.
//
// As of iteration 3, an empty Choices array is a valid, expected shape (a
// free-text question), not an error — only a missing/null Choices property
// indicates the model didn't follow the requested format.
//
// Superseded by ClaudeQuestionToolParser as of iteration 5 — GetQuestionAsync
// no longer prompts for raw JSON text, it uses two tools with tool_choice:
// "auto" instead. Not registered anywhere anymore (same status as
// StaticQuizProvider: no longer wired into the live path), but kept as-is,
// tests included, as the "before" side of the diff — this is what "ask
// nicely for JSON, parse defensively" (iteration 2/3) looked like right
// before tool_choice: "auto" (iteration 5) replaced it. Compare ExtractJson's
// string-slicing defensiveness against ClaudeQuestionToolParser's "the API
// already validated the shape, I just have to pick which tool" simplicity —
// that gap is the point of this file staying in the repo.
public static class ClaudeResponseParser
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // Claude usually obeys "JSON only," but strips defensively in case a stray
    // sentence or code fence sneaks in around the object.
    public static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    // Returns null instead of throwing when the model's text doesn't match the
    // expected shape — the caller decides what to do (show an error card, retry,
    // log it for an eval later).
    public static QuizQuestion? Parse(string subject, string rawText)
    {
        GeneratedQuestion? generated;
        try
        {
            generated = JsonSerializer.Deserialize<GeneratedQuestion>(ExtractJson(rawText), JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }

        if (generated is null || generated.Choices is null)
            return null;

        return new QuizQuestion(subject, generated.Prompt, generated.Choices, generated.CorrectAnswer, generated.Explanation);
    }

    private record GeneratedQuestion(string Prompt, List<string> Choices, string CorrectAnswer, string Explanation);
}
