using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeQuizProvider.BuildAvoidClause — the pure logic behind the
// "don't repeat the same question" fix. Quiz.razor has no server-side session
// state, so the only way to steer Claude away from a question it already
// asked is to replay recently-shown prompts back into the next request.
// Pulled into its own method for the same reason ResolveModel was: directly
// testable without HttpClient or config.
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%); the clause itself
// belongs to Prompt and Context Engineering (11.0%).
public class ClaudeQuizProviderAvoidClauseTests
{
    [Fact]
    public void BuildAvoidClause_NullList_ReturnsEmptyString()
    {
        var result = ClaudeQuizProvider.BuildAvoidClause(null);

        Assert.Equal("", result);
    }

    [Fact]
    public void BuildAvoidClause_EmptyList_ReturnsEmptyString()
    {
        var result = ClaudeQuizProvider.BuildAvoidClause(Array.Empty<string>());

        Assert.Equal("", result);
    }

    [Fact]
    public void BuildAvoidClause_OnePrompt_ListsIt()
    {
        var result = ClaudeQuizProvider.BuildAvoidClause(new[] { "What keyword makes a field settable only in the constructor?" });

        Assert.Contains("What keyword makes a field settable only in the constructor?", result);
        Assert.Contains("Do not repeat or closely rephrase", result);
    }

    [Fact]
    public void BuildAvoidClause_MultiplePrompts_ListsAllOfThem()
    {
        var result = ClaudeQuizProvider.BuildAvoidClause(new[] { "Question one?", "Question two?", "Question three?" });

        Assert.Contains("Question one?", result);
        Assert.Contains("Question two?", result);
        Assert.Contains("Question three?", result);
    }
}
