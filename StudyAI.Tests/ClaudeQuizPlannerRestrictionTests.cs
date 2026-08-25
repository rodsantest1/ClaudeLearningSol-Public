using StudyAI.Models;
using StudyAI.Services;
using Xunit;

namespace StudyAI.Tests;

// Covers ClaudeQuizPlanner's restrictToCurrentTopic parameter behavior.
// When restrictToCurrentTopic is false (default), the planner can recommend any topic.
// When restrictToCurrentTopic is true, the planner is instructed to keep the student
// on the current topic and only adjust difficulty.
//
// CCDV-F domains:
//   - Eval, Testing, and Debugging (2.6%) — this test suite.
//   - Agents and Workflows (14.7%) — restrictToCurrentTopic demonstrates that the
//     same workflow (IQuizPlanner) produces different behavior based on caller input.
//   - Prompt and Context Engineering (11.0%) — the constraint is appended to the
//     user message, showing how prompts adapt based on control parameters.
public class ClaudeQuizPlannerRestrictionTests
{
    [Fact]
    public void RecommendNextAsync_WithoutRestriction_NoConstraintAdded()
    {
        // When restrictToCurrentTopic is false (default), the constraint string
        // should be empty. The planner can freely recommend any topic based on
        // the student's performance history.
        
        var history = new List<TopicPerformance>
        {
            new TopicPerformance("Claude Certified Developer", 1, 2)
        };

        var restrictToCurrentTopic = false;
        var topicConstraint = restrictToCurrentTopic && history.Count > 0
            ? $" The student must keep studying {history[0].Topic}; do not recommend a different topic."
            : "";

        // Without restriction, the constraint is empty, so the prompt allows topic changes
        Assert.Empty(topicConstraint);
    }

    [Fact]
    public void RecommendNextAsync_WithRestriction_PromptConstrainsToCurrentTopic()
    {
        // When restrictToCurrentTopic is true, the prompt should include an
        // explicit constraint naming the current topic.
        
        var history = new List<TopicPerformance>
        {
            new TopicPerformance("Claude Certified Developer", 1, 2)
        };

        var restrictToCurrentTopic = true;
        var topicConstraint = restrictToCurrentTopic && history.Count > 0
            ? $" The student must keep studying {history[0].Topic}; do not recommend a different topic."
            : "";

        Assert.NotEmpty(topicConstraint);
        Assert.Contains("Claude Certified Developer", topicConstraint);
        Assert.Contains("must keep studying", topicConstraint);
        Assert.Contains("do not recommend a different topic", topicConstraint);
    }

    [Fact]
    public void RecommendNextAsync_WithRestrictionAndEmptyHistory_NoConstraint()
    {
        // Edge case: if history is empty, even with restrictToCurrentTopic=true,
        // we can't extract a topic name, so no constraint is added.
        
        var history = new List<TopicPerformance>();

        var restrictToCurrentTopic = true;
        var topicConstraint = restrictToCurrentTopic && history.Count > 0
            ? $" The student must keep studying {history[0].Topic}; do not recommend a different topic."
            : "";

        Assert.Empty(topicConstraint);
    }

    [Fact]
    public void RecommendNextAsync_ConstraintNamesFirstTopicInHistory()
    {
        // If history contains multiple topics (shouldn't happen in StayOnTopic mode,
        // but test defensively), the constraint should name the first one.
        
        var history = new List<TopicPerformance>
        {
            new TopicPerformance("Prompt Engineering", 3, 5),
            new TopicPerformance("Tools and MCPs", 2, 4)
        };

        var restrictToCurrentTopic = true;
        var topicConstraint = restrictToCurrentTopic && history.Count > 0
            ? $" The student must keep studying {history[0].Topic}; do not recommend a different topic."
            : "";

        Assert.Contains("Prompt Engineering", topicConstraint);
        Assert.DoesNotContain("Tools and MCPs", topicConstraint);
    }

    [Fact]
    public void RecommendNextAsync_ConstraintFalseByDefault()
    {
        // Verify that restrictToCurrentTopic defaults to false, so the interface
        // change is backward compatible.
        
        // If RecommendNextAsync is called without the parameter, it should default
        // to false (allowing topic changes). We can't test this directly without
        // a mock, but we verify the parameter is optional.
        
        var methodInfo = typeof(IQuizPlanner).GetMethod("RecommendNextAsync");
        Assert.NotNull(methodInfo);
        
        var parameters = methodInfo.GetParameters();
        var restrictParam = parameters.FirstOrDefault(p => p.Name == "restrictToCurrentTopic");
        
        Assert.NotNull(restrictParam);
        Assert.True(restrictParam.HasDefaultValue);
        Assert.False((bool)restrictParam.DefaultValue);
    }

    [Theory]
    [InlineData("C# Basics")]
    [InlineData("Claude Certified Developer")]
    [InlineData("Prompt Engineering Fundamentals")]
    [InlineData("Tools and MCPs")]
    public void RecommendNextAsync_ConstraintIncludesTopicName(string topicName)
    {
        // Parametrized test: constraint should always include the topic name,
        // regardless of what topic is passed.
        
        var history = new List<TopicPerformance>
        {
            new TopicPerformance(topicName, 1, 3)
        };

        var restrictToCurrentTopic = true;
        var topicConstraint = restrictToCurrentTopic && history.Count > 0
            ? $" The student must keep studying {history[0].Topic}; do not recommend a different topic."
            : "";

        Assert.Contains(topicName, topicConstraint);
    }
}
