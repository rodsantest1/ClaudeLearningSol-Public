using Microsoft.Extensions.Configuration;
using StudyAI.Models;
using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Through iteration 2 this was a true shared-contract suite: both providers
// graded with an identical string compare, so one [Theory] body ran against
// both via [MemberData]. Iteration 3 breaks that on purpose — ClaudeQuizProvider
// now grades free-text answers with a forced tool call instead of matching a
// string, so "same input, same output" is no longer true between the two
// implementations. That's not a regression; it's the whole point of the swap.
//
// StaticQuizProvider keeps the old deterministic behavior below. ClaudeQuizProvider's
// fail-closed path (missing API key) is still deterministic without a network
// call, so that's tested separately here. The response-parsing logic — including
// what happens if the tool call comes back truncated or malformed — is covered
// in ClaudeGradeParserTests instead, since that's the part that was actually
// pulled out to be testable. The live HTTP round trip itself (SendAsync) still
// isn't covered; that would need mocking HttpClient, a reasonable next step,
// not done yet.
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%).
public class ProviderGradingContractTests
{
    private static readonly QuizQuestion Sample = new(
        "Test Topic", "Which is correct?", new[] { "Alpha", "Beta", "Gamma" }, "Beta", "Beta is correct.");

    [Fact]
    public async Task StaticQuizProvider_ExactMatch_IsCorrect()
    {
        var provider = new StaticQuizProvider();

        var result = await provider.GradeAsync(Sample, "Beta");

        Assert.True(result.IsCorrect);
        Assert.Equal(Sample.Explanation, result.Explanation);
        Assert.Equal(GradeOutcome.Correct, result.Outcome);
    }

    [Fact]
    public async Task StaticQuizProvider_WrongAnswer_IsIncorrect()
    {
        var provider = new StaticQuizProvider();

        var result = await provider.GradeAsync(Sample, "Alpha");

        Assert.False(result.IsCorrect);
        // A genuine wrong answer, not a grading failure — Outcome must
        // distinguish these, or the GradeOutcome fix doesn't hold up: a wrong
        // answer should still count against the score.
        Assert.Equal(GradeOutcome.Incorrect, result.Outcome);
    }

    [Fact]
    public async Task StaticQuizProvider_IsCaseInsensitive()
    {
        var provider = new StaticQuizProvider();

        var result = await provider.GradeAsync(Sample, "beta");

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task ClaudeQuizProvider_NoApiKey_FailsClosedInsteadOfThrowing()
    {
        var provider = new ClaudeQuizProvider(new HttpClient(), new ConfigurationBuilder().Build());

        var result = await provider.GradeAsync(Sample, "Beta");

        Assert.False(result.IsCorrect);
        Assert.Contains("API key", result.Explanation);
        Assert.Equal(GradeOutcome.GradingFailed, result.Outcome);
    }
}
