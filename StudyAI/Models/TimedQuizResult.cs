namespace StudyAI.Models;

// Represents the result of a completed timed quiz.
// Tracks correct answers, time, and which questions were marked for review.
public record TimedQuizResult
{
    public int TotalQuestions { get; init; }
    public int CorrectAnswers { get; init; }
    public long TotalTimeMs { get; init; }
    public List<QuestionAttempt> Attempts { get; init; } = new();
    public List<int> MarkedForReview { get; init; } = new(); // Question indices
    public int? CorrectByEasy { get; init; }
    public int? CorrectByMedium { get; init; }
    public int? CorrectByHard { get; init; }
    public int? TotalByEasy { get; init; }
    public int? TotalByMedium { get; init; }
    public int? TotalByHard { get; init; }
    
    public double PercentCorrect => TotalQuestions > 0 ? (double)CorrectAnswers / TotalQuestions * 100 : 0;
    public double AverageTimePerQuestionMs => TotalQuestions > 0 ? (double)TotalTimeMs / TotalQuestions : 0;
}

// Represents a single question attempt in a timed quiz.
//
// Grade is nullable to support deferred grading: a question marked for
// review is submitted (UserAnswer captured, added to Attempts) but not
// graded yet — Grade stays null until the student finishes editing it on
// the review screen and clicks "Finish Quiz", at which point TimedQuiz.razor
// grades it and replaces this record with Grade populated. Every attempt
// is expected to have a non-null Grade by the time TimedQuizResult is built.
public record QuestionAttempt
{
    public int QuestionNumber { get; init; }
    public QuizQuestion Question { get; init; } = null!;
    public string UserAnswer { get; init; } = "";
    public GradeResult? Grade { get; init; }
    public long TimeSpentMs { get; init; }
    public bool MarkedForReview { get; init; }
}
