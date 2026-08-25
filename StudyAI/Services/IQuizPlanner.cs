using StudyAI.Models;

namespace StudyAI.Services;

// Agents and Workflows (CCDV-F, 14.7%): a distinct seam from IQuizProvider on
// purpose. IQuizProvider answers "generate a question" / "grade an answer" —
// content tasks. This answers "what should happen next" — a routing decision,
// Anthropic's term for a workflow step that classifies input and dispatches to
// a specialized next step, instead of a human always picking from a fixed
// dropdown. Keeping it a separate interface (not a third IQuizProvider method)
// makes that distinction visible in the architecture, not just in a comment.
public interface IQuizPlanner
{
    // → baselineDifficulty (added after the initial routing-decision design):
    // the student's own configured difficulty (QuizSettings.DifficultyFilter —
    // "Easy"/"Medium"/"Hard"/"Mixed"), passed through so the recommendation can
    // respect it as the intended level rather than always drifting toward a
    // generic "start at Medium, earn your way to Hard" progression regardless
    // of what the student already chose. Same additive-optional-trailing
    // pattern as every other parameter IQuizProvider/IQuizPlanner have grown —
    // omitting it preserves the original always-start-conservative behavior.
    // See ClaudeQuizPlanner.BuildProgressionGuidance for what it actually does.
    Task<NextStepRecommendation> RecommendNextAsync(IReadOnlyList<TopicPerformance> history, string? model = null, bool restrictToCurrentTopic = false, string? baselineDifficulty = null);
}
