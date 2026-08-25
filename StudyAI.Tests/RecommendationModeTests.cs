using StudyAI.Models;
using Xunit;

namespace StudyAI.Tests;

// Covers the RecommendationMode enum and its role in controlling routing behavior.
// Demonstrates that the enum values are well-defined, round-trip correctly through
// Quiz.razor's nameof() string storage, and can be parsed back reliably. This is
// critical because Quiz.razor stores the mode as a string in a dropdown and parses
// it back with Enum.Parse<T> in the Recommend() method.
//
// CCDV-F domains:
//   - Eval, Testing, and Debugging (2.6%) — this test suite.
//   - Agents and Workflows (14.7%) — enum values control how routing decisions
//     are applied (Auto vs. StayOnTopic vs. Disabled).
//   - Prompt and Context Engineering (11.0%) — different modes result in different
//     input data fed to IQuizPlanner, demonstrating that workflows adapt based on
//     what callers provide.
public class RecommendationModeTests
{
    [Fact]
    public void RecommendationMode_HasThreeValues()
    {
        var values = Enum.GetValues(typeof(RecommendationMode));
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void RecommendationMode_DefaultIsStayOnTopic()
    {
        Assert.Equal(RecommendationMode.StayOnTopic, (RecommendationMode)0);
    }

    [Fact]
    public void RecommendationMode_CanParseFromString()
    {
        Assert.Equal(RecommendationMode.Auto, Enum.Parse<RecommendationMode>(nameof(RecommendationMode.Auto)));
        Assert.Equal(RecommendationMode.StayOnTopic, Enum.Parse<RecommendationMode>(nameof(RecommendationMode.StayOnTopic)));
        Assert.Equal(RecommendationMode.Disabled, Enum.Parse<RecommendationMode>(nameof(RecommendationMode.Disabled)));
    }

    [Fact]
    public void RecommendationMode_HasCorrectValues()
    {
        Assert.Equal(0, (int)RecommendationMode.StayOnTopic);
        Assert.Equal(1, (int)RecommendationMode.Disabled);
        Assert.Equal(2, (int)RecommendationMode.Auto);
    }

    [Fact]
    public void RecommendationMode_NameofRoundTrips()
    {
        // Quiz.razor uses nameof() to store/retrieve mode via a dropdown.
        // Verify the round-trip: enum -> nameof string -> parse back works.
        var mode = RecommendationMode.StayOnTopic;
        var nameofString = nameof(RecommendationMode.StayOnTopic);
        var parsed = Enum.Parse<RecommendationMode>(nameofString);
        Assert.Equal(mode, parsed);
    }

    [Theory]
    [InlineData(nameof(RecommendationMode.Auto), RecommendationMode.Auto)]
    [InlineData(nameof(RecommendationMode.StayOnTopic), RecommendationMode.StayOnTopic)]
    [InlineData(nameof(RecommendationMode.Disabled), RecommendationMode.Disabled)]
    public void RecommendationMode_AllModesRoundTripCorrectly(string nameofString, RecommendationMode expected)
    {
        var parsed = Enum.Parse<RecommendationMode>(nameofString);
        Assert.Equal(expected, parsed);
    }
}
