using Xunit;
using StudyAI.Models;

namespace StudyAI.Tests;

/// <summary>
/// Unit tests for TimedQuizConfig model.
/// Tests configuration defaults, factory method, and record behavior.
/// </summary>
public class TimedQuizConfigTests
{
    [Fact]
    public void QuickStart_ReturnsConfigWithDefaultValues()
    {
        // Arrange & Act
        var config = TimedQuizConfig.QuickStart();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(10, config.NumQuestions);
        Assert.Equal("Mixed", config.DifficultyFilter);
        Assert.Null(config.TimePerQuestionSeconds);
        Assert.Null(config.TotalTimeSeconds);
        Assert.Equal("", config.Topic);
        Assert.Equal(QuestionFormat.Any, config.PreferredFormat);
    }

    [Fact]
    public void QuickStart_MultipleCallsProduceSameValues()
    {
        // Arrange & Act
        var config1 = TimedQuizConfig.QuickStart();
        var config2 = TimedQuizConfig.QuickStart();

        // Assert - records support value equality
        Assert.Equal(config1, config2);
    }

    [Fact]
    public void CanCreateConfigWithCustomValues()
    {
        // Arrange & Act
        var config = new TimedQuizConfig
        {
            NumQuestions = 20,
            DifficultyFilter = "Hard",
            TimePerQuestionSeconds = 60,
            TotalTimeSeconds = 1200,
            Topic = "Claude API",
            PreferredFormat = QuestionFormat.ShortAnswer
        };

        // Assert
        Assert.Equal(20, config.NumQuestions);
        Assert.Equal("Hard", config.DifficultyFilter);
        Assert.Equal(60, config.TimePerQuestionSeconds);
        Assert.Equal(1200, config.TotalTimeSeconds);
        Assert.Equal("Claude API", config.Topic);
        Assert.Equal(QuestionFormat.ShortAnswer, config.PreferredFormat);
    }

    [Fact]
    public void WithTopicOnly_CreatesConfigForSpecificTopic()
    {
        // Arrange & Act
        var config = new TimedQuizConfig
        {
            Topic = "Agents and Workflows"
        };

        // Assert
        Assert.Equal("Agents and Workflows", config.Topic);
        Assert.Equal("Any", config.DifficultyFilter); // Default
        Assert.Equal(QuestionFormat.Any, config.PreferredFormat); // Default
    }

    [Fact]
    public void TimePerQuestion_CanBeSetToNull()
    {
        // Arrange & Act
        var config = new TimedQuizConfig
        {
            TimePerQuestionSeconds = null
        };

        // Assert
        Assert.Null(config.TimePerQuestionSeconds);
    }

    [Fact]
    public void TotalTime_CanBeSetToNull()
    {
        // Arrange & Act
        var config = new TimedQuizConfig
        {
            TotalTimeSeconds = null
        };

        // Assert
        Assert.Null(config.TotalTimeSeconds);
    }

    [Fact]
    public void ConfigWithTimePerQuestion_EnforcesPerQuestionLimit()
    {
        // Arrange & Act
        var config = new TimedQuizConfig
        {
            TimePerQuestionSeconds = 45
        };

        // Assert
        Assert.Equal(45, config.TimePerQuestionSeconds);
        // Note: UI should use this to enforce time limit per question
    }
}
