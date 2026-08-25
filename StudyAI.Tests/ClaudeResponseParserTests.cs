using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// No network, no mocking — this is exactly the kind of Claude-integration logic
// that's testable without ever calling the API: does the code correctly turn
// model output into the shape the rest of the app expects?
//
// CCDV-F domains: Eval, Testing, and Debugging (2.6%) — verifying model-output
// handling with fixed inputs instead of eyeballing it in the running app. Also
// reinforces Applications and Integration (33.1%): "don't trust the model's
// text" is only a real practice if something checks it, which is what this class does.
public class ClaudeResponseParserTests
{
    private const string ValidJson =
        "{\"prompt\": \"What is 2+2?\", \"choices\": [\"3\",\"4\",\"5\"], " +
        "\"correctAnswer\": \"4\", \"explanation\": \"Basic math.\"}";

    [Fact]
    public void Parse_CleanJson_ReturnsQuestion()
    {
        var question = ClaudeResponseParser.Parse("Math", ValidJson);

        Assert.NotNull(question);
        Assert.Equal("What is 2+2?", question!.Prompt);
        Assert.Equal("4", question.CorrectAnswer);
        Assert.Equal(3, question.Choices.Count);
    }

    [Fact]
    public void Parse_JsonWrappedInProseOrCodeFence_StillParses()
    {
        var wrapped = $"Sure, here's a question:\n```json\n{ValidJson}\n```\nHope that helps!";

        var question = ClaudeResponseParser.Parse("Math", wrapped);

        Assert.NotNull(question);
        Assert.Equal("4", question!.CorrectAnswer);
    }

    [Fact]
    public void Parse_Garbage_ReturnsNull()
    {
        var question = ClaudeResponseParser.Parse("Math", "not json at all");

        Assert.Null(question);
    }

    // Iteration 3: free-text questions come back with an empty choices array.
    // That's a valid shape now, not a parse failure — this guards against
    // accidentally reintroducing the old "reject if Count == 0" check.
    [Fact]
    public void Parse_EmptyChoicesArray_StillParses()
    {
        const string freeTextJson =
            "{\"prompt\": \"What does HTTP 201 mean?\", \"choices\": [], " +
            "\"correctAnswer\": \"A new resource was created.\", \"explanation\": \"Returned from a successful POST.\"}";

        var question = ClaudeResponseParser.Parse("Web", freeTextJson);

        Assert.NotNull(question);
        Assert.Empty(question!.Choices);
    }

    [Fact]
    public void ExtractJson_NoBraces_ReturnsOriginalText()
    {
        var result = ClaudeResponseParser.ExtractJson("no json here");

        Assert.Equal("no json here", result);
    }
}
