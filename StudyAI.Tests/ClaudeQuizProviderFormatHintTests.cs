using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeQuizProvider.BuildFormatHintClause — added after manual
// testing showed a static "default to short-answer" instruction in the base
// prompt overcorrected tool_choice: "auto" from nearly-always
// multiple-choice to nearly-always short-answer. This replaces that static
// preference with a data-driven nudge based on the actual recent format
// history Quiz.razor tracks (RecentFormats), same fix shape as
// BuildAvoidClause/RecentPrompts for the repeated-question problem.
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%); the clause itself
// belongs to Tools and MCPs (10.6%) / Prompt and Context Engineering (11.0%).
public class ClaudeQuizProviderFormatHintTests
{
    [Fact]
    public void BuildFormatHintClause_NullList_ReturnsEmptyString()
    {
        var result = ClaudeQuizProvider.BuildFormatHintClause(null);

        Assert.Equal("", result);
    }

    [Fact]
    public void BuildFormatHintClause_EmptyList_ReturnsEmptyString()
    {
        var result = ClaudeQuizProvider.BuildFormatHintClause(Array.Empty<bool>());

        Assert.Equal("", result);
    }

    [Fact]
    public void BuildFormatHintClause_MostlyMultipleChoice_NudgesTowardShortAnswer()
    {
        var result = ClaudeQuizProvider.BuildFormatHintClause(new[] { true, true, true, false });

        Assert.Contains("mostly been multiple-choice", result);
        Assert.Contains("Use short-answer this time", result);
    }

    [Fact]
    public void BuildFormatHintClause_MostlyShortAnswer_NudgesTowardMultipleChoice()
    {
        var result = ClaudeQuizProvider.BuildFormatHintClause(new[] { false, false, false, true });

        Assert.Contains("mostly been short-answer", result);
        Assert.Contains("Use multiple-choice this time", result);
    }

    [Fact]
    public void BuildFormatHintClause_EvenSplit_ReturnsEmptyString()
    {
        // Balanced already — no correction needed, and asserting a
        // preference here would just reintroduce the original problem
        // (a static nudge with no basis in the actual history).
        var result = ClaudeQuizProvider.BuildFormatHintClause(new[] { true, false, true, false });

        Assert.Equal("", result);
    }

    [Fact]
    public void BuildFormatHintClause_AllMultipleChoice_NudgesTowardShortAnswer()
    {
        var result = ClaudeQuizProvider.BuildFormatHintClause(new[] { true, true, true, true, true });

        Assert.Contains("mostly been multiple-choice", result);
    }
}
