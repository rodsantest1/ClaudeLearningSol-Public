namespace StudyAI.Models;

// Represents how the quiz handles "Suggested Next" recommendations after grading.
// Allows users to control whether Claude routes them to new topics or keeps them
// focused on the current topic.
public enum RecommendationMode
{
    StayOnTopic = 0,    // Claude only adjusts difficulty, keeps the same topic
    Disabled = 1,        // No recommendations — user must manually pick next question
    Auto = 2       // Default — Claude recommends topic and difficulty based on performance
}
