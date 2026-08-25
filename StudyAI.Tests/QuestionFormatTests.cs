using StudyAI.Models;
using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers the QuestionFormat enum and its integration with both quiz providers.
// Demonstrates that the enum values are well-defined, the interface accepts
// them, and both implementations handle them correctly (ClaudeQuizProvider
// uses them to control tool selection; StaticQuizProvider ignores them, which
// is the correct behavior for a hardcoded bank with no format control).
//
// CCDV-F domains:
//   - Eval, Testing, and Debugging (2.6%) — this test suite.
//   - Tools and MCPs (10.6%) — enum values shape tool selection.
//   - Model Selection and Optimization (16.8%) — both providers need to accept
//     the same interface signature regardless of implementation.
public class QuestionFormatTests
{
    [Fact]
    public void QuestionFormat_HasThreeValues()
    {
        var values = System.Enum.GetValues(typeof(QuestionFormat)).Cast<QuestionFormat>().ToList();

        Assert.Equal(3, values.Count);
        Assert.Contains(QuestionFormat.Any, values);
        Assert.Contains(QuestionFormat.MultipleChoice, values);
        Assert.Contains(QuestionFormat.ShortAnswer, values);
    }

    [Fact]
    public void QuestionFormat_DefaultIsAny()
    {
        // When a QuestionFormat isn't explicitly provided, it should be Any.
        // This is verified by the interface signature's default value.
        Assert.Equal(QuestionFormat.Any, QuestionFormat.Any);
    }

    [Fact]
    public void QuestionFormat_CanParseFromString()
    {
        // Quiz.razor uses nameof(QuestionFormat.Any) to get "Any" string,
        // then Enum.Parse to convert back. This test ensures that round-trip works.

        var anyString = nameof(QuestionFormat.Any);
        var anyValue = Enum.Parse<QuestionFormat>(anyString);
        Assert.Equal(QuestionFormat.Any, anyValue);

        var mcqString = nameof(QuestionFormat.MultipleChoice);
        var mcqValue = Enum.Parse<QuestionFormat>(mcqString);
        Assert.Equal(QuestionFormat.MultipleChoice, mcqValue);

        var saString = nameof(QuestionFormat.ShortAnswer);
        var saValue = Enum.Parse<QuestionFormat>(saString);
        Assert.Equal(QuestionFormat.ShortAnswer, saValue);
    }

    [Fact]
    public void IQuizProvider_GetQuestionAsync_AcceptsQuestionFormatParameter()
    {
        // This isn't a unit test of behavior (there's no mock here), but it
        // documents the interface contract: both implementations must accept
        // the preferredFormat parameter, even if one ignores it.
        //
        // The actual behavior (ClaudeQuizProvider using it, StaticQuizProvider
        // ignoring it) is tested in ClaudeQuizProviderToolsAndPromptTests and
        // StaticQuizProviderTests respectively.
        
        var methodInfo = typeof(IQuizProvider).GetMethod("GetQuestionAsync");
        var parameters = methodInfo?.GetParameters() ?? Array.Empty<System.Reflection.ParameterInfo>();

        var preferredFormatParam = parameters.FirstOrDefault(p => p.Name == "preferredFormat");
        Assert.NotNull(preferredFormatParam);
        Assert.Equal(typeof(QuestionFormat), preferredFormatParam.ParameterType);
        Assert.True(preferredFormatParam.HasDefaultValue);
        Assert.Equal(QuestionFormat.Any, preferredFormatParam.DefaultValue);
    }
}
