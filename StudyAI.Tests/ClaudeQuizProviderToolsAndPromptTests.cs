using StudyAI.Models;
using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeQuizProvider.BuildToolsAndPrompt — the logic that adapts
// which tools are offered to Claude based on the user's question format
// preference. This is pure logic (no HttpClient, no API calls) so it's
// directly testable and clearly visible as the only place where tool
// selection happens.
//
// CCDV-F domains:
//   - Eval, Testing, and Debugging (2.6%) — this test suite itself.
//   - Tools and MCPs (10.6%) — the method being tested, demonstrating that
//     tool selection (which tools to offer) is a control lever.
//   - Prompt and Context Engineering (11.0%) — the user message varies based
//     on preferredFormat.
public class ClaudeQuizProviderToolsAndPromptTests
{
    [Fact]
    public void BuildToolsAndPrompt_FormatAny_OffersBothTools()
    {
        var (tools, toolChoice, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.Any,
            null,
            null);

        Assert.Equal(2, tools.Length);
        Assert.Contains("auto", toolChoice.ToString() ?? "");
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatAny_UserMessageSaysChooseFormat()
    {
        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.Any,
            null,
            null);

        Assert.Contains("genuinely fits the question best", userMessage);
        Assert.Contains("don't default to one over the other", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatMultipleChoice_OffersOnlyMCQTool()
    {
        var (tools, toolChoice, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.MultipleChoice,
            null,
            null);

        Assert.Single(tools);
        var toolJson = tools[0].ToString();
        Assert.Contains("multiple_choice", toolJson ?? "");
        Assert.Contains("tool", toolChoice.ToString() ?? "");
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatMultipleChoice_UserMessageForcesFormat()
    {
        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.MultipleChoice,
            null,
            null);

        Assert.Contains("multiple-choice question", userMessage);
        Assert.Contains("must create", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatShortAnswer_OffersOnlyShortAnswerTool()
    {
        var (tools, toolChoice, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.ShortAnswer,
            null,
            null);

        Assert.Single(tools);
        var toolJson = tools[0].ToString();
        Assert.Contains("short_answer", toolJson ?? "");
        Assert.Contains("tool", toolChoice.ToString() ?? "");
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatShortAnswer_UserMessageForcesFormat()
    {
        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.ShortAnswer,
            null,
            null);

        Assert.Contains("short-answer", userMessage);
        Assert.Contains("must create", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_UserMessageIncludesTopic()
    {
        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "Web Fundamentals",
            "Medium",
            QuestionFormat.Any,
            null,
            null);

        Assert.Contains("Web Fundamentals", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_UserMessageIncludesDifficulty()
    {
        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Hard",
            QuestionFormat.Any,
            null,
            null);

        Assert.Contains("hard-difficulty", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_UserMessageIncludesAvoidClauseWhenPromptsProvided()
    {
        var recentPrompts = new[] { "Old question one?", "Old question two?" };

        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.Any,
            recentPrompts,
            null);

        Assert.Contains("Do not repeat or closely rephrase", userMessage);
        Assert.Contains("Old question one?", userMessage);
        Assert.Contains("Old question two?", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatAny_IncludesFormatHintWhenFormatsProvided()
    {
        var recentFormats = new[] { true, true, true, false, false }; // 3 MCQ, 2 short-answer

        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.Any,
            null,
            recentFormats);

        Assert.Contains("mostly been multiple-choice", userMessage);
        Assert.Contains("Use short-answer this time", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatMultipleChoice_NoFormatHintEvenIfFormatsProvided()
    {
        // When format is forced, the format hint shouldn't appear because
        // there's no choice to make — the caller already decided.
        var recentFormats = new[] { true, true, true, false, false };

        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.MultipleChoice,
            null,
            recentFormats);

        // Should have the forced format instruction
        Assert.Contains("multiple-choice question", userMessage);
        // But should NOT have the nudge about recent history
        Assert.DoesNotContain("mostly been", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatShortAnswer_NoFormatHintEvenIfFormatsProvided()
    {
        var recentFormats = new[] { true, true, true, false, false };

        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.ShortAnswer,
            null,
            recentFormats);

        Assert.Contains("short-answer", userMessage);
        Assert.DoesNotContain("mostly been", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_FormatAny_BalancedFormatsNoHint()
    {
        // When recent history is evenly split, no nudge is needed
        var recentFormats = new[] { true, true, false, false };

        var (_, _, userMessage) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.Any,
            null,
            recentFormats);

        Assert.DoesNotContain("mostly been", userMessage);
    }

    [Fact]
    public void BuildToolsAndPrompt_ToolSchemasHaveCorrectNames()
    {
        var (tools, _, _) = ClaudeQuizProvider.BuildToolsAndPrompt(
            "C# Basics",
            "Medium",
            QuestionFormat.Any,
            null,
            null);

        var toolsJson = tools
            .Select(t => t.ToString() ?? "")
            .OrderBy(t => t)
            .ToList();

        Assert.Contains("create_multiple_choice_question", toolsJson[0]);
        Assert.Contains("create_short_answer_question", toolsJson[1]);
    }
}
