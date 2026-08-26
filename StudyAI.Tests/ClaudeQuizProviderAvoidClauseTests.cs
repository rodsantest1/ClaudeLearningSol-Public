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

    [Fact]
    public void BuildAvoidClause_IncludesVaryConceptInstruction()
    {
        // The instruction that steers Claude toward being different by
        // concept rather than by adding more words — see the class comment
        // on the questions-getting-longer-over-a-run failure mode.
        var result = ClaudeQuizProvider.BuildAvoidClause(new[] { "Some question?" });

        Assert.Contains("Vary the underlying concept or angle", result);
    }

    // --- Avoid-list entry truncation ---------------------------------------
    //
    // Caps what gets replayed back per prior prompt, so a long question
    // doesn't produce an equally long avoid-list entry that pressures the
    // next question to be longer still.

    [Fact]
    public void BuildAvoidClause_ShortPrompt_IsNotTruncated()
    {
        const string shortPrompt = "What keyword makes a field settable only in the constructor?";

        var result = ClaudeQuizProvider.BuildAvoidClause(new[] { shortPrompt });

        Assert.Contains(shortPrompt, result);
        Assert.DoesNotContain("…", result);
    }

    [Fact]
    public void BuildAvoidClause_LongPrompt_IsTruncatedWithEllipsis()
    {
        var longPrompt = "Consider a scenario where " + new string('x', 200) + " what happens?";

        var result = ClaudeQuizProvider.BuildAvoidClause(new[] { longPrompt });

        Assert.Contains("…", result);
        Assert.DoesNotContain(longPrompt, result);
        // The full 200-x run shouldn't survive intact — only a truncated prefix should.
        Assert.DoesNotContain(new string('x', 200), result);
    }

    [Fact]
    public void BuildAvoidClause_LongPrompt_KeepsPrefixRecognizable()
    {
        var longPrompt = "Consider a scenario where " + new string('x', 200) + " what happens?";

        var result = ClaudeQuizProvider.BuildAvoidClause(new[] { longPrompt });

        Assert.Contains("Consider a scenario where", result);
    }

    [Fact]
    public void BuildAvoidClause_MultipleLongPrompts_EachTruncatedIndependently()
    {
        var first = "First scenario description " + new string('a', 150);
        var second = "Second scenario description " + new string('b', 150);

        var result = ClaudeQuizProvider.BuildAvoidClause(new[] { first, second });

        Assert.Contains("First scenario description", result);
        Assert.Contains("Second scenario description", result);
        Assert.DoesNotContain(new string('a', 150), result);
        Assert.DoesNotContain(new string('b', 150), result);
    }
}
