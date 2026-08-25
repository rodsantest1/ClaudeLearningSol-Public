namespace StudyAI.Models;

// CorrectAnswer holds the literal text of the right choice (not an index) so that
// grading only ever needs a string comparison — this keeps the shape identical
// whether the answer is checked by hand (iteration 1) or by an AI (iteration 2+).
//
// InputTokens/OutputTokens/Difficulty are optional and trail the record on
// purpose — same trick as IQuizProvider's `model` parameter. They default to
// null so StaticQuizProvider's existing constructors (a hardcoded bank has no
// concept of token usage or requested difficulty) didn't need to change at
// all. ClaudeQuizProvider fills InputTokens/OutputTokens in from the API
// response's usage field (Model Selection and Optimization, CCDV-F, 16.8%:
// token counts are what "cost" actually means), and tags Difficulty with
// whatever level was requested — see ClaudeQuizPlanner, which is what
// actually decides that level (Agents and Workflows, 14.7%).
// WasTruncated defaults to false for the same reason InputTokens/OutputTokens/
// Difficulty do — StaticQuizProvider's real questions and every existing
// ErrorQuestion call site don't need to know about it. ClaudeQuizProvider sets
// it true only on the one specific failure sentinel that's actually
// retryable: a max_tokens truncation. Living on QuizQuestion itself (rather
// than a separate return type, or the caller string-matching on Explanation's
// text) keeps GetQuestionAsync's contract as "always returns a QuizQuestion"
// while still giving TimedQuiz.razor a typed signal to decide whether to
// retry with a bigger budget — see TimedQuizLogic.BumpMaxTokens.
public record QuizQuestion(
    string Topic,
    string Prompt,
    IReadOnlyList<string> Choices,
    string CorrectAnswer,
    string Explanation,
    int? InputTokens = null,
    int? OutputTokens = null,
    string? Difficulty = null,
    bool WasTruncated = false);
