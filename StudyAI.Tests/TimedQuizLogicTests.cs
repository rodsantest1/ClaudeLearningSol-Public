using Xunit;
using StudyAI.Models;
using StudyAI.Services;

namespace StudyAI.Tests;

/// <summary>
/// Unit tests for TimedQuizLogic — the pure decision logic pulled out of
/// TimedQuiz.razor's code-behind (difficulty selection, difficulty-breakdown
/// grouping, pause/resume, max_tokens ratcheting, and the Previous/Next
/// history-navigation index math) specifically so it can be exercised here
/// without spinning up a Blazor component.
/// </summary>
public class TimedQuizLogicTests
{
    // --- GetRandomDifficulty -------------------------------------------------

    [Theory]
    [InlineData("Easy")]
    [InlineData("Medium")]
    [InlineData("Hard")]
    public void GetRandomDifficulty_FixedFilter_ReturnsThatDifficulty(string filter)
    {
        var result = TimedQuizLogic.GetRandomDifficulty(filter);

        Assert.Equal(filter, result);
    }

    [Fact]
    public void GetRandomDifficulty_UnknownFilter_DefaultsToMedium()
    {
        var result = TimedQuizLogic.GetRandomDifficulty("NotARealFilter");

        Assert.Equal("Medium", result);
    }

    [Fact]
    public void GetRandomDifficulty_EmptyFilter_DefaultsToMedium()
    {
        var result = TimedQuizLogic.GetRandomDifficulty("");

        Assert.Equal("Medium", result);
    }

    [Theory]
    [InlineData(0, "Easy")]
    [InlineData(1, "Medium")]
    [InlineData(2, "Hard")]
    public void GetRandomDifficulty_Mixed_UsesInjectedPickerDeterministically(int pickerIndex, string expected)
    {
        // The picker is the testable seam — production code lets it default
        // to Random.Shared, tests fix it so "Mixed" is assertable.
        var result = TimedQuizLogic.GetRandomDifficulty("Mixed", _ => pickerIndex);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetRandomDifficulty_Mixed_PassesThreeAsPickerUpperBound()
    {
        int? observedCount = null;

        TimedQuizLogic.GetRandomDifficulty("Mixed", count =>
        {
            observedCount = count;
            return 0;
        });

        Assert.Equal(3, observedCount);
    }

    [Fact]
    public void GetRandomDifficulty_FixedFilter_DoesNotInvokePicker()
    {
        // Easy/Medium/Hard are deterministic and shouldn't touch randomness at all.
        var pickerCalled = false;

        TimedQuizLogic.GetRandomDifficulty("Easy", _ => { pickerCalled = true; return 0; });

        Assert.False(pickerCalled);
    }

    // --- BuildDifficultyBreakdown ---------------------------------------------

    private static QuestionAttempt MakeAttempt(string? difficulty, bool isCorrect)
    {
        var question = new QuizQuestion(
            Topic: "Claude API",
            Prompt: "Sample question",
            Choices: new List<string> { "A", "B" },
            CorrectAnswer: "A",
            Explanation: "Because.",
            Difficulty: difficulty);

        return new QuestionAttempt
        {
            QuestionNumber = 1,
            Question = question,
            UserAnswer = "A",
            Grade = new GradeResult(IsCorrect: isCorrect, Explanation: "graded"),
            TimeSpentMs = 1000,
            MarkedForReview = false
        };
    }

    // A marked-for-review question that's been submitted but not graded yet
    // (see QuestionAttempt.Grade / TimedQuiz.razor's deferred-grading flow).
    private static QuestionAttempt MakePendingAttempt(string? difficulty)
    {
        var question = new QuizQuestion(
            Topic: "Claude API",
            Prompt: "Sample question",
            Choices: new List<string> { "A", "B" },
            CorrectAnswer: "A",
            Explanation: "Because.",
            Difficulty: difficulty);

        return new QuestionAttempt
        {
            QuestionNumber = 1,
            Question = question,
            UserAnswer = "A",
            Grade = null,
            TimeSpentMs = 1000,
            MarkedForReview = true
        };
    }

    [Fact]
    public void BuildDifficultyBreakdown_EmptyAttempts_ReturnsAllZero()
    {
        var breakdown = TimedQuizLogic.BuildDifficultyBreakdown(new List<QuestionAttempt>());

        Assert.Equal(0, breakdown.CorrectByEasy);
        Assert.Equal(0, breakdown.TotalByEasy);
        Assert.Equal(0, breakdown.CorrectByMedium);
        Assert.Equal(0, breakdown.TotalByMedium);
        Assert.Equal(0, breakdown.CorrectByHard);
        Assert.Equal(0, breakdown.TotalByHard);
    }

    [Fact]
    public void BuildDifficultyBreakdown_CountsCorrectAndTotalPerDifficulty()
    {
        var attempts = new List<QuestionAttempt>
        {
            MakeAttempt("Easy", isCorrect: true),
            MakeAttempt("Easy", isCorrect: true),
            MakeAttempt("Easy", isCorrect: false),
            MakeAttempt("Medium", isCorrect: true),
            MakeAttempt("Hard", isCorrect: false),
        };

        var breakdown = TimedQuizLogic.BuildDifficultyBreakdown(attempts);

        Assert.Equal(2, breakdown.CorrectByEasy);
        Assert.Equal(3, breakdown.TotalByEasy);
        Assert.Equal(1, breakdown.CorrectByMedium);
        Assert.Equal(1, breakdown.TotalByMedium);
        Assert.Equal(0, breakdown.CorrectByHard);
        Assert.Equal(1, breakdown.TotalByHard);
    }

    [Fact]
    public void BuildDifficultyBreakdown_NullDifficulty_ExcludedFromAllBuckets()
    {
        var attempts = new List<QuestionAttempt>
        {
            MakeAttempt(null, isCorrect: true),
            MakeAttempt("Easy", isCorrect: true),
        };

        var breakdown = TimedQuizLogic.BuildDifficultyBreakdown(attempts);

        // The null-difficulty attempt shows up nowhere in the breakdown —
        // only the Easy attempt is counted.
        Assert.Equal(1, breakdown.CorrectByEasy);
        Assert.Equal(1, breakdown.TotalByEasy);
        Assert.Equal(0, breakdown.CorrectByMedium + breakdown.CorrectByHard);
        Assert.Equal(0, breakdown.TotalByMedium + breakdown.TotalByHard);
    }

    [Fact]
    public void BuildDifficultyBreakdown_UngradedAttempt_CountsTowardTotalNotCorrect()
    {
        // A marked-for-review question that hasn't been through
        // HandleFinishMarked yet still has a real Difficulty, so it should
        // count toward that bucket's total — it just can't be "correct" yet.
        var attempts = new List<QuestionAttempt>
        {
            MakePendingAttempt("Easy"),
            MakeAttempt("Easy", isCorrect: true),
        };

        var breakdown = TimedQuizLogic.BuildDifficultyBreakdown(attempts);

        Assert.Equal(2, breakdown.TotalByEasy);
        Assert.Equal(1, breakdown.CorrectByEasy);
    }

    [Fact]
    public void BuildDifficultyBreakdown_AllCorrect_CorrectEqualsTotalPerBucket()
    {
        var attempts = new List<QuestionAttempt>
        {
            MakeAttempt("Easy", isCorrect: true),
            MakeAttempt("Medium", isCorrect: true),
            MakeAttempt("Hard", isCorrect: true),
        };

        var breakdown = TimedQuizLogic.BuildDifficultyBreakdown(attempts);

        Assert.Equal(breakdown.TotalByEasy, breakdown.CorrectByEasy);
        Assert.Equal(breakdown.TotalByMedium, breakdown.CorrectByMedium);
        Assert.Equal(breakdown.TotalByHard, breakdown.CorrectByHard);
    }

    // --- TogglePause -----------------------------------------------------
    //
    // These cover the stopwatch orchestration only — not whether the pause
    // button is actually wired up in TimedQuiz.razor's markup. That was the
    // real bug (IsPaused/OnTogglePause never passed to QuizRunner), and a
    // plain unit test on this extracted method can't catch a missing Razor
    // parameter binding; only a component test (bUnit) could; this project
    // doesn't have that set up.

    [Fact]
    public void TogglePause_FromRunning_StopsBothStopwatchesAndReturnsTrue()
    {
        var quiz = System.Diagnostics.Stopwatch.StartNew();
        var question = System.Diagnostics.Stopwatch.StartNew();

        var result = TimedQuizLogic.TogglePause(isPaused: false, quiz, question);

        Assert.True(result);
        Assert.False(quiz.IsRunning);
        Assert.False(question.IsRunning);
    }

    [Fact]
    public void TogglePause_FromPaused_StartsBothStopwatchesAndReturnsFalse()
    {
        var quiz = new System.Diagnostics.Stopwatch();
        var question = new System.Diagnostics.Stopwatch();

        var result = TimedQuizLogic.TogglePause(isPaused: true, quiz, question);

        Assert.False(result);
        Assert.True(quiz.IsRunning);
        Assert.True(question.IsRunning);
    }

    [Fact]
    public void TogglePause_Pausing_DoesNotResetAccumulatedElapsedTime()
    {
        // Stop() (unlike Reset()) preserves ElapsedTicks — pausing should
        // freeze the clock, not zero it out. Simulate "some time had already
        // accumulated" by starting and stopping once, then leave the
        // stopwatch stopped (not restarted) before pausing again.
        //
        // Deliberately NOT calling quiz.Start() again before TogglePause:
        // an earlier version of this test did, which meant real wall-clock
        // time elapsed between capturing elapsedBeforePause and TogglePause's
        // own Stop() call — ElapsedTicks legitimately grows during any
        // running interval, however short, so the exact-equality assertion
        // was timing-dependent and failed under enough machine load (test
        // runner overhead, a busy IDE) to register even one extra tick.
        // Calling TogglePause on an already-stopped stopwatch makes its
        // internal Stop() a no-op, which is what "doesn't reset" actually
        // means here — Reset() would zero ElapsedTicks; a second Stop()
        // must not.
        var quiz = System.Diagnostics.Stopwatch.StartNew();
        quiz.Stop();
        var elapsedBeforePause = quiz.ElapsedTicks;
        var question = new System.Diagnostics.Stopwatch();

        TimedQuizLogic.TogglePause(isPaused: false, quiz, question);

        Assert.Equal(elapsedBeforePause, quiz.ElapsedTicks);
    }

    [Fact]
    public void TogglePause_RoundTrip_ReturnsToOriginalIsPausedValue()
    {
        var quiz = new System.Diagnostics.Stopwatch();
        var question = new System.Diagnostics.Stopwatch();

        var pausedThenResumed = TimedQuizLogic.TogglePause(
            TimedQuizLogic.TogglePause(isPaused: false, quiz, question),
            quiz, question);

        Assert.False(pausedThenResumed);
        Assert.True(quiz.IsRunning);
        Assert.True(question.IsRunning);
    }

    // --- View navigation (Previous/Next history browsing) -----------------
    //
    // GetPreviousViewIndex/GetNextViewIndex/CanGoPrevious/CanGoNext/
    // IsAtLastHistoryEntry back the Previous/Next controls on TimedQuiz.razor's
    // Running screen (see ViewIndex there). null always means "viewing the
    // live question"; a 0-based int means "browsing Attempts[index]."
    //
    // Note: this class used to also have GetViewPosition, for the header's
    // "Question N" label. It assumed the live position was always
    // attemptsCount + 1, which doesn't hold — Attempts.Add happens
    // synchronously in HandleAnswer, before the next question's async fetch
    // resolves, so that undercount showed up as the header advancing before
    // the new question was actually loaded. The live position now reads
    // TimedQuiz.razor's QuestionNumber field directly (DisplayedQuestionNumber),
    // which isn't expressible as a pure function of (viewIndex, attemptsCount)
    // alone, so there's no equivalent pure-logic test for it here — it's
    // simple enough to read directly in the markup.

    [Fact]
    public void GetPreviousViewIndex_FromLive_WithAttempts_ReturnsLastIndex()
    {
        var result = TimedQuizLogic.GetPreviousViewIndex(viewIndex: null, attemptsCount: 3);

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetPreviousViewIndex_FromLive_NoAttempts_StaysNull()
    {
        var result = TimedQuizLogic.GetPreviousViewIndex(viewIndex: null, attemptsCount: 0);

        Assert.Null(result);
    }

    [Fact]
    public void GetPreviousViewIndex_FromMiddleIndex_Decrements()
    {
        var result = TimedQuizLogic.GetPreviousViewIndex(viewIndex: 2, attemptsCount: 5);

        Assert.Equal(1, result);
    }

    [Fact]
    public void GetPreviousViewIndex_AtZero_ClampsAtZero()
    {
        var result = TimedQuizLogic.GetPreviousViewIndex(viewIndex: 0, attemptsCount: 5);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetNextViewIndex_FromLive_StaysNull()
    {
        // Nothing further forward than the live question exists yet.
        var result = TimedQuizLogic.GetNextViewIndex(viewIndex: null, attemptsCount: 5);

        Assert.Null(result);
    }

    [Fact]
    public void GetNextViewIndex_FromLastHistoryIndex_ReturnsToLive()
    {
        var result = TimedQuizLogic.GetNextViewIndex(viewIndex: 4, attemptsCount: 5);

        Assert.Null(result);
    }

    [Fact]
    public void GetNextViewIndex_FromMiddleIndex_Increments()
    {
        var result = TimedQuizLogic.GetNextViewIndex(viewIndex: 1, attemptsCount: 5);

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetPreviousThenNext_RoundTrip_ReturnsToLive()
    {
        var previous = TimedQuizLogic.GetPreviousViewIndex(viewIndex: null, attemptsCount: 3);
        var backToLive = TimedQuizLogic.GetNextViewIndex(previous, attemptsCount: 3);

        Assert.Null(backToLive);
    }

    [Theory]
    [InlineData(null, 3, true)]
    [InlineData(null, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(1, 3, true)]
    public void CanGoPrevious_MatchesExpected(int? viewIndex, int attemptsCount, bool expected)
    {
        var result = TimedQuizLogic.CanGoPrevious(viewIndex, attemptsCount);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, true)]
    [InlineData(2, true)]
    public void CanGoNext_MatchesExpected(int? viewIndex, bool expected)
    {
        var result = TimedQuizLogic.CanGoNext(viewIndex);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, 3, false)]
    [InlineData(1, 3, false)]
    [InlineData(2, 3, true)]
    public void IsAtLastHistoryEntry_MatchesExpected(int? viewIndex, int attemptsCount, bool expected)
    {
        var result = TimedQuizLogic.IsAtLastHistoryEntry(viewIndex, attemptsCount);

        Assert.Equal(expected, result);
    }

    // --- ShouldDeferGradingOnSave -------------------------------------------
    //
    // Backs HandleSaveHistoryEdit's fork between "just update the stored
    // answer" (defer) and "re-grade right now." Only a still-pending
    // marked-for-review attempt (marked, Grade still null) should defer —
    // everything else re-grades immediately.

    [Fact]
    public void ShouldDeferGradingOnSave_MarkedAndUngraded_Defers()
    {
        var result = TimedQuizLogic.ShouldDeferGradingOnSave(markedForReview: true, grade: null);

        Assert.True(result);
    }

    [Fact]
    public void ShouldDeferGradingOnSave_MarkedButAlreadyGraded_RegradesImmediately()
    {
        // A marked-for-review attempt that already went through
        // HandleFinishMarked has a real Grade — editing it after that point
        // (e.g. from ReviewMarked) should re-grade like any other attempt,
        // not silently defer again.
        var grade = new GradeResult(IsCorrect: true, Explanation: "Because.");

        var result = TimedQuizLogic.ShouldDeferGradingOnSave(markedForReview: true, grade);

        Assert.False(result);
    }

    [Fact]
    public void ShouldDeferGradingOnSave_NotMarkedAndUngraded_RegradesImmediately()
    {
        // Shouldn't happen in practice (a non-marked attempt is always
        // graded at submit time), but the condition is only about
        // MarkedForReview — an ungraded, non-marked attempt still doesn't
        // qualify for deferral.
        var result = TimedQuizLogic.ShouldDeferGradingOnSave(markedForReview: false, grade: null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldDeferGradingOnSave_NotMarkedAndGraded_RegradesImmediately()
    {
        var grade = new GradeResult(IsCorrect: false, Explanation: "Not quite.");

        var result = TimedQuizLogic.ShouldDeferGradingOnSave(markedForReview: false, grade);

        Assert.False(result);
    }

    // --- BumpMaxTokens -----------------------------------------------------

    [Fact]
    public void BumpMaxTokens_BelowCap_AddsIncrement()
    {
        var result = TimedQuizLogic.BumpMaxTokens(700);

        Assert.Equal(1000, result);
    }

    [Fact]
    public void BumpMaxTokens_CustomIncrement_IsRespected()
    {
        var result = TimedQuizLogic.BumpMaxTokens(700, increment: 500);

        Assert.Equal(1200, result);
    }

    [Fact]
    public void BumpMaxTokens_WouldExceedCap_ReturnsCapInstead()
    {
        var result = TimedQuizLogic.BumpMaxTokens(1500, increment: 300, cap: 1600);

        Assert.Equal(1600, result);
    }

    [Fact]
    public void BumpMaxTokens_AlreadyAtCap_StaysAtCap()
    {
        var result = TimedQuizLogic.BumpMaxTokens(1600, increment: 300, cap: 1600);

        Assert.Equal(1600, result);
    }

    [Fact]
    public void BumpMaxTokens_RepeatedCalls_RatchetsUpwardEachTime()
    {
        // Simulates successive truncations across a quiz run: each bump
        // starts from the previous result, so the budget keeps climbing
        // instead of resetting.
        var budget = 700;
        budget = TimedQuizLogic.BumpMaxTokens(budget);
        budget = TimedQuizLogic.BumpMaxTokens(budget);

        Assert.Equal(1300, budget);
    }
}
