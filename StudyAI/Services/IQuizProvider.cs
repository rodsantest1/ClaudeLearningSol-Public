using StudyAI.Models;

namespace StudyAI.Services;

// The seam between the UI and "however questions get generated and graded."
// Iteration 1 implements this with a hardcoded question bank. A later iteration
// swaps in an implementation backed by the Claude API — the Quiz page and this
// interface don't change at all when that happens.
//
// The optional `model` and `difficulty` parameters below are the exceptions to
// that stability promise. Iterations 1 through 3 only ever changed what
// happened *behind* the interface; "which model to use" and "how hard should
// the question be" are genuinely new inputs that didn't exist as concepts
// before, so the interface itself had to grow to carry them. Both are
// additive (optional, trailing, defaulted), so nothing that already called
// this interface had to change. StaticQuizProvider ignores both (a hardcoded
// bank has no model and no adjustable difficulty). Model Selection and
// Optimization (CCDV-F, 16.8%) is the domain `model` exists to demonstrate —
// the point isn't the parameter, it's letting you feel the cost/speed/quality
// tradeoff between model tiers instead of only reading about it. `difficulty`
// exists so IQuizPlanner (Agents and Workflows, 14.7%) has something concrete
// to control, not just a label with no effect.
//
// `recentPrompts` (added after iteration 4) is the same kind of additive,
// optional growth: a stateless "write one question about X" prompt has no
// memory of what it already asked, so Claude tends to converge on the same
// obvious question for a given topic/difficulty. The caller now gets to pass
// recently-seen prompts back in so the implementation can steer away from
// them. StaticQuizProvider ignores it (a fixed bank has nothing to steer).
//
// `recentWasMultipleChoice` (added after iteration 5) grew the interface the
// same way again, for a symptom found the same way `recentPrompts` was: a
// qualitative instruction in the prompt ("prefer short-answer unless
// multiple-choice earns its place") swung `tool_choice: "auto"` from nearly
// always multiple-choice to nearly always short-answer in manual testing —
// wording alone is too blunt an instrument, because the prompt is stateless
// and has no feedback loop. Passing back the actual recent format history
// lets ClaudeQuizProvider give Claude something concrete to correct against
// instead of a static preference. StaticQuizProvider ignores it, same as
// `recentPrompts`.
//
// `preferredFormat` (added after iteration 5) is the same additive growth
// pattern: lets the caller constrain question types to only multiple-choice,
// only short-answer, or either (the default, matching iteration 5's
// tool_choice: "auto" behavior). Affects which tools are offered to Claude —
// QuestionFormat.Any offers both tools with tool_choice: "auto",
// QuestionFormat.MultipleChoice forces only the MCQ tool, QuestionFormat.ShortAnswer
// forces only the short-answer tool. This demonstrates Tools and MCPs
// (10.6%, CCDV-F): showing how tool selection (which tools to offer) is
// itself a lever Claude Code and prompt engineering can control,
// not just how tools are defined.
// `maxTokens` (added after iteration 6) is the same additive growth pattern
// once more: lets a caller that's already hit a max_tokens truncation on this
// question (see QuizQuestion.WasTruncated) ask again with a bigger output
// budget instead of every retry repeating the exact same failure. 700 matches
// ClaudeQuizProvider's own default, so passing nothing behaves exactly as it
// did before this parameter existed. StaticQuizProvider ignores it, same as
// the others — a hardcoded bank has no output budget to speak of.
public interface IQuizProvider
{
    Task<QuizQuestion> GetQuestionAsync(string topic, string? model = null, string? difficulty = null, IReadOnlyList<string>? recentPrompts = null, IReadOnlyList<bool>? recentWasMultipleChoice = null, QuestionFormat preferredFormat = QuestionFormat.Any, int maxTokens = 700);

    Task<GradeResult> GradeAsync(QuizQuestion question, string userAnswer, string? model = null);
}
