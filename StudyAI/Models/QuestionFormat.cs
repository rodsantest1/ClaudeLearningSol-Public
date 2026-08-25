namespace StudyAI.Models;

// Represents the user's preference for question types during a quiz session.
// Allows constraining Claude to generate only multiple-choice, only short-answer,
// or either format (the default, matching iteration 5's "tool_choice: auto" behavior).
public enum QuestionFormat
{
    Any = 0,              // Default — Claude picks the best format per question
    MultipleChoice = 1,   // Only MCQ format
    ShortAnswer = 2       // Only free-text format
}
