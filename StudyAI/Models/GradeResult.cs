namespace StudyAI.Models;

// See QuizQuestion for why InputTokens/OutputTokens are optional trailing
// fields rather than required ones.
//
// GradingFailed (added after iteration 4) is the same additive trick again:
// a new optional trailing bool, defaulted to false, so every existing
// `new GradeResult(isCorrect, explanation)` call site — success paths in
// ClaudeGradeParser and StaticQuizProvider — keeps compiling and behaving
// exactly as before. Only the failure paths (missing API key, API error,
// unparseable/truncated tool call) pass GradingFailed: true. Outcome is a
// computed property rather than a stored field so there's exactly one place
// (here) where "correct vs incorrect vs failed" gets decided, instead of
// every caller re-deriving it from IsCorrect + GradingFailed by hand.
public record GradeResult(bool IsCorrect, string Explanation, int? InputTokens = null, int? OutputTokens = null, bool GradingFailed = false)
{
    public GradeOutcome Outcome =>
        GradingFailed ? GradeOutcome.GradingFailed
        : IsCorrect ? GradeOutcome.Correct
        : GradeOutcome.Incorrect;
}
