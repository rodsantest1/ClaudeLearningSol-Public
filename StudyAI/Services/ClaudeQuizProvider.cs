using System.Text;
using System.Text.Json;
using StudyAI.Models;
using System.Linq;

namespace StudyAI.Services;

// Iteration 3: questions are now free-text (Choices comes back empty — see
// ClaudeQuestionToolParser), and GradeAsync no longer does a string compare. It
// calls Claude with a forced tool call instead, so grading judges meaning, not
// exact wording, and the response shape is guaranteed by the API rather than
// hoped for from a prompt.
//
// Iteration 5: GetQuestionAsync no longer asks for free-text only. It offers
// Claude two tools — create_multiple_choice_question and
// create_short_answer_question — with tool_choice: "auto", so Claude picks
// the format instead of the app hardcoding it. See ClaudeQuestionToolParser
// for the "which tool got called" branching this adds.
//
// Iteration 6: GetQuestionAsync now accepts a preferredFormat parameter to let
// the caller constrain question types — only MCQ, only short-answer, or either
// (the default). This demonstrates Tools and MCPs (10.6%, CCDV-F): showing how
// tool selection (which tools to offer) is itself a control lever, not just
// how tools are defined. The UI can now offer three distinct filtering modes
// without any change to the grading or recommendation logic.
//
// CCDV-F (Claude Certified Developer, Foundations) domains touched by this file:
//   - Applications and Integration (33.1%) — typed HttpClient, request/response mechanics.
//   - Prompt and Context Engineering (11.0%) — system vs. user message design,
//     structured output.
//   - Tools and MCPs (10.6%) — three different tool_choice values live in this
//     one file now: GradeAsync forces one specific tool (only one right output
//     shape), GetQuestionAsync conditionally offers one or two tools with
//     appropriate tool_choice based on preferredFormat (demonstrating tool
//     selection as a control point), and this is the file to compare
//     against ClaudeResponseParser (iteration 2/3's "ask nicely for JSON, parse
//     defensively" approach with no tools at all) for how much each step
//     narrows down what a caller has to defend against.
//   - Security and Safety (8.1%) — failing closed instead of throwing or leaking
//     a missing/invalid key back to the UI.
//   - Model Selection and Optimization (16.8%) — the optional `model` parameter
//     on both methods (caller picks the tier per-request instead of a fixed
//     app-wide setting), plus token usage attached from the response via
//     ClaudeUsageParser so cost is visible, not just latency.
//   - Agents and Workflows (14.7%) — ResolveModel (below) is reused by
//     ClaudeQuizPlanner rather than duplicated; see IQuizPlanner for the
//     actual routing-decision logic this file doesn't own.
// Not touched yet: Claude Code, Eval/Testing/Debugging.
public class ClaudeQuizProvider : IQuizProvider
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public ClaudeQuizProvider(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    // → GetQuestionAsync: generate a quiz question with optional format control
    public async Task<QuizQuestion> GetQuestionAsync(string topic, string? model = null, string? difficulty = null, IReadOnlyList<string>? recentPrompts = null, IReadOnlyList<bool>? recentWasMultipleChoice = null, QuestionFormat preferredFormat = QuestionFormat.Any, int maxTokens = 700)
    {
    // → Check for API key early
        var apiKey = _config["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ErrorQuestion("No Claude API key configured. Set Claude:ApiKey via user-secrets.");
        }

        var subject = string.IsNullOrWhiteSpace(topic) ? "general software engineering" : topic;

        // Agents and Workflows (14.7%): "medium" is the fallback when nobody's
        // told this method to do otherwise — same shape as ResolveModel, just
        // for difficulty instead of model tier. ClaudeQuizPlanner is what
        // actually sets this to something other than the default.
        var effectiveDifficulty = string.IsNullOrWhiteSpace(difficulty) ? "Medium" : difficulty;

        // Prompt and Context Engineering: the system prompt sets a persistent role
        // ("you generate exam questions"); the user prompt carries the
        // per-request instruction (topic + difficulty + avoid-list). Splitting role
        // instructions from task instructions this way is the pattern the exam
        // guide calls out under system vs. user message design.
        //
        // Tools and MCPs (10.6%): tool selection (which tools to offer) is itself
        // a control lever. Based on preferredFormat, we either offer both tools
        // with tool_choice: "auto" (Any), force one specific tool (MultipleChoice
        // or ShortAnswer). This demonstrates that not all control flows through
        // the prompt — the tool set itself, and the tool_choice value used, are
        // equally important levers in shaping Claude's output.
        //
        // Model Selection and Optimization: the caller's choice wins if given;
        // config is just the fallback default, not the only source of truth.
        var (tools, toolChoice, userMessage) = BuildToolsAndPrompt(subject, effectiveDifficulty, preferredFormat, recentPrompts, recentWasMultipleChoice);

        var payload = new
        {
            model = ResolveModel(model, _config["Claude:Model"]),
            // → 400 was too tight — ClaudeQuestionToolParser fails closed (returns
            // null, surfaced to the UI as "Claude didn't return a question. Try
            // again.") whenever the tool call gets cut off before prompt/choices/
            // correctAnswer/explanation finish writing, and a 4-choice MCQ prompt +
            // explanation can genuinely need more than 400 tokens on its own. It got
            // measurably worse toward the end of a run because BuildAvoidClause
            // appends the growing list of already-asked prompts (see
            // recentPrompts): the more Claude has to avoid repeating, the longer and
            // more elaborate a genuinely-different question tends to be, so the same
            // budget that was fine for question 1 starts truncating by question 8-10.
            // 700 is just the starting point now, not a hard ceiling — maxTokens
            // lets a caller that already saw a truncation (QuizQuestion.WasTruncated)
            // ask again with more room instead of repeating the same failure.
            max_tokens = maxTokens,
            system = "You generate exam questions for a study app by calling one of the provided " +
                     "tools. Always call a tool — never reply with plain text.",
        // → Tool definitions (determined by format preference)
            tools = tools,
            tool_choice = toolChoice,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = userMessage
                }
            }
        };

        // Applications and Integration: raw Messages API mechanics — POST to
        // /v1/messages, auth via the x-api-key header, and the required
        // anthropic-version header. (A later iteration could swap this HttpClient
        // call for the official SDK; the exam guide expects familiarity with both.)
    // → Create POST request to Claude API
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
    // → Send to Messages API
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return ErrorQuestion($"API error ({response.StatusCode}): {body}");

            // The parsing/validation logic lives in ClaudeQuestionToolParser —
            // pulled out so it's testable without a network call, same as
            // ClaudeGradeParser and ClaudeRecommendationParser. See
            // StudyAI.Tests/ClaudeQuestionToolParserTests.cs.
            var question = ClaudeQuestionToolParser.Parse(subject, body);
            if (question is null)
            {
                // → DescribeFailure re-inspects the same body to say WHY Parse
                // failed closed (max_tokens truncation vs. no tool call vs.
                // missing fields vs. unparseable JSON) instead of just "no
                // question." Logged server-side so it shows up in the VS
                // Output/console without the student needing to reproduce it,
                // and surfaced in the error message itself so it's visible
                // right where "Try again" already is.
                var reason = ClaudeQuestionToolParser.DescribeFailure(body);
                Console.Error.WriteLine($"GetQuestionAsync: Claude didn't return a usable question ({reason})");

                // → Only the max_tokens case is flagged WasTruncated — it's the
                // one failure a bigger budget can actually fix. The others (no
                // API key, a network/API error, a genuinely malformed body)
                // would just fail the same way again with more tokens, so
                // TimedQuiz.razor only auto-retries this specific reason.
                var wasTruncated = reason == ClaudeQuestionToolParser.MaxTokensTruncatedReason;
                return ErrorQuestion($"Claude didn't return a question ({reason}). Try again.", wasTruncated);
            }

            // Model Selection and Optimization: token counts are what "cost"
            // actually means. Attached after parsing rather than threaded through
            // ClaudeQuestionToolParser, since usage is a property of the whole
            // response, not of the question text specifically.
            var (inputTokens, outputTokens) = ClaudeUsageParser.Parse(body);
            return question with { InputTokens = inputTokens, OutputTokens = outputTokens, Difficulty = effectiveDifficulty };
        }
        catch (Exception ex)
        {
            return ErrorQuestion(ex.Message);
        }
    }

    // Tools and MCPs: instead of asking Claude to describe the verdict in prose,
    // this defines a tool schema and sets tool_choice to *force* that exact
    // tool call — unlike GetQuestionAsync's tool_choice (which varies based on
    // preferredFormat), the model literally cannot reply any other way once a
    // specific tool is forced, because there's only one valid output shape for a grade.
    // → GradeAsync: evaluate student answer using tool-based grading
    public async Task<GradeResult> GradeAsync(QuizQuestion question, string userAnswer, string? model = null)
    {
    // → Check for API key early
        var apiKey = _config["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new GradeResult(false, "No Claude API key configured. Set Claude:ApiKey via user-secrets.", GradingFailed: true);

        var payload = new
        {
            model = ResolveModel(model, _config["Claude:Model"]),
            max_tokens = 300,
            tools = new object[]
            {
                new
                {
                    name = "submit_grade",
                    description = "Grade a student's free-text answer to an exam question.",
                    input_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            isCorrect = new
                            {
                                type = "boolean",
                                description = "Whether the student's answer is substantively correct."
                            },
                            explanation = new
                            {
                                type = "string",
                                description = "One or two sentence explanation, referencing the correct answer."
                            }
                        },
                        required = new[] { "isCorrect", "explanation" }
                    }
                }
            },
            tool_choice = new { type = "tool", name = "submit_grade" },
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $"Question: {question.Prompt}\n" +
                              $"Reference answer: {question.CorrectAnswer}\n" +
                              $"Student's answer: {userAnswer}\n\n" +
                              "Judge the student's answer on meaning, not exact wording — minor phrasing " +
                              "differences are fine if the core idea matches the reference answer. Call " +
                              "submit_grade with your verdict."
                }
            }
        };

    // → Create POST request to Claude API
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
    // → Send to Messages API
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new GradeResult(false, $"API error ({response.StatusCode}): grading unavailable, try again.", GradingFailed: true);

            // The parsing — including what happens if the tool call came back
            // incomplete (e.g. hit max_tokens mid-generation) — lives in
            // ClaudeGradeParser so it's testable without a network call. See
            // StudyAI.Tests/ClaudeGradeParserTests.cs.
            var result = ClaudeGradeParser.Parse(body, question.Explanation);

            var (inputTokens, outputTokens) = ClaudeUsageParser.Parse(body);
            return result with { InputTokens = inputTokens, OutputTokens = outputTokens };
        }
        catch (Exception ex)
        {
            // Network-level failures only at this point (timeout, DNS, etc.) —
            // response-shape failures are handled inside ClaudeGradeParser.
            return new GradeResult(false, $"Grading failed: {ex.Message}", GradingFailed: true);
        }
    }

    // Security and Safety: fail closed. No missing-key exception bubbles up to crash
    // the request, and no raw exception detail beyond a short message is shown —
    // this satisfies the same shape (QuizQuestion) the UI already knows how to render.
    private static QuizQuestion ErrorQuestion(string message, bool wasTruncated = false) =>
        new("Setup needed", "Couldn't get a question from Claude.", new[] { "OK" }, "OK", message, WasTruncated: wasTruncated);

    // Pulled out for the same reason ClaudeResponseParser/ClaudeGradeParser
    // were: a request-per-call parameter (requestedModel) had no test coverage
    // at all, since the one existing GradeAsync test hits the no-API-key
    // early-return before ever reaching this logic. This one line is now
    // directly testable without touching HttpClient or config. See
    // StudyAI.Tests/ClaudeQuizProviderModelTests.cs.
    public static string ResolveModel(string? requestedModel, string? configuredModel) =>
        requestedModel ?? configuredModel ?? "claude-sonnet-4-5";

    // Tools and MCPs (10.6%): tool selection is a control point. This method
    // builds the tool array and tool_choice value based on preferredFormat,
    // demonstrating how the set of available tools (not just the prompt) shapes
    // what Claude can do. Pulled out so it's directly testable without touching
    // HttpClient or the full request payload. QuestionFormat.Any offers both
    // tools with tool_choice: "auto", QuestionFormat.MultipleChoice forces only
    // the MCQ tool, QuestionFormat.ShortAnswer forces only short-answer.
    public static (object[] tools, object toolChoice, string userMessage) BuildToolsAndPrompt(
        string subject,
        string difficulty,
        QuestionFormat preferredFormat,
        IReadOnlyList<string>? recentPrompts,
        IReadOnlyList<bool>? recentWasMultipleChoice)
    {
        var multipleChoiceTool = new
        {
            name = ClaudeQuestionToolParser.MultipleChoiceTool,
            description = "Create a multiple-choice exam question with a fixed set of answer choices.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    prompt = new { type = "string", description = "The question text — 2-4 sentences, concise even for a scenario-based question." },
                    choices = new
                    {
                        type = "array",
                        items = new { type = "string" },
                        description = "3 or 4 answer options, exactly one of which matches correctAnswer word-for-word."
                    },
                    correctAnswer = new { type = "string", description = "The correct choice — must exactly match one entry in choices." },
                    explanation = new { type = "string", description = "1-2 sentence explanation of the correct answer." }
                },
                required = new[] { "prompt", "choices", "correctAnswer", "explanation" }
            }
        };

        var shortAnswerTool = new
        {
            name = ClaudeQuestionToolParser.ShortAnswerTool,
            description = "Create a free-text exam question with a reference answer for grading — not multiple choice.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    prompt = new { type = "string", description = "The question text, answerable in a sentence or two." },
                    correctAnswer = new { type = "string", description = "A concise reference answer to grade against." },
                    explanation = new { type = "string", description = "1-2 sentence explanation." }
                },
                required = new[] { "prompt", "correctAnswer", "explanation" }
            }
        };

        object[] resultTools;
        object resultToolChoice;
        string formatInstruction;

        if (preferredFormat == QuestionFormat.MultipleChoice)
        {
            resultTools = new object[] { multipleChoiceTool };
            resultToolChoice = new { type = "tool", name = ClaudeQuestionToolParser.MultipleChoiceTool };
            formatInstruction = "You must create a multiple-choice question.";
        }
        else if (preferredFormat == QuestionFormat.ShortAnswer)
        {
            resultTools = new object[] { shortAnswerTool };
            resultToolChoice = new { type = "tool", name = ClaudeQuestionToolParser.ShortAnswerTool };
            formatInstruction = "You must create a short-answer (free-text) question.";
        }
        else
        {
            resultTools = new object[] { multipleChoiceTool, shortAnswerTool };
            resultToolChoice = new { type = "auto" };
            formatInstruction = "Use whichever format — multiple-choice or short-answer — genuinely fits the question best; don't default to one over the other.";
        }

        var userMessage = $"Write one {difficulty.ToLowerInvariant()}-difficulty exam question about " +
                          $"{subject}. {formatInstruction} Call the matching tool." +
                          BuildAvoidClause(recentPrompts) +
                          (preferredFormat == QuestionFormat.Any ? BuildFormatHintClause(recentWasMultipleChoice) : "");

        return (resultTools, resultToolChoice, userMessage);
    }

    // Prompt and Context Engineering (11.0%): a stateless "write one question
    // about X" prompt has no memory of its own prior output, so for a narrow
    // topic/difficulty pair Claude tends to converge on the same "obvious"
    // question run after run. Appending an explicit avoid-list steers it away
    // without needing any server-side session state — the caller (Quiz.razor)
    // just replays back what it already showed the user. Pulled out as its
    // own pure method for the same reason ResolveModel was: directly testable
    // without touching HttpClient. See StudyAI.Tests/ClaudeQuizProviderAvoidClauseTests.cs.
    //
    // → This clause used to just say "don't repeat these" and list the full
    // prompts verbatim, with no cap on length in either direction. That has a
    // real failure mode: told not to repeat or rephrase a growing list of
    // prior questions, Claude's easiest way to guarantee it's "different
    // enough" is to add more scenario detail rather than pick a genuinely
    // different concept — so questions kept getting longer over a run (by
    // question 8-10, sometimes a full page for one prompt). And it compounds:
    // a longer question produces a longer avoid-list entry next time, which
    // pushes toward an even longer question after that. Two fixes here:
    // TruncateForAvoidList caps what gets replayed back (breaking the
    // feedback loop at its source), and the explicit "vary the concept, not
    // the amount of detail" sentence steers HOW Claude tries to be different.
    public static string BuildAvoidClause(IReadOnlyList<string>? recentPrompts)
    {
        if (recentPrompts is null || recentPrompts.Count == 0) return "";

        var list = string.Join("\n", recentPrompts.Select(p => $"- {TruncateForAvoidList(p)}"));
        return "\n\nDo not repeat or closely rephrase any of these already-asked questions:\n" + list +
               "\n\nVary the underlying concept or angle to stay different from the list above — " +
               "not the amount of detail. Keep the question just as concise as it would be without this list.";
    }

    // → A truncated prompt is still more than enough for Claude to recognize
    // and avoid repeating the same question — the avoid-list only needs to
    // be recognizable, not complete. TrimEnd before appending the ellipsis
    // avoids a truncation landing mid-word with a trailing space looking odd
    // (e.g. "...the constructor …" instead of "...the construct…").
    private const int AvoidListEntryMaxChars = 100;

    private static string TruncateForAvoidList(string prompt) =>
        prompt.Length <= AvoidListEntryMaxChars
            ? prompt
            : prompt[..AvoidListEntryMaxChars].TrimEnd() + "…";

    // Tools and MCPs (10.6%) / Prompt and Context Engineering (11.0%): a
    // qualitative instruction in the base prompt ("default to short-answer")
    // was tried first and overcorrected — tool_choice: "auto" went from
    // nearly always multiple-choice to nearly always short-answer in manual
    // testing, because a static preference has no feedback loop. This is the
    // same fix shape as BuildAvoidClause, applied to format instead of
    // content: replay the *actual* recent distribution back to Claude and
    // only nudge when it's genuinely lopsided, instead of asserting a
    // fixed preference every single call regardless of what's already
    // happened this session. Only used when preferredFormat is Any — when
    // the caller forces a specific format, this hint is not needed.
    public static string BuildFormatHintClause(IReadOnlyList<bool>? recentWasMultipleChoice)
    {
        if (recentWasMultipleChoice is null || recentWasMultipleChoice.Count == 0) return "";

        var multipleChoiceCount = recentWasMultipleChoice.Count(wasMultipleChoice => wasMultipleChoice);
        var shortAnswerCount = recentWasMultipleChoice.Count - multipleChoiceCount;

        if (multipleChoiceCount == shortAnswerCount) return "";

        return multipleChoiceCount > shortAnswerCount
            ? "\n\nYour recent questions have mostly been multiple-choice. Use short-answer this time unless the question genuinely doesn't work as short-answer."
            : "\n\nYour recent questions have mostly been short-answer. Use multiple-choice this time unless the question genuinely doesn't work as multiple-choice.";
    }
}
