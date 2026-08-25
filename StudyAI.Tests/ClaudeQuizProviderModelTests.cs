using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeQuizProvider.ResolveModel — the one line of logic behind the
// model picker (Quiz.razor's Haiku/Sonnet dropdown). This didn't exist as a
// test until it was pointed out that adding an optional `model` parameter to
// IQuizProvider compiled cleanly and broke nothing, which sounds reassuring
// but actually meant nothing was asserting the new parameter did anything at
// all. Pulling the resolution logic into its own pure method makes that
// checkable without HttpClient or config.
//
// CCDV-F domain: Eval, Testing, and Debugging (2.6%); the resolution logic
// itself belongs to Model Selection and Optimization (16.8%).
public class ClaudeQuizProviderModelTests
{
    [Fact]
    public void ResolveModel_RequestedModelGiven_WinsOverConfig()
    {
        var result = ClaudeQuizProvider.ResolveModel("claude-haiku-4-5-20251001", "claude-sonnet-4-5");

        Assert.Equal("claude-haiku-4-5-20251001", result);
    }

    [Fact]
    public void ResolveModel_NoRequestedModel_FallsBackToConfig()
    {
        var result = ClaudeQuizProvider.ResolveModel(null, "claude-sonnet-4-5");

        Assert.Equal("claude-sonnet-4-5", result);
    }

    [Fact]
    public void ResolveModel_NothingProvided_FallsBackToHardcodedDefault()
    {
        var result = ClaudeQuizProvider.ResolveModel(null, null);

        Assert.Equal("claude-sonnet-4-5", result);
    }

    [Fact]
    public void ResolveModel_RequestedModelIsWhitespace_StillWinsOverConfig()
    {
        // Not sanitized on purpose — Quiz.razor's dropdown only ever sends a
        // known-good value, so ResolveModel trusts its caller. Documented here
        // so that assumption is visible instead of silent.
        var result = ClaudeQuizProvider.ResolveModel("  ", "claude-sonnet-4-5");

        Assert.Equal("  ", result);
    }
}
