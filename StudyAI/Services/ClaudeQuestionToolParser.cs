using System.Linq;
using System.Text.Json;
using StudyAI.Models;

namespace StudyAI.Services;

// Iteration 5: pure parsing of a Messages API response body into a
// QuizQuestion, for GetQuestionAsync's tool_choice: "auto" call — Claude
// picks one of two tools (create_multiple_choice_question or
// create_short_answer_question) instead of the app always requesting
// free-text. Same "no HttpClient, no network" split as ClaudeGradeParser and
// ClaudeRecommendationParser; the difference from both of those is that this
// one has to branch on *which* tool got called, since tool_choice is "auto"
// here rather than forced to one specific tool.
//
// Tools and MCPs (CCDV-F, 10.6%): auto vs. forced tool_choice is the concept
// this file demonstrates. GradeAsync and RecommendNextAsync force a specific
// tool because there's only one right shape for their output. GetQuestionAsync
// genuinely has two valid shapes, so tool_choice: "auto" lets Claude choose —
// which also means "Claude replied with plain text instead of calling either
// tool" is a real possibility here in a way it isn't for the forced-tool
// call sites, so Parse returns null (not a fallback value) for the caller to
// turn into an error card, same contract as ClaudeResponseParser (the
// iteration 2/3 JSON-text parser this supersedes).
public static class ClaudeQuestionToolParser
{
    public const string MultipleChoiceTool = "create_multiple_choice_question";
    public const string ShortAnswerTool = "create_short_answer_question";

    // → Shared with DescribeFailure below and with ClaudeQuizProvider, which
    // compares a failure's reason against this exact string to decide whether
    // it's the one failure mode worth an automatic retry with a bigger
    // max_tokens budget (see QuizQuestion.WasTruncated). Pulled into a
    // constant instead of leaving the literal duplicated in both places so
    // the two can't silently drift apart.
    public const string MaxTokensTruncatedReason =
        "Claude's response was cut off after hitting the token limit before finishing the question";

    public static QuizQuestion? Parse(string subject, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var toolUse = doc.RootElement.GetProperty("content")
                .EnumerateArray()
                .FirstOrDefault(block => block.GetProperty("type").GetString() == "tool_use");

            if (toolUse.ValueKind == JsonValueKind.Undefined)
                return null;

            var toolName = toolUse.GetProperty("name").GetString();
            var input = toolUse.GetProperty("input");

            // prompt/correctAnswer/explanation are required by both tool
            // schemas — if the call got truncated before any of them
            // finished writing, treat it the same as no tool call at all
            // rather than handing the UI a half-built question.
            if (!input.TryGetProperty("prompt", out var promptEl) ||
                !input.TryGetProperty("correctAnswer", out var answerEl) ||
                !input.TryGetProperty("explanation", out var explanationEl))
                return null;

            var prompt = promptEl.GetString();
            var correctAnswer = answerEl.GetString();
            var explanation = explanationEl.GetString();

            if (prompt is null || correctAnswer is null || explanation is null)
                return null;

            // Only create_multiple_choice_question's schema has a choices
            // array — create_short_answer_question gets the same empty
            // Choices list free-text questions have had since iteration 3.
            var choices = toolName == MultipleChoiceTool && input.TryGetProperty("choices", out var choicesEl)
                ? choicesEl.EnumerateArray().Select(c => c.GetString() ?? "").ToList()
                : new List<string>();

            return new QuizQuestion(subject, prompt, choices, correctAnswer, explanation);
        }
        catch (Exception)
        {
            // Covers: the body isn't valid JSON at all (response cut off
            // mid-document), or a required GetProperty above throws because
            // the field genuinely isn't there. Fail closed with null instead
            // of throwing into GetQuestionAsync's caller.
            return null;
        }
    }

    // → Called only when Parse returns null, to say WHY instead of just
    // "no question." Kept separate from Parse (rather than folding this in)
    // so Parse's contract stays exactly "QuizQuestion or null" and this stays
    // independently testable — same no-network, canned-body-in/string-out
    // shape as Parse itself. stop_reason == "max_tokens" is checked first and
    // is the unambiguous, common real cause (see ClaudeQuizProvider's
    // max_tokens comment — a 4-choice MCQ with an explanation can genuinely
    // need more tokens than the budget allows, especially with a long
    // avoid-list pushing Claude toward a longer, more differentiated
    // question). Everything else falls back to weaker signals about what
    // Claude actually sent back.
    public static string DescribeFailure(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("stop_reason", out var stopReasonEl) &&
                stopReasonEl.GetString() == "max_tokens")
            {
                return MaxTokensTruncatedReason;
            }

            var hasToolUse = root.TryGetProperty("content", out var contentEl) &&
                contentEl.ValueKind == JsonValueKind.Array &&
                contentEl.EnumerateArray().Any(block =>
                    block.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "tool_use");

            if (!hasToolUse)
                return "Claude replied without calling a tool";

            return "Claude's tool call was missing required fields";
        }
        catch (Exception)
        {
            return "Claude's response wasn't valid JSON";
        }
    }
}
