using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeQuestionToolParser — the piece of GetQuestionAsync that turns a
// raw Messages API response body into a QuizQuestion, now that iteration 5
// asks Claude to call one of two tools instead of returning prompted JSON
// text. No network, no mocking: canned response bodies in, QuizQuestion (or
// null) out, same contract ClaudeResponseParser had for iteration 2/3.
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%). The tool_choice: "auto"
// scenarios (no tool call at all) are the one failure mode that's genuinely
// new here — ClaudeGradeParser/ClaudeRecommendationParser never have to
// handle "Claude just replied with text," because their tool_choice is
// forced.
public class ClaudeQuestionToolParserTests
{
    [Fact]
    public void Parse_MultipleChoiceToolCall_ReturnsQuestionWithChoices()
    {
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"create_multiple_choice_question\"," +
            "\"input\":{\"prompt\":\"Which keyword makes a field settable only in the constructor?\"," +
            "\"choices\":[\"const\",\"readonly\",\"static\",\"sealed\"]," +
            "\"correctAnswer\":\"readonly\",\"explanation\":\"readonly fields can be set in the constructor.\"}}]}";

        var question = ClaudeQuestionToolParser.Parse("C# Basics", body);

        Assert.NotNull(question);
        Assert.Equal("Which keyword makes a field settable only in the constructor?", question!.Prompt);
        Assert.Equal(4, question.Choices.Count);
        Assert.Equal("readonly", question.CorrectAnswer);
        Assert.Equal("C# Basics", question.Topic);
    }

    [Fact]
    public void Parse_ShortAnswerToolCall_ReturnsQuestionWithEmptyChoices()
    {
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"create_short_answer_question\"," +
            "\"input\":{\"prompt\":\"What does HTTP 201 mean?\"," +
            "\"correctAnswer\":\"A new resource was created.\",\"explanation\":\"Returned from a successful POST.\"}}]}";

        var question = ClaudeQuestionToolParser.Parse("Web Fundamentals", body);

        Assert.NotNull(question);
        Assert.Empty(question!.Choices);
        Assert.Equal("A new resource was created.", question.CorrectAnswer);
    }

    [Fact]
    public void Parse_NoToolUseBlock_ReturnsNull()
    {
        // tool_choice: "auto" (unlike GradeAsync/RecommendNextAsync's forced
        // calls) genuinely allows this — Claude can reply with plain text
        // instead of calling either tool.
        const string body = "{\"content\":[{\"type\":\"text\",\"text\":\"Here's a question for you...\"}]}";

        var question = ClaudeQuestionToolParser.Parse("C# Basics", body);

        Assert.Null(question);
    }

    [Fact]
    public void Parse_MultipleChoiceMissingChoicesField_TreatsAsEmptyChoices()
    {
        // Truncated after correctAnswer/explanation but before choices
        // finished writing — required fields (prompt/correctAnswer/
        // explanation) are all present, so this still parses; it just can't
        // reconstruct the choices list, same fallback as a real short-answer
        // question.
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"create_multiple_choice_question\"," +
            "\"input\":{\"prompt\":\"Which is a value type?\"," +
            "\"correctAnswer\":\"struct\",\"explanation\":\"Structs are value types.\"}}]}";

        var question = ClaudeQuestionToolParser.Parse("C# Basics", body);

        Assert.NotNull(question);
        Assert.Empty(question!.Choices);
    }

    [Fact]
    public void Parse_MissingRequiredField_ReturnsNull()
    {
        // Truncated before explanation finished writing at all.
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"create_short_answer_question\"," +
            "\"input\":{\"prompt\":\"What is ACID?\",\"correctAnswer\":\"A set of database transaction guarantees.\"}}]}";

        var question = ClaudeQuestionToolParser.Parse("Databases", body);

        Assert.Null(question);
    }

    [Fact]
    public void Parse_MalformedBody_ReturnsNull()
    {
        const string body = "{\"content\":[{\"type\":\"tool_use\",\"name\":\"create_short_answer_question\",\"input\":{\"prompt\":";

        var question = ClaudeQuestionToolParser.Parse("Databases", body);

        Assert.Null(question);
    }

    // --- DescribeFailure -------------------------------------------------
    //
    // Only ever called after Parse has already returned null for the same
    // body — these tests reuse the exact bodies from the Parse_*_ReturnsNull
    // cases above so the two stay in lockstep.

    [Fact]
    public void DescribeFailure_StopReasonMaxTokens_ReturnsTruncationMessage()
    {
        // stop_reason is checked first regardless of what content made it
        // through before the cutoff — this is the real-world case that
        // prompted adding DescribeFailure at all (see ClaudeQuizProvider's
        // max_tokens comment). The body has to stay valid JSON overall (the
        // API's envelope always parses even when Claude's own generation was
        // cut off mid-tool-call) — it's the *input* object that's missing
        // fields, not the surrounding document that's malformed.
        const string body =
            "{\"stop_reason\":\"max_tokens\",\"content\":[{\"type\":\"tool_use\"," +
            "\"name\":\"create_multiple_choice_question\"," +
            "\"input\":{\"prompt\":\"Which keyword makes a field settable only in the constructor?\"}}]}";

        var reason = ClaudeQuestionToolParser.DescribeFailure(body);

        Assert.Equal(ClaudeQuestionToolParser.MaxTokensTruncatedReason, reason);
    }

    [Fact]
    public void DescribeFailure_NoToolUseBlock_ReturnsNoToolCallMessage()
    {
        const string body = "{\"content\":[{\"type\":\"text\",\"text\":\"Here's a question for you...\"}]}";

        var reason = ClaudeQuestionToolParser.DescribeFailure(body);

        Assert.Equal("Claude replied without calling a tool", reason);
    }

    [Fact]
    public void DescribeFailure_ToolUsePresentButMissingRequiredFields_ReturnsMissingFieldsMessage()
    {
        const string body =
            "{\"content\":[{\"type\":\"tool_use\",\"name\":\"create_short_answer_question\"," +
            "\"input\":{\"prompt\":\"What is ACID?\",\"correctAnswer\":\"A set of database transaction guarantees.\"}}]}";

        var reason = ClaudeQuestionToolParser.DescribeFailure(body);

        Assert.Equal("Claude's tool call was missing required fields", reason);
    }

    [Fact]
    public void DescribeFailure_MalformedBody_ReturnsInvalidJsonMessage()
    {
        const string body = "{\"content\":[{\"type\":\"tool_use\",\"name\":\"create_short_answer_question\",\"input\":{\"prompt\":";

        var reason = ClaudeQuestionToolParser.DescribeFailure(body);

        Assert.Equal("Claude's response wasn't valid JSON", reason);
    }

    [Fact]
    public void DescribeFailure_NoStopReasonField_FallsThroughToToolUseCheck()
    {
        // Real response bodies always include stop_reason, but DescribeFailure
        // shouldn't throw if it's absent — TryGetProperty just fails the first
        // check and moves on to the tool_use scan.
        const string body = "{\"content\":[{\"type\":\"text\",\"text\":\"no tool here\"}]}";

        var reason = ClaudeQuestionToolParser.DescribeFailure(body);

        Assert.Equal("Claude replied without calling a tool", reason);
    }
}
