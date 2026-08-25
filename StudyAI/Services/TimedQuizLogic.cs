using StudyAI.Models;

namespace StudyAI.Services;

// Pure, dependency-free decision logic for the timed quiz feature — pulled out
// of TimedQuiz.razor's code-behind so it can be unit tested directly, the same
// way ClaudeQuizProvider.BuildToolsAndPrompt and ClaudeQuizPlanner's prompt
// builders are: no IJSRuntime, no IQuizProvider, no Blazor lifecycle — just
// inputs in, deterministic output out.
public static class TimedQuizLogic
{
    private static readonly string[] MixedDifficulties = { "Easy", "Medium", "Hard" };

    // → Resolve a difficulty filter ("Easy"/"Medium"/"Hard"/"Mixed"/anything else)
    // to the difficulty to request for the next question.
    //
    // randomPicker is an injectable seam: it's called with the number of
    // choices and must return an index in [0, count). Production code leaves
    // it null and gets real randomness (Random.Shared); tests pass a fixed
    // function so "Mixed" selection is deterministic and assertable — the
    // same optional-parameter trick used throughout this codebase to keep a
    // method backward compatible while making it testable.
    public static string GetRandomDifficulty(string filter, Func<int, int>? randomPicker = null)
    {
        var pick = randomPicker ?? (count => Random.Shared.Next(count));

        return filter switch
        {
            "Easy" => "Easy",
            "Medium" => "Medium",
            "Hard" => "Hard",
            "Mixed" => MixedDifficulties[pick(MixedDifficulties.Length)],
            _ => "Medium"
        };
    }

    // → Group completed attempts by difficulty and count correct/total for
    // each bucket. Attempts whose Question.Difficulty is null or not one of
    // Easy/Medium/Hard are simply excluded from all three buckets — they
    // still count toward TimedQuizResult.TotalQuestions/CorrectAnswers,
    // which are computed separately from the full Attempts list.
    //
    // Grade is nullable (deferred grading for marked-for-review questions —
    // see QuestionAttempt), so an ungraded attempt is counted toward its
    // difficulty's total but never toward its correct count. In practice
    // this method is only called once every attempt has been graded, but
    // the null-safe check keeps it from throwing if that ever isn't true.
    public static DifficultyBreakdown BuildDifficultyBreakdown(IReadOnlyList<QuestionAttempt> attempts)
    {
        int CorrectCount(string difficulty) =>
            attempts.Count(a => a.Question.Difficulty == difficulty && a.Grade?.IsCorrect == true);

        int TotalCount(string difficulty) =>
            attempts.Count(a => a.Question.Difficulty == difficulty);

        return new DifficultyBreakdown(
            CorrectByEasy: CorrectCount("Easy"),
            TotalByEasy: TotalCount("Easy"),
            CorrectByMedium: CorrectCount("Medium"),
            TotalByMedium: TotalCount("Medium"),
            CorrectByHard: CorrectCount("Hard"),
            TotalByHard: TotalCount("Hard"));
    }

    // → Pause/resume orchestration for TimedQuiz.razor's pause button.
    // Pulled out of the code-behind so the actual stopwatch behavior is
    // testable directly — Stopwatch is a concrete class with an observable
    // IsRunning property, so this doesn't need real elapsed time or fake
    // clocks, just assertions on which stopwatches got Stop()/Start()
    // called. Returns the new IsPaused value; the caller assigns it, same
    // shape as GetRandomDifficulty returning rather than mutating.
    //
    // Note what this does NOT cover: the bug that made the pause button do
    // nothing wasn't in this logic, it was TimedQuiz.razor's markup never
    // passing IsPaused/OnTogglePause to QuizRunner at all. A unit test on
    // this method can't catch a missing parameter binding in Razor markup —
    // that needs a component test (e.g. bUnit), which this project doesn't
    // have set up. This test coverage is for "does pausing/resuming do the
    // right thing to the stopwatches," not "is the button actually wired."
    public static bool TogglePause(bool isPaused, System.Diagnostics.Stopwatch quizStopwatch, System.Diagnostics.Stopwatch questionStopwatch)
    {
        var newIsPaused = !isPaused;

        if (newIsPaused)
        {
            quizStopwatch.Stop();
            questionStopwatch.Stop();
        }
        else
        {
            quizStopwatch.Start();
            questionStopwatch.Start();
        }

        return newIsPaused;
    }

    // → Previous/Next navigation across already-answered questions during a
    // running quiz (TimedQuiz.razor's ViewIndex — null means "viewing/
    // answering the live question," a 0-based index into Attempts means
    // "browsing/editing that past one" via a second QuizRunner instance).
    // Pulled out for the same reason TogglePause was: pure index-in,
    // index-out transitions that don't need a running Blazor component to
    // exercise.
    //
    // GetNextViewIndex bottoms out at null rather than clamping at the last
    // index — "Next" past the last historical attempt means "return to the
    // live question," not "stay put." GetPreviousViewIndex is the mirror:
    // from null (live), it jumps to the last attempt; from an attempt, it
    // clamps at 0 rather than going negative.
    public static int? GetPreviousViewIndex(int? viewIndex, int attemptsCount)
    {
        if (viewIndex is null)
            return attemptsCount > 0 ? attemptsCount - 1 : null;

        return viewIndex.Value > 0 ? viewIndex.Value - 1 : viewIndex;
    }

    public static int? GetNextViewIndex(int? viewIndex, int attemptsCount)
    {
        if (viewIndex is null)
            return null;

        return viewIndex.Value < attemptsCount - 1 ? viewIndex.Value + 1 : null;
    }

    // → Whether the Previous/Next buttons should be enabled — kept as pure
    // functions rather than inlined in the markup so "can I actually move"
    // has one answer shared by the buttons' disabled state and anything
    // else that needs to ask it.
    public static bool CanGoPrevious(int? viewIndex, int attemptsCount) =>
        viewIndex is null ? attemptsCount > 0 : viewIndex.Value > 0;

    public static bool CanGoNext(int? viewIndex) => viewIndex is not null;

    // → True when browsing lands on the last historical attempt — the one
    // case where "Next" doesn't mean "step forward in history," it means
    // "return to the live question." TimedQuiz.razor uses this to relabel
    // the Next button ("Back to current →") rather than leave a generic
    // "Next →" that undersells what the click actually does.
    public static bool IsAtLastHistoryEntry(int? viewIndex, int attemptsCount) =>
        viewIndex is not null && viewIndex.Value == attemptsCount - 1;

    // → Whether saving an edited answer from the history view should defer
    // grading (only update the stored answer) rather than grade right away.
    // Pulled out of TimedQuiz.razor's HandleSaveHistoryEdit — the one branch
    // in that method that's pure decision logic rather than an actual
    // QuizProvider.GradeAsync call, and the branch this whole "don't reveal
    // correctness until Continue to Results" conversation keeps coming back
    // to: get this condition wrong and a still-pending marked-for-review
    // attempt would get graded (and its Grade would exist) before the
    // student ever asked to finish the quiz.
    //
    // True only for a marked-for-review attempt that hasn't been graded yet
    // (mid deferred grading — see QuestionAttempt's doc comment). Everything
    // else — a regular attempt, or a marked one that's already been graded
    // by HandleFinishMarked — re-grades immediately on save instead.
    public static bool ShouldDeferGradingOnSave(bool markedForReview, GradeResult? grade) =>
        markedForReview && grade is null;

    // Note: an earlier version of this class had a GetViewPosition(viewIndex,
    // attemptsCount) here for the nav header's "Question N" label, assuming
    // the live position was always attemptsCount + 1. That's wrong —
    // Attempts.Add happens synchronously in HandleAnswer, before the async
    // LoadNextQuestion fetch resolves, so attemptsCount jumps the instant an
    // answer is submitted, well before the next question is actually ready.
    // The live position has to come from TimedQuiz.razor's QuestionNumber
    // field instead, which only advances once the fetch succeeds — a
    // dependency this pure (viewIndex, attemptsCount) signature can't
    // express, so that logic now lives directly in TimedQuiz.razor as
    // DisplayedQuestionNumber instead of here.

    // → Ratchets the per-question max_tokens budget up after a truncation
    // (QuizQuestion.WasTruncated), for TimedQuiz.razor to both retry the
    // current question immediately and keep the higher budget for every
    // later question in the same quiz run. See ClaudeQuizProvider's
    // max_tokens comment: a longer avoid-list later in a run pushes Claude
    // toward longer, more differentiated questions, so a budget that was
    // fine early on can start truncating later even without any single
    // "wrong" value — ratcheting (rather than resetting each time) is what
    // makes that self-correcting instead of repeating the same failure on
    // every question from here to the end of the quiz.
    //
    // cap exists so a persistently truncating model/avoid-list combination
    // can't ratchet the budget (and the per-question cost/latency that comes
    // with it) up without bound — it just settles at the cap and any
    // truncation past that point falls through to the normal retry card.
    public static int BumpMaxTokens(int current, int increment = 300, int cap = 1600) =>
        Math.Min(current + increment, cap);
}

// Result of BuildDifficultyBreakdown. A small dedicated type rather than a
// tuple so the six ints read as labeled fields at every call site instead of
// Item1..Item6.
public record DifficultyBreakdown(
    int CorrectByEasy,
    int TotalByEasy,
    int CorrectByMedium,
    int TotalByMedium,
    int CorrectByHard,
    int TotalByHard);
