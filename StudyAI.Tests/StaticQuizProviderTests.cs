using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// No CCDV-F domain here on purpose — StaticQuizProvider has no Claude/AI code
// in it at all. Included as a baseline: these are the "boring," fully
// deterministic tests that don't need eval-style thinking, for contrast against
// ClaudeResponseParserTests below.
public class StaticQuizProviderTests
{
    [Fact]
    public async Task GetQuestionAsync_KnownTopic_ReturnsMatchingTopic()
    {
        var provider = new StaticQuizProvider();

        var question = await provider.GetQuestionAsync("Databases");

        Assert.Equal("Databases", question.Topic);
    }

    [Fact]
    public async Task GetQuestionAsync_UnknownTopic_FallsBackToFullBank()
    {
        var provider = new StaticQuizProvider();

        var question = await provider.GetQuestionAsync("Quantum Computing");

        Assert.NotNull(question);
        Assert.False(string.IsNullOrWhiteSpace(question.Prompt));
    }

    [Fact]
    public async Task GetQuestionAsync_EmptyTopic_ReturnsAnyQuestion()
    {
        var provider = new StaticQuizProvider();

        var question = await provider.GetQuestionAsync("");

        Assert.NotNull(question);
    }

    [Fact]
    public async Task GetQuestionAsync_WithPreferredFormat_IgnoresItAndReturnsAnyQuestion()
    {
        // StaticQuizProvider has no concept of question format preference —
        // a hardcoded bank is what it is. This test documents that it accepts
        // the parameter (satisfying IQuizProvider) but has no effect on output.
        // Both format-preference calls should return questions from the same pool.
        var provider = new StaticQuizProvider();

        var mcqQuestion = await provider.GetQuestionAsync("C# Basics", preferredFormat: StudyAI.Models.QuestionFormat.MultipleChoice);
        var saQuestion = await provider.GetQuestionAsync("C# Basics", preferredFormat: StudyAI.Models.QuestionFormat.ShortAnswer);
        var anyQuestion = await provider.GetQuestionAsync("C# Basics", preferredFormat: StudyAI.Models.QuestionFormat.Any);

        // All three should be valid questions from the hardcoded bank
        Assert.NotNull(mcqQuestion);
        Assert.NotNull(saQuestion);
        Assert.NotNull(anyQuestion);
        Assert.Equal("C# Basics", mcqQuestion.Topic);
        Assert.Equal("C# Basics", saQuestion.Topic);
        Assert.Equal("C# Basics", anyQuestion.Topic);
    }
}
