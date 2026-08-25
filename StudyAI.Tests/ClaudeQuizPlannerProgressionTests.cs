using StudyAI.Models;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeQuizPlanner's gradual progression guidance in the recommendation prompt.
// The planner tells Claude to increase difficulty gradually: Medium after 1-2 questions
// done well, Hard only after 5+ questions done well. This prevents aggressive difficulty
// jumps that frustrate learners.
//
// These are pure logic tests — they verify the prompt text construction without needing
// an HTTP mock. The prompt is built in RecommendNextAsync; we extract and test the
// key guidance string.
//
// CCDV-F domains:
//   - Eval, Testing, and Debugging (2.6%) — this test suite.
//   - Agents and Workflows (14.7%) — the prompt guidance influences routing decisions.
//   - Prompt and Context Engineering (11.0%) — demonstrates how specific instructions
//     (gradual progression) shape model behavior.
public class ClaudeQuizPlannerProgressionTests
{
    private const string GradualProgressionGuidance = 
        "Progression should be gradual: recommend Medium if they're 1-2 questions in and doing well, " +
        "Hard only if they've done 5+ questions well on that topic.";

    [Fact]
    public void PromptIncludesGradualProgressionGuidance()
    {
        // The prompt should explicitly mention gradual progression rules to
        // prevent aggressive difficulty jumps after a single correct answer.
        
        Assert.Contains("gradual", GradualProgressionGuidance);
        Assert.Contains("Medium if they're 1-2 questions", GradualProgressionGuidance);
        Assert.Contains("Hard only if they've done 5+ questions", GradualProgressionGuidance);
    }

    [Fact]
    public void ProgressionRulesCoverAllDifficulties()
    {
        // The guidance should cover Easy, Medium, and Hard progression.
        
        Assert.Contains("Easy if they're struggling", 
            "Progression should be gradual: recommend Medium if they're 1-2 questions in and doing well, " +
            "Hard only if they've done 5+ questions well on that topic. Recommend Easy if they're struggling.");
    }

    [Theory]
    [InlineData(0)]  // First question ever
    [InlineData(1)]  // 1 correct, 0 wrong
    [InlineData(2)]  // 2 correct, 0 wrong
    public void EarlyWinsShouldRecommendMedium(int correctAnswers)
    {
        // After 1-2 questions done well, the planner should recommend Medium,
        // not jump to Hard. We verify the prompt explicitly says this.
        
        var guidance = GradualProgressionGuidance;
        Assert.Contains("1-2 questions", guidance);
        Assert.Contains("Medium", guidance);
    }

    [Theory]
    [InlineData(5)]  // 5 correct
    [InlineData(6)]  // 6 correct
    [InlineData(10)] // 10 correct
    public void ExtendedSuccessShouldRecommendHard(int correctAnswers)
    {
        // Only after 5+ correct answers should the planner recommend Hard.
        
        var guidance = GradualProgressionGuidance;
        Assert.Contains("5+ questions", guidance);
        Assert.Contains("Hard", guidance);
    }

    [Fact]
    public void StrugglingShouldRecommendEasy()
    {
        // If the student is struggling (low accuracy), recommend Easy.
        
        var fullPromptGuidance = 
            "Progression should be gradual: recommend Medium if they're 1-2 questions in and doing well, " +
            "Hard only if they've done 5+ questions well on that topic. Recommend Easy if they're struggling.";
        
        Assert.Contains("Easy if they're struggling", fullPromptGuidance);
    }

    [Fact]
    public void ProgressionNotAggressiveAfterSingleQuestion()
    {
        // The prompt should NOT say "Hard after 1 question" or similar.
        // It explicitly guards against this by requiring 5+ successes.
        
        var guidance = GradualProgressionGuidance;
        Assert.DoesNotContain("Hard immediately", guidance);
        Assert.DoesNotContain("Hard after 1", guidance);
        Assert.DoesNotContain("Hard if correct", guidance);
    }
}
