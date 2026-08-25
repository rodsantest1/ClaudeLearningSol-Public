namespace StudyAI.Models;

// Represents configuration for a timed quiz session.
// Used to control quiz behavior: how many questions, difficulty filtering,
// time limits, and topic selection.
public record TimedQuizConfig
{
    public int NumQuestions { get; init; } = 10;
    public string DifficultyFilter { get; init; } = "Any"; // Any, Easy, Medium, Hard, Mixed
    public int? TimePerQuestionSeconds { get; init; } = null; // null = no limit
    public int? TotalTimeSeconds { get; init; } = null; // null = no limit
    public string Topic { get; init; } = ""; // Empty = random across all
    public QuestionFormat PreferredFormat { get; init; } = QuestionFormat.Any;
    
    // Factory method for quick-start defaults
    public static TimedQuizConfig QuickStart() => new()
    {
        NumQuestions = 10,
        DifficultyFilter = "Mixed",
        TimePerQuestionSeconds = null,
        TotalTimeSeconds = null,
        Topic = "",
        PreferredFormat = QuestionFormat.Any
    };
}
