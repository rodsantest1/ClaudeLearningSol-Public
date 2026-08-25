namespace StudyAI.Models;

// Eval, Testing, and Debugging (CCDV-F, 2.6%): before this existed,
// GradeResult only had IsCorrect, so "graded as incorrect" and "grading
// failed" (missing/truncated tool call, unparseable response, no API key,
// API error) were indistinguishable — both came back as IsCorrect = false,
// and a failure silently counted against the user's score. GradeOutcome
// makes the three real cases explicit; see GradeResult.Outcome for how it's
// derived.
public enum GradeOutcome
{
    Correct,
    Incorrect,
    GradingFailed,
}
