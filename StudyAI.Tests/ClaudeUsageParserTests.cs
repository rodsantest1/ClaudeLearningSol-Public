using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// No network, no mocking — same pattern as the other Claude*Parser tests.
// CCDV-F domain: Model Selection and Optimization (16.8%), since token counts
// are what "cost" actually means; Eval, Testing, and Debugging (2.6%) for the
// pattern itself.
public class ClaudeUsageParserTests
{
    [Fact]
    public void Parse_ValidUsage_ReturnsBothCounts()
    {
        const string body = "{\"usage\":{\"input_tokens\":120,\"output_tokens\":45},\"content\":[]}";

        var (input, output) = ClaudeUsageParser.Parse(body);

        Assert.Equal(120, input);
        Assert.Equal(45, output);
    }

    [Fact]
    public void Parse_MissingUsageField_ReturnsNulls()
    {
        const string body = "{\"content\":[]}";

        var (input, output) = ClaudeUsageParser.Parse(body);

        Assert.Null(input);
        Assert.Null(output);
    }

    [Fact]
    public void Parse_PartialUsageObject_ReturnsWhatItCanAndNullForTheRest()
    {
        const string body = "{\"usage\":{\"input_tokens\":80},\"content\":[]}";

        var (input, output) = ClaudeUsageParser.Parse(body);

        Assert.Equal(80, input);
        Assert.Null(output);
    }

    [Fact]
    public void Parse_MalformedBody_ReturnsNullsInsteadOfThrowing()
    {
        const string body = "not json at all";

        var (input, output) = ClaudeUsageParser.Parse(body);

        Assert.Null(input);
        Assert.Null(output);
    }
}
