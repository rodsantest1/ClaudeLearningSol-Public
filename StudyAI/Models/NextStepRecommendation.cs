namespace StudyAI.Models;

// The "output" side of the routing decision: what IQuizPlanner recommends
// studying next, and why. Difficulty is a free-form string rather than an
// enum so the prompt and ClaudeRecommendationParser can stay in lockstep
// without a shared type crossing the API boundary — same choice
// QuizQuestion.Difficulty makes.
//
// InputTokens/OutputTokens are optional trailing fields, same trick as on
// QuizQuestion/GradeResult — a recommendation is still a real API call, so it
// belongs in the session-wide cost total or that total quietly undercounts
// (Model Selection and Optimization, CCDV-F, 16.8%).
public record NextStepRecommendation(
    string Topic,
    string Difficulty,
    string Reason,
    int? InputTokens = null,
    int? OutputTokens = null);
