using Xunit;
using StudyAI.Models;

namespace StudyAI.Tests;

/// <summary>
/// Unit tests for TimedQuizResult model.
/// Tests score calculation, time tracking, and difficulty breakdown.
/// </summary>
public class TimedQuizResultTests
{
    [Fact]
    public void PercentCorrect_CalculatesCorrectly_AllCorrect()
    {
        // Arrange
        var result = new TimedQuizResult
        {
            TotalQuestions = 10,
            CorrectAnswers = 10,
            TotalTimeMs = 50000
        };

        // Act & Assert
        Assert.Equal(100, result.PercentCorrect);
    }

    [Fact]
    public void PercentCorrect_CalculatesCorrectly_PartialCredit()
    {
        // Arrange
        var result = new TimedQuizResult
        {
            TotalQuestions = 10,
            CorrectAnswers = 7,
            TotalTimeMs = 50000
        };

        // Act & Assert
        Assert.Equal(70, result.PercentCorrect);
    }

    [Fact]
    public void PercentCorrect_CalculatesCorrectly_NoCorrect()
    {
        // Arrange
        var result = new TimedQuizResult
        {
            TotalQuestions = 10,
            CorrectAnswers = 0,
            TotalTimeMs = 50000
        };

        // Act & Assert
        Assert.Equal(0, result.PercentCorrect);
    }

    [Fact]
    public void PercentCorrect_ReturnsZero_WhenZeroQuestions()
    {
        // Arrange
        var result = new TimedQuizResult
        {
            TotalQuestions = 0,
            CorrectAnswers = 0,
            TotalTimeMs = 0
        };

        // Act & Assert
        Assert.Equal(0, result.PercentCorrect);
    }

    [Fact]
    public void AverageTimePerQuestionMs_CalculatesCorrectly()
    {
        // Arrange
        var result = new TimedQuizResult
        {
            TotalQuestions = 10,
            CorrectAnswers = 8,
            TotalTimeMs = 50000 // 50 seconds = 50000ms
        };

        // Act & Assert
        Assert.Equal(5000, result.AverageTimePerQuestionMs); // 50000 / 10 = 5000ms
    }

    [Fact]
    public void AverageTimePerQuestionMs_ReturnsZero_WhenZeroQuestions()
    {
        // Arrange
        var result = new TimedQuizResult
        {
            TotalQuestions = 0,
            CorrectAnswers = 0,
            TotalTimeMs = 0
        };

        // Act & Assert
        Assert.Equal(0, result.AverageTimePerQuestionMs);
    }

    [Fact]
    public void CanTrackDifficultyBreakdown_Easy()
    {
        // Arrange & Act
        var result = new TimedQuizResult
        {
            TotalQuestions = 10,
            CorrectAnswers = 8,
            TotalTimeMs = 60000,
            CorrectByEasy = 3,
            TotalByEasy = 3,
            CorrectByMedium = 3,
            TotalByMedium = 4,
            CorrectByHard = 2,
            TotalByHard = 3
        };

        // Assert
        Assert.Equal(3, result.CorrectByEasy);
        Assert.Equal(3, result.TotalByEasy);
        Assert.Equal(100, (double)result.CorrectByEasy.Value / result.TotalByEasy.Value * 100);
    }

    [Fact]
    public void CanTrackDifficultyBreakdown_Medium()
    {
        // Arrange & Act
        var result = new TimedQuizResult
        {
            CorrectByMedium = 3,
            TotalByMedium = 4
        };

        // Assert
        Assert.Equal(3, result.CorrectByMedium);
        Assert.Equal(4, result.TotalByMedium);
    }

    [Fact]
    public void CanTrackDifficultyBreakdown_Hard()
    {
        // Arrange & Act
        var result = new TimedQuizResult
        {
            CorrectByHard = 1,
            TotalByHard = 3
        };

        // Assert
        Assert.Equal(1, result.CorrectByHard);
        Assert.Equal(3, result.TotalByHard);
    }

    [Fact]
    public void MarkedForReview_TracksQuestionIndices()
    {
        // Arrange & Act
        var result = new TimedQuizResult
        {
            MarkedForReview = new List<int> { 2, 5, 7 }
        };

        // Assert
        Assert.Equal(3, result.MarkedForReview.Count);
        Assert.Contains(2, result.MarkedForReview);
        Assert.Contains(5, result.MarkedForReview);
        Assert.Contains(7, result.MarkedForReview);
    }

    [Fact]
    public void Attempts_TracksIndividualQuestionAttempts()
    {
        // Arrange
        var mockGrade = new GradeResult(IsCorrect: true, Explanation: "Good job");
        var mockQuestion = new QuizQuestion(
            Topic: "Claude API",
            Prompt: "Test Q",
            Choices: new List<string> { "A", "B", "C" },
            CorrectAnswer: "A",
            Explanation: "Correct!",
            Difficulty: "Easy"
        );

        var attempt = new QuestionAttempt
        {
            QuestionNumber = 1,
            Question = mockQuestion,
            UserAnswer = "A",
            Grade = mockGrade,
            TimeSpentMs = 5000,
            MarkedForReview = false
        };

        var result = new TimedQuizResult
        {
            TotalQuestions = 1,
            CorrectAnswers = 1,
            TotalTimeMs = 5000,
            Attempts = new List<QuestionAttempt> { attempt }
        };

        // Act & Assert
        Assert.Single(result.Attempts);
        Assert.Equal(1, result.Attempts[0].QuestionNumber);
        Assert.True(result.Attempts[0].Grade.IsCorrect);
        Assert.Equal(5000, result.Attempts[0].TimeSpentMs);
    }

    [Fact]
    public void CompleteScenario_MixedDifficulty_PartialCredit()
    {
        // Arrange - Simulate a mixed difficulty quiz with 10 questions, 7 correct
        var result = new TimedQuizResult
        {
            TotalQuestions = 10,
            CorrectAnswers = 7,
            TotalTimeMs = 120000, // 2 minutes
            CorrectByEasy = 3,    // 3/3 on Easy
            TotalByEasy = 3,
            CorrectByMedium = 3,  // 3/4 on Medium
            TotalByMedium = 4,
            CorrectByHard = 1,    // 1/3 on Hard
            TotalByHard = 3,
            MarkedForReview = new List<int> { 5, 8 }
        };

        // Act & Assert
        Assert.Equal(70, result.PercentCorrect);
        Assert.Equal(12000, result.AverageTimePerQuestionMs);
        Assert.Equal(2, result.MarkedForReview.Count);
    }
}
