# StudyAI

## What this is

A rudimentary Blazor Server app with an AI-powered quiz/exam trainer, built
deliberately iteration-by-iteration so each concept from the **Claude Certified
Developer, Foundations (CCDV-F)** exam shows up as a distinct, diffable step in
git history rather than being buried in one big build. The app is secondary to
the goal — it exists to demonstrate exam concepts in a real running system, not
the other way around.

Keep additions minimal and rudimentary unless there's a specific exam concept
they'd demonstrate. Don't add frameworks, an ORM, auth, or polish for its own
sake.

**Keep this file current.** When a new iteration lands, a feature gets added,
or an "open thread" below gets resolved or a new one comes up, update the
relevant section here as part of that work — don't wait to be asked.

## Stack

.NET 8, Blazor Server (Interactive Server render mode), no database, xUnit for
tests. Solution format is `.slnx`.

- `StudyAI/` — the app
- `StudyAI.Tests/` — xUnit tests, no network calls anywhere in the suite

## Architecture

`IQuizProvider` is the seam: `GetQuestionAsync(topic, model?, difficulty?,
recentPrompts?, recentWasMultipleChoice?)` and `GradeAsync(question, userAnswer,
model?)`. Two implementations:

- `StaticQuizProvider` — iteration 1. Hardcoded MCQ bank, no AI, no network.
  Not registered in DI anymore, but kept as an offline fallback / test fixture
  and as the "before" side of diffs against the AI provider.
- `ClaudeQuizProvider` — iteration 2+. Currently the registered `IQuizProvider`
  in `Program.cs`. Generates questions and grades answers via the Claude API.

`IQuizPlanner` (iteration 4) is a second, deliberately separate seam:
`RecommendNextAsync(history, model?)`. It doesn't generate or grade content —
it answers "what should happen next," Anthropic's "routing" workflow pattern.
`ClaudeQuizPlanner` is the one implementation, registered alongside
`IQuizProvider` in `Program.cs` with its own DI-managed `HttpClient`. Kept as
its own interface rather than a third `IQuizProvider` method so the
routing-decision capability is visible in the architecture, not buried as a
side effect of content generation.

Pure logic that would otherwise require mocking `HttpClient` to test lives in
standalone static classes instead:

- `ClaudeResponseParser` — iteration 2/3's parser: turned `GetQuestionAsync`'s
  raw model text into a `QuizQuestion` (or null), handling prose/code-fences
  around prompted JSON. Superseded by `ClaudeQuestionToolParser` as of
  iteration 5; kept in the repo (tests included) as the "before" side of that
  diff, same status as `StaticQuizProvider`.
- `ClaudeQuestionToolParser` — iteration 5's replacement: turns
  `GetQuestionAsync`'s raw response body into a `QuizQuestion` by reading a
  `tool_use` block and branching on which of the two tools got called
  (`create_multiple_choice_question` vs `create_short_answer_question`).
  Returns `null` if Claude replied with plain text instead of calling either
  tool — the one failure mode that's real here and isn't for the
  forced-`tool_choice` parsers below, since `tool_choice: "auto"` doesn't
  guarantee a tool call at all.
- `ClaudeGradeParser` — turns `GradeAsync`'s raw response body into a
  `GradeResult`. Handles a missing/incomplete tool call, including truncation,
  by returning `GradingFailed: true` rather than a bare `IsCorrect = false`.
  Also has `WasTruncated`, which checks `stop_reason == "max_tokens"`.
- `ClaudeQuizProvider.ResolveModel` — the requested-model-vs-config-default
  fallback logic, pulled out after noticing the optional `model` parameter had
  no test covering it at all (the one test that touches `GradeAsync` short-
  circuits on the missing-API-key check before reaching it).
- `ClaudeUsageParser` — pulls `usage.input_tokens`/`output_tokens` out of a
  Messages API response body. Shared by `GetQuestionAsync`, `GradeAsync`, and
  `RecommendNextAsync`, attached to the returned record via a `with`
  expression. Returns nulls instead of throwing on a missing field or
  malformed body — usage is a nice-to-have for the display, not something
  that should ever break a question, a grade, or a recommendation over it.
- `ClaudeRecommendationParser` — turns `RecommendNextAsync`'s raw tool-call
  response into a `NextStepRecommendation`. Same shape as `ClaudeGradeParser`
  (missing/incomplete tool call handled the same way), applied to the
  `recommend_next` tool instead of `submit_grade`.
- `ClaudeQuizProvider.BuildAvoidClause` — turns a list of recently-asked
  prompts into the "don't repeat these" text appended to `GetQuestionAsync`'s
  user message. Same reasoning as `ResolveModel`: a per-call input
  (`recentPrompts`) had no test until the string-building logic behind it was
  pulled into its own pure method.
- `ClaudeQuizProvider.BuildFormatHintClause` — same shape as `BuildAvoidClause`
  but for question *format* instead of content: turns a list of recent
  multiple-choice/short-answer outcomes into a nudge toward whichever format
  has been used less, only when there's an actual imbalance to correct.

This split is the pattern to keep following: if new logic can be pure (text
in, typed result out), pull it out of the `HttpClient`-holding class so it's
unit testable without mocking.

The optional `model` and `difficulty` parameters on `IQuizProvider` (added
after iteration 3) are the deliberate exceptions to "the interface never
changes." Everything through iteration 3 only swapped implementation behind a
stable interface; "which model tier" and "how hard should the question be"
are genuinely new inputs, so the interface had to grow to carry them. Both are
additive/optional, so nothing that already called the interface broke.
`StaticQuizProvider` ignores both. `ClaudeQuizProvider` uses `model` in place
of the `Claude:Model` config default when provided, and threads `difficulty`
into the generation prompt (defaulting to "Medium").

`recentPrompts` (added after iteration 4) grew the interface the same
additive way, to fix a real symptom: a stateless "write one question about
X" prompt has no memory of its own prior output, so for a narrow
topic/difficulty pair Claude kept converging on the same "obvious" question
run after run. `Quiz.razor` now keeps a session-long `Queue<string>` of the
last 5 shown prompts (`RecentPrompts`) and passes it into every
`GetQuestionAsync` call; `ClaudeQuizProvider.BuildAvoidClause` turns that
into an explicit "don't repeat or closely rephrase these" instruction
appended to the user message. `StaticQuizProvider` ignores it, same as
`model`/`difficulty` — a fixed bank has nothing to steer.

`recentWasMultipleChoice` (added after iteration 5) is the same trick again,
for a different symptom: a *static* instruction telling Claude to prefer one
question format over the other overcorrected in manual testing (see Open
Threads history below for the numbers). `Quiz.razor` keeps a second
`Queue<bool>` (`RecentFormats`, same 5-item cap) recording whether each
recent question was multiple-choice, and passes it into every
`GetQuestionAsync` call; `ClaudeQuizProvider.BuildFormatHintClause` only adds
a nudge when the actual recent history is lopsided, and says nothing when
it's balanced or empty. `StaticQuizProvider` ignores it, same as the other
optional parameters.

## Iteration history

1. **Static quiz app** — no AI, hardcoded MCQ bank, string-compare grading.
2. **Claude-generated MCQ** — `ClaudeQuizProvider` swapped in via one DI line.
   Questions generated via prompted JSON (asked nicely, parsed defensively).
   Grading still string-compare, identical to iteration 1 on purpose.
3. **Free-text questions + tool-use grading** — questions no longer have fixed
   choices; `GradeAsync` uses a forced `tool_choice` (`submit_grade` tool)
   instead of string comparison, so Claude judges meaning, not exact wording.
   `ClaudeGradeParser` and `WasTruncated` added here.
4. **Adaptive routing** — `IQuizPlanner`/`ClaudeQuizPlanner` added as a second
   seam (see Architecture). After every grade, a forced `recommend_next` tool
   call looks at per-topic performance so far this session (`TopicStats` in
   `Quiz.razor`, snapshotted into `TopicPerformance` records) and returns a
   `NextStepRecommendation` (topic, difficulty, one-sentence reason). The Quiz
   page shows it as a card with a "Use this" button that applies the pick and
   loads a new question. `QuizQuestion` gained a `Difficulty` field so the
   recommendation has something concrete to control — `GetQuestionAsync`
   threads it into the generation prompt. This is Anthropic's "routing"
   workflow pattern, and the first hands-on coverage of Agents and Workflows
   (14.7%) in this app.
5. **Multi-tool question generation** — `GetQuestionAsync` no longer hardcodes
   free-text. It offers Claude two tools, `create_multiple_choice_question`
   and `create_short_answer_question`, with `tool_choice: "auto"` instead of
   a forced tool — Claude decides per question which format fits, rather than
   the app dictating it. `ClaudeQuestionToolParser` handles the "which tool
   got called" branching (see Architecture). This is the first `tool_choice:
   "auto"` call site in the app — every other tool call
   (`GradeAsync`'s `submit_grade`, `RecommendNextAsync`'s `recommend_next`)
   forces one specific tool because there's only one valid output shape;
   question generation is the first case where two shapes are both valid and
   the model gets to pick. Also brings the MCQ path back to life: process-of-
   elimination cross-out (built in iteration 1, dormant since iteration 3
   made all AI-generated questions free-text) works again whenever Claude
   picks the multiple-choice tool.

Also added along the way, not tied to a specific iteration: mark-for-review running list,
loading/grading state indicators, a "Claude Certified Developer" topic with
10 hand-written practice questions in `StaticQuizProvider` (offline fallback,
not live — `ClaudeQuizProvider` handles that topic dynamically instead), and a
Haiku/Sonnet model picker on the Quiz page with a Stopwatch-timed display
(Model Selection and Optimization, 16.8%) — the point is feeling the speed
tradeoff between tiers, not just picking one from a dropdown. Token usage
(`InputTokens`/`OutputTokens` on `QuizQuestion` and `GradeResult`, parsed by
`ClaudeUsageParser`) followed the same line of thinking: each `.timing` line
now shows tokens alongside milliseconds, and a session-wide aggregate (total
calls, total time, total tokens in/out) sits under the score so cost is
visible across a whole study session, not just per call.

Also fixed along the way: `GetQuestionAsync` repeating the same (or nearly
the same) question for a given topic/difficulty, since the prompt had no
memory of what it had already generated. `Quiz.razor` now tracks the last 5
prompts shown and replays them into the request as an avoid-list — see
`IQuizProvider`'s `recentPrompts` parameter and
`ClaudeQuizProvider.BuildAvoidClause` above.

Also fixed: `GradeResult` couldn't tell "graded as incorrect" apart from
"grading failed" — both came back as `IsCorrect = false`, so a truncated or
unparseable response silently cost the user a point. `GradeResult` gained a
`GradingFailed` bool (additive/optional, same trick as `InputTokens` —
defaults to `false` so every existing success-path call site was unaffected)
and a computed `Outcome` property (`GradeOutcome`: `Correct` / `Incorrect` /
`GradingFailed`) derived from `IsCorrect` + `GradingFailed` in one place.
`ClaudeGradeParser` and `ClaudeQuizProvider.GradeAsync` set `GradingFailed:
true` on every failure path (missing API key, API error, missing/truncated
tool call, unparseable body). `Quiz.razor`'s `Submit()` checks
`Result.Outcome == GradeOutcome.GradingFailed` and, if so, skips the score,
`TopicStats`, and the `Recommend()` call entirely instead of quietly
treating it as a wrong answer; the result card shows a neutral "Grading
failed" state instead of the red "Not quite" styling. This was the
prerequisite called out below for the truncation-retry work — that's still
not wired up, but the score can no longer be corrupted by a failed grade
while it isn't.

Also fixed: right after iteration 5 landed, manual testing showed
`tool_choice: "auto"` skewing hard toward `create_multiple_choice_question`
— close to 10/10 in a row. The first fix attempt gave the base prompt a
static preference ("default to short-answer unless multiple-choice earns its
place"), which overcorrected to close to the opposite extreme (all
short-answer) in the next round of testing. A static instruction has no
feedback loop, so it just replaces one fixed bias with another. The actual
fix reverted the base prompt to neutral ("use whichever format genuinely
fits, don't default to one") and added `recentWasMultipleChoice` (see
Architecture) so `ClaudeQuizProvider.BuildFormatHintClause` can nudge toward
whichever format the *real* recent history is missing, and say nothing when
it's already balanced. Same lesson as the repeated-question fix: a stateless
prompt can't self-correct without the app feeding real history back in —
qualitative wording alone isn't a substitute.

Also added: Previous/Next navigation across already-answered questions on
the Timed Quiz's Running screen (`TimedQuiz.razor`'s `ViewIndex`, a new
`QuestionHistoryViewer` component). Started as a read-only "peek back" and
grew editing on top — the student can change an already-submitted answer
from history and click Save. What Save does forks on whether the attempt
was already graded: a regular (non-marked) attempt re-grades immediately
via another `GradeAsync` call, folded into the same session cost totals via
`RecordCall`; a still-pending marked-for-review attempt only updates the
stored answer, keeping grading deferred to Finish Quiz like before (see
`TimedQuiz.razor`'s `HandleSaveHistoryEdit`). This visibly overlaps with
"mark for review": before this, marking was the only way to revisit an
answer, and now any answer — marked or not — can be revisited and changed
via Prev/Next. Mark for review's remaining, and now sole, distinguishing
behavior is deferring grading until the end of the quiz; `QuizRunner`'s hint
text next to the checkbox was updated ("Keeps this question ungraded until
you finish the quiz") to say exactly that instead of implying it's the only
way back. The index math itself (`GetPreviousViewIndex`/`GetNextViewIndex`/
`CanGoPrevious`/`CanGoNext`/`IsAtLastHistoryEntry`) lives in `TimedQuizLogic`,
pulled out the same way `TogglePause` was, with its own coverage in
`TimedQuizLogicTests.cs`.

Also fixed: an early version of the above had two separate sticky bars — a
Previous/Next nav bar in `TimedQuiz.razor` and `QuizRunner`'s own progress/
timer header — stacked with a hardcoded offset (`top: 3rem` on one to clear
the other's height) to keep them from overlapping when scrolled. That grew
into a real problem beyond the CSS coupling: showing two "Question X of Y"
displays at once was redundant, and the nav bar's counter (`Attempts.Count`-
derived) advanced the instant an answer was submitted, before the next
question had actually loaded, while `QuizRunner`'s own counter (driven by
the `QuestionNumber` field, only bumped once the fetch succeeds) stayed
correct — so the two counters visibly disagreed for the length of every
fetch. Fixed by merging into one header: `QuizRunner` no longer renders any
header at all (just the question card), and `TimedQuiz.razor`'s single
`.quiz-header` now owns progress, Previous/Next, and the timers/pause button
together, `position: sticky; top: 0` with nothing left to offset against.
The counter itself reads a new `DisplayedQuestionNumber` computed property
(not `Attempts.Count`) — `QuestionNumber` while live, or the browsed
attempt's own `QuestionNumber` while using Prev/Next — which is what
actually fixed the premature-advance bug, not just the visual duplication.
`TimedQuizLogic.GetViewPosition`, which encoded the old (wrong)
`Attempts.Count`-based assumption, was removed along with its tests, since
`DisplayedQuestionNumber` depends on a field outside the
`(viewIndex, attemptsCount)` shape that class's other methods share.

Also fixed: `QuestionHistoryViewer` (Timed Quiz's Previous/Next browsing —
see above) used to show a result badge, an explanation, and even highlight
the correct choice among the MCQ options for any already-graded (non-marked)
attempt the student browsed back to — so navigating back during a still-
running quiz could tell you whether you got a question right well before
Continue to Results. Removed all of that: no badge, no correct-answer
highlight, no explanation, regardless of the attempt's actual `Grade` state.
Every attempt now gets the same neutral "you'll see whether this one's
correct once you finish the quiz" note, and the Save button always reads
"Save answer" (dropped the "Save & re-grade" distinction in the UI, since
users shouldn't be able to tell from the button whether a background
re-grade is happening). The underlying grading pipeline in
`HandleSaveHistoryEdit` is unchanged — a regular attempt's edit still
re-grades immediately behind the scenes — this was purely a display fix,
since nothing rendered that result until now.

Also replaced: `QuestionHistoryViewer` — the dedicated component from the
Previous/Next entry above — is gone. Previous/Next browsing now renders a
SECOND `QuizRunner` instance in a new `IsHistoryMode`, instead of a
purpose-built component with its own card layout, save row, and copy.
`QuizRunner` gained `InitialAnswer` (the value to preselect and compare
against — `""` live, an attempt's stored `UserAnswer` in history mode),
`IsHistoryMode`, and `ShowMarkForReview` (off in history mode). The action
button's disabled condition grew one clause — `selectedAnswer ==
InitialAnswer` — so a historical question starts greyed out exactly like the
live one starts greyed out before any choice is made; picking a different
answer un-greys it, and clicking it calls `HandleHistoryAnswer` (a thin
adapter onto the existing `HandleSaveHistoryEdit`) instead of advancing to a
new question. This was a deliberate "every screen should look and behave the
same" simplification — same card, same button-label logic ("Next Question" /
"Submit" based on position, same as live), same Quit button — rather than
maintaining two different question-card designs for what's fundamentally the
same interaction (pick an answer, submit it). The one branch inside
`HandleSaveHistoryEdit` that's actual decision logic (defer vs. re-grade
immediately) was pulled out as `TimedQuizLogic.ShouldDeferGradingOnSave`,
with its own tests in `TimedQuizLogicTests.cs` — the specific piece of state
that keeps a still-pending marked-for-review attempt from getting graded
before Continue to Results.

Also fixed: right after the `IsHistoryMode` change above shipped, a
historical question's stored answer wasn't showing as selected when browsing
back to it — the choices/textarea rendered with nothing picked, even for an
attempt the student had genuinely already answered. Root cause was
`QuizRunner`'s "did the question change" check: it compared `Question` by
object reference (`ReferenceEquals`), which happens to stay the same
instance across a history save (`QuestionAttempt`'s `with` expression
doesn't touch `Question`) but apparently isn't a reliable "same vs.
different question" signal in general — the exact pitfall the old
`QuestionHistoryViewer` component's own equivalent check had already run
into and left a comment about, before this component absorbed its job (it
keyed off `Attempt.QuestionNumber` instead of comparing `Attempt` by
reference, for the same reason). Fixed by switching the check to
`QuestionNumber` — a plain `int`, unique per question either way (the live
field only advances after a successful fetch; an attempt's `QuestionNumber`
is fixed at submit time) — so there's no identity ambiguity left to trip
over.

Also fixed: the `ReferenceEquals`-to-`QuestionNumber` fix directly above was a
real, correct improvement, but it turned out not to be the actual cause of
what the student kept seeing — after it shipped, browsing back to an
already-answered question *still* showed nothing selected. Tracked down by
temporarily rendering `QuizRunner`'s raw `selectedAnswer`/`InitialAnswer`
values on screen, which revealed `InitialAnswer` held the literal text
`"Attempts[ViewIndex.Value].UserAnswer"` — the C# expression itself, not its
evaluated value. Root cause: in Razor, a component attribute bound to a
**non-string** parameter (`int`, a record, `bool`) is always evaluated as a
C# expression with no special syntax needed, but an attribute bound to a
**string** parameter is treated as a plain string literal unless explicitly
prefixed with `@`. `TimedQuiz.razor`'s `<QuizRunner ... InitialAnswer=
"Attempts[ViewIndex.Value].UserAnswer" ...>` was missing that `@` — every
other parameter on that same tag (`Question`, `QuestionNumber`,
`TotalQuestions`) happens to be non-string, so they evaluated correctly and
gave no hint that `InitialAnswer` specifically wasn't. Fixed by changing it to
`InitialAnswer="@Attempts[ViewIndex.Value].UserAnswer"`. Worth remembering for
any future `string`-typed `[Parameter]`: a component attribute that *looks*
like an expression silently becomes a literal string without the `@`, and
nothing about it fails to compile or throws at runtime — it just quietly
binds the wrong value.

Also fixed: the sticky `.quiz-header` (progress + Previous/Next + timers/
pause, all one flex row) only just fit its own 800px max-width to begin with,
so any real-world narrowing (a smaller window, a scrollbar) pushed the timer
group onto a second line by itself, jammed up against the question card below
with no separation. First fix attempt forced that second row deliberately
(`flex-basis: 100%` on `.timer-group`, plus a border-top) rather than leaving
it to flex-wrap's unpredictable break point — worked, but traded the bug for
a header that was always two rows even when one would've fit. Replaced that
with sizing everything in the header down instead (smaller padding/gaps,
smaller nav buttons and timer text, a narrower progress bar) so all three
pieces reliably share one line again within the same 800px budget.

Also fixed: that header-shrinking pass had a side effect that only showed up
on the Results screen — `TimedQuiz.razor`'s header progress bar and
`DifficultyBar.razor`'s (unrelated) correct/total bars both use the plain
class names `.progress-bar`/`.progress-fill`, and Blazor's inline `<style>`
blocks aren't CSS-isolated the way a companion `.razor.css` file would be, so
the two components were sharing one global rule the whole time. Shrinking
the header's version down to a thin 5px sliver shrank `DifficultyBar`'s much
taller bars down to match, crushing its centered percentage label into an
unreadable line. Fixed by renaming the header's copy to
`.header-progress-bar`/`.header-progress-fill` — `DifficultyBar`'s own rules
were never touched and no longer share a name with anything. Worth watching
for elsewhere in this app: any two components defining the same class name
in their own `<style>` block are one rename away from silently fighting over
it, since nothing here scopes styles per-component.

Also fixed: the doc comment explaining the rename above broke the build the
first time it was written — `TimedQuiz.razor` failed with `RZ9980 Unclosed
tag 'style'`. The cause was the comment text itself: it was a CSS `/* ... */`
comment sitting inside the page's `<style>` block, and it spelled out the
literal string `<style>` while explaining the bug (talking about "Blazor's
plain `<style>` blocks"). Razor's tag-balancer for a `<style>`/`<script>`
block apparently doesn't know it's inside a CSS comment and scans the raw
text for anything that looks like a tag, so that literal `<style>` read as
an extra nested opening tag — which threw off the count enough that the
real closing `</style>` further down the file registered as unmatched.
Fixed by rewording the comment to say "style blocks" instead of spelling
out the tag with angle brackets. General lesson: never write a literal
`<style>` or `<script>` (with angle brackets) inside a comment that lives
inside that same kind of block — say "style block"/"script block" in prose
instead, even in a `/* CSS comment */`.

Also replaced: the header's Previous/Next and `QuizRunner`'s own per-instance
Submit/Next Question button — two different-looking controls that both
ultimately meant "commit what's on screen and move" — merged into one
Previous/Next row at the bottom of the answers (`.answer-nav-footer`),
replacing both. Quit Quiz moved up into the header as its own control
(a sibling of `.timer-group`, not nested inside it) to fill the space
Previous/Next left there.

`HandleNext` is where the merge actually lives: live (`ViewIndex is null`),
it submits `LiveSelectedAnswer` through the existing grade-and-advance
pipeline, same contract Submit always had; browsing history, it saves a
pending edit if there is one (`HandleSaveHistoryEdit` already no-ops safely
when nothing changed, so this is unconditional) and then steps forward, same
as the old header Next button. `HandlePrevious` mirrors the save-if-changed
half for stepping backward through history, but deliberately does *not*
touch or submit `LiveSelectedAnswer` when leaving the live question to peek
at history — Previous stays a pure, non-destructive peek from live, exactly
like before; only Next commits anything, and only from the live question.

This forced `QuizRunner` itself to shrink into a much simpler component: no
buttons, no `OnAnswer`/`OnQuit`/`OnMark` callbacks, no `IsLoading`/`IsPaused`/
`IsHistoryMode`/`InitialAnswer`/`TotalQuestions`/`Config` parameters (`Config`
turned out to be unused inside the component even before this — dead weight
carried along since the original build). `SelectedAnswer` and `IsMarked` are
now two-way bound parameters (`SelectedAnswerChanged`/`IsMarkedChanged`)
instead of `QuizRunner`'s own private fields, because the parent needs to
know the current pick to decide whether Next is even clickable and what to
submit when it's clicked. `TimedQuiz.razor` owns `LiveSelectedAnswer`/
`LiveIsMarked`/`HistorySelectedAnswer` and resets them directly at the exact
points it changes the question (`LoadNextQuestion`'s success path, `GoToPrevious`/
`GoToNext`, `StartQuiz`) instead of `QuizRunner` reactively detecting a
parameter change after the fact — the same class of `OnParametersSet`
parameter-diffing that caused both bugs described in the two entries above
this one is now only needed for the one piece of state that's genuinely still
local and low-stakes (cross-out marks), not for anything that affects what
shows as selected. `QuizAnswerSubmission` (the payload type `QuizRunner`'s old
`OnAnswer` callback used) is gone along with it — nothing constructs one
anymore now that `HandleAnswer` takes `(answer, markedForReview)` directly.

Also fixed, as a side effect of the above: `QuizRunner`'s `OnMark` callback
and `TimedQuiz.razor`'s `HandleMark` were already dead code before this
change — the Mark for Review checkbox's `@onchange` only ever updated
`QuizRunner`'s own local `isMarked` field, never actually invoked `OnMark`,
so `HandleMark`'s `MarkedForReview.Add/Remove` calls never ran from the
checkbox at all. Had no visible effect either way, since `HandleAnswer`
already adds to `MarkedForReview` correctly at submit time from
`submission.MarkedForReview` — but it's removed now rather than carried
forward into the new bound-parameter design.

Also fixed: right after the Previous/Next-merge redesign above shipped,
clicking an answer stopped doing anything at all — no highlight, Next stayed
disabled, nothing. Same root cause as the `InitialAnswer` bug from two
entries up, just on a different attribute this time: `TimedQuiz.razor`'s
`<QuizRunner ... SelectedAnswer="LiveSelectedAnswer" ...>` (and the history
instance's `SelectedAnswer="HistorySelectedAnswer"`) were both missing the
`@` a `string`-typed component parameter needs, so each was binding the
literal text `"LiveSelectedAnswer"`/`"HistorySelectedAnswer"` instead of the
field's value. Confirmed with the same trick as before — a temporary debug
banner showing `SelectedAnswer`, a click counter, and `EventCallback.HasDelegate`
— which showed clicks *were* registering and `SelectedAnswerChanged` *was*
wired up correctly (`hasDelegate=True`); the callback genuinely updated
`LiveSelectedAnswer` in the parent, it just got stomped back to the literal
string on the very next render because `SelectedAnswer` itself was never
reading the field to begin with. Fixed by adding the `@` to both. Two
instances of the identical mistake in one project is worth calling out
plainly: **every `string`-typed Razor component parameter bound to a C#
expression needs an explicit `@`** — non-string parameters (`int`, `bool`,
a record) don't, which is exactly what makes this easy to miss when a tag
has a mix of both, and the failure is always silent (compiles fine, runs,
just quietly binds the wrong value).

Also removed: the untimed `Quiz.razor` had its own, much older "Mark for
review" toggle (a `review-btn`) and a static `MarkedForReview` list rendered
at the bottom of the page. Unlike Timed Quiz's version, it didn't defer
anything — `Submit` graded immediately regardless of the mark state — and
the list itself wasn't clickable, so marking a question there never did more
than add a topic/prompt line to a static list. There's also no time pressure
in that mode forcing you to move on before you're ready, which is the actual
reason "skip and come back later" earns its keep in Timed Quiz. Removed
`IsMarkedForReview`, `MarkedForReview`, and `ToggleReview()` from
`Quiz.razor` rather than trying to make it do something, since nothing in
that mode currently calls for a skip-and-return workflow.

Also fixed: restarting a Timed Quiz (Quick Start/Configure again, including
right after quitting mid-quiz) showed the PREVIOUS run's last question, still
fully interactive with its old timer ticking, under a header that had already
reset to "Question 0 of N." `StartQuiz` reset every other piece of state but
never touched `CurrentQuestion`/`QuestionStopwatch` — `QuizRunner` only
renders its loading spinner when `Question` is actually `null`, so the stale
question stayed on screen during the new quiz's first fetch. Fixed by nulling
`CurrentQuestion` and resetting `QuestionStopwatch` in `StartQuiz` alongside
everything else already reset there.

Also redesigned: `Home.razor` replaced its two plain text links (Regular
Quiz / Timed Quiz) with two large gradient "mode cards" (`.mode-card-regular`,
`.mode-card-timed`) — icon, title, one-line description, "Start quiz →" —
with a hover lift/shadow and a sliding arrow. Purely cosmetic, no new
CCDV-F concept; the original two-link screen looked like a placeholder.

Also fixed: `QuizResults.razor`'s "Average Time/Question" stat showed raw
milliseconds (e.g. "58946.3 ms"), unreadable at a glance. Now divided by
1000 and labeled "s".

Also redesigned: submitting an answer and seeing the next question appear on
Timed Quiz took roughly 6 seconds, because two sequential Claude calls sat
back-to-back on the critical path — `GradeAsync` for the just-submitted
answer, then `GetQuestionAsync` for the next question. A first attempt
deferred ALL grading to Continue to Results (`Task.WhenAll` over every
attempt right before showing the score) — fully built, but the student
reported no perceptible change even after a full rebuild, and it was shelved
rather than kept, since `HandleAnswer` → `LoadNextQuestion` was still one
sequential await chain either way; it just relocated the `GradeAsync` call
into a different sequential chain later instead of removing it from a
critical path.

The redesign that replaced it takes both calls off the critical path instead
of relocating one of them:

- **Prefetching.** `LoadNextQuestion` kicks off `_prefetchTask =
  FetchQuestionWithRetry()` (fire-and-forget, not awaited) for question N+1
  as soon as question N is actually on screen, instead of waiting for the
  student to click Next. The next `LoadNextQuestion` call consumes
  `_prefetchTask` (awaiting it if it hasn't resolved yet) instead of
  starting a fresh fetch, so the round-trip to Claude for the next question
  mostly happens while the student is still reading/answering the current
  one. `FetchQuestionWithRetry` was pulled out of `LoadNextQuestion` to do
  the actual fetch-plus-truncation-retry work and hand back a `FetchResult`
  (question + per-call timing/token stats + any retry notice) WITHOUT
  touching `RecordCall`/`RecentNotice` directly — those side effects are
  applied by `LoadNextQuestion` only once it actually promotes the result to
  the displayed question, so a truncation retry on a still-in-flight
  prefetch of N+1 can't show its notice under question N.
- **Background grading.** `HandleAnswer` no longer awaits `GradeAsync`
  before adding the attempt — it adds the attempt with `Grade = null` and
  calls `GradeInBackground(questionNumber)`, which grades and writes the
  result back on its own schedule via `InvokeAsync` (a background `Task`'s
  continuation after a real `await` has no guaranteed `SynchronizationContext`,
  so mutating component state from it needs `InvokeAsync` — same reasoning
  as `uiRefreshTimer`'s callback). `HandleSaveHistoryEdit`'s immediate-
  re-grade path (a regular attempt's answer edited from history) uses the
  same `GradeInBackground` instead of awaiting inline, and `HandleAnswer`'s
  callers plus `HandlePrevious`/`HandleNext`'s history branch went back to
  being synchronous (`void`) rather than `async Task`, since nothing left in
  them needs to be awaited by the caller.
  - A "run generation" guard (`_quizRunId`, bumped in `StartQuiz`) protects
    against a background grade landing after the student has already quit
    and started a new quiz — `GradeInBackground`'s continuation checks
    `runId != _quizRunId` and no-ops if the run it belongs to is gone,
    rather than writing a grade into the wrong quiz's `Attempts` list.
  - `_pendingGradingTasks` collects every in-flight background grade;
    `HandleContinueToResults` (now `async Task`) awaits all of them before
    building the final score, as a safety net for whatever hasn't finished
    by the time the student gets there — grading one sentence is almost
    always faster than reading the rest of the quiz, so in practice this
    list is usually already empty by then.
  - `TimedQuizLogic.ShouldDeferGradingOnSave` and the mark-for-review batch
    grading in `HandleFinishMarked` are unchanged — deliberately out of
    scope here, since marked questions are meant to stay ungraded until the
    student is done editing them regardless of how grading itself runs.

Not yet confirmed: whether the original ~6 second latency was actually
dominated by `GetQuestionAsync` itself (the assumption this redesign is
built on) or partly by a truncation retry silently doubling the call — the
diagnostic questions asked after the first (batch-grading) attempt were
never answered before it got shelved. Prefetching removes the fetch from
the critical path either way, so it should help regardless of which
explanation is right, but the actual before/after latency hasn't been
re-measured yet.

Also added: both quiz modes now show a proper spinning loading indicator
(a rotating circle + "Loading question…" text) instead of bare text while a
question is being fetched — `app.css`'s `.spinner`/`@keyframes spin` are
shared by Quiz.razor's own loading branch and `QuizRunner.razor`'s (the
component TimedQuiz delegates to). Worth a note for future edits to this
file specifically: `@keyframes` needs `@@keyframes` only inside a Razor
`<style>` block (`.razor` files) — `app.css` is plain CSS with no Razor
parser touching it, so the same escaping there is invalid and silently
breaks the animation (the spinner renders but never rotates). That
double-escaping mismatch, plus `QuizRunner.razor`'s loading branch
originally having no `.spinner` element at all, were the two real bugs
behind an extended "the spinner isn't showing" debugging session — a couple
of `Task.Delay`/`StateHasChanged()` additions tried along the way turned
out to be unnecessary once both were fixed and were removed again, except
one `StateHasChanged()` call kept in `Quiz.razor`'s `LoadQuestion` (removing
it there measurably stopped the loading state from rendering, unlike in
TimedQuiz.razor, where `CurrentQuestion` is already null-and-rendered by a
separate method — `StartQuiz` — before the fetch ever starts).

Also added: Home.razor's "Topic" dropdown is no longer four hardcoded
`<option>`s — it's built from a new `QuizSettings.Topics` (`List<string>`,
seeded with the original four topics), persisted through the same
SettingsPersistence JSON round-trip as every other setting. A new "Manage
Topics" section in the Settings panel gives full CRUD over that list: add,
rename, and delete, each backed by a pure `QuizTopicsLogic` static method
(`AddTopic`/`RenameTopic`/`RemoveTopic` — text/list in, list-or-null out,
unit tested in `QuizTopicsLogicTests.cs` without touching Blazor or the
filesystem, same split as `ClaudeQuizProvider`'s pure helpers). Delete is
the one action with a confirmation step, done via the browser's native
`confirm()` through the existing `IJSRuntime` (same pattern Quiz.razor
already uses for its localStorage avoid-list) rather than a second modal
stacked on the settings panel. Deleting or renaming whichever topic is
currently selected (`Settings.Topic`) keeps that selection consistent —
delete falls back to "Any topic," rename follows the topic to its new name
— so the dropdown can never end up pointing at a name that no longer exists
in the list.

Also fixed: `Quiz.razor`'s avoid-list (`RecentPrompts`, the rolling list of
recently-shown prompts replayed into `GetQuestionAsync` so Claude doesn't
repeat itself) used to be one flat queue shared across every topic, and it
was persisted to `localStorage` under a single key — so it survived
indefinitely across page loads and browser sessions, not just within one
quiz run. Adding a new topic (e.g. "Claude Certified Architect") and
answering a few questions on it fed those prompts into that same shared
list, which then got replayed as the avoid-list for every *other* topic
too, including "Claude Certified Developer" — and per `BuildAvoidClause`'s
own comment in `ClaudeQuizProvider`, a longer, less-topically-relevant
avoid-list measurably pushes Claude toward longer, more-elaborate
questions to differentiate against it (worse at Hard difficulty, where
Claude already reaches for more detail). Changed `RecentPrompts`/
`RecentFormats` to `Dictionary<string, Queue<T>>` keyed by topic, and
switched the `localStorage` persistence to a new key
(`recentPromptsByTopic`, a JSON object keyed by topic) rather than
migrating the old flat-array key — starting each topic's history empty is
harmless. `TimedQuiz.razor` didn't need this fix: its `RecentPrompts` is
already cleared at the start of every run and isn't persisted, so a mixed-
topic run (`Topic == ""`, random across all) intentionally shares one
avoid-list for that run only, and never leaks into a later run.

The per-topic dictionary bookkeeping behind that fix was pulled out of
`Quiz.razor` into a new pure class, `StudyAI/Services/RecentHistoryLogic.cs`
(`AddToTopic`/`ForTopic`/`RemoveTopic` — dictionaries in, dictionaries out,
non-mutating, same convention as `QuizTopicsLogic`), unit tested in
`RecentHistoryLogicTests.cs`. The two tests that actually matter
(`AddToTopic_ToOneTopic_LeavesOtherTopicsUntouched`,
`AddToTopic_AcrossTwoTopics_KeepsThemIndependent`) are direct regression
coverage for the cross-topic contamination bug above.

Also added: even with per-topic scoping, a single topic's avoid-list still
grows toward its 5-item cap with regular use — and once full, it *stays*
full indefinitely (it's persisted to `localStorage`), so every future
question for that topic sits on the longer/more-elaborate end from then on.
Notably, just loading `Quiz.razor` counts toward this even if the question
is never answered (a question is generated and shown immediately in
`OnInitializedAsync`), so repeatedly opening Regular Quiz and navigating
back to Home a few times is enough to saturate a topic's avoid-list on its
own. Home.razor's Manage Topics section now has a "Clear history" (↺)
button per topic, next to Rename/Delete, backed by
`RecentHistoryLogic.RemoveTopic` — it reaches directly into the
`recentPromptsByTopic` `localStorage` key via `IJSRuntime` (there's no live
`Quiz.razor` instance to talk to from `Home.razor`), and resets just that
topic's avoid-list back to empty. No confirmation step, unlike Delete —
clearing history can't lose a topic or any quiz progress. A shared
`TopicNotice` field (styled like `TopicError` but green) gives feedback
("Cleared ... question history") and gets reset by every other Manage
Topics action so it can't linger and read as feedback for the wrong one.

Also added: the avoid-list's per-topic cap (was a fixed `MaxRecentPrompts =
5` constant, duplicated in both `Quiz.razor` and `TimedQuiz.razor`) is now
`QuizSettings.AvoidListSize` — a single student-configurable value (range
1-15, default 5, "applies to all quizzes" like `Model`/`DifficultyFilter`)
persisted through the same `SettingsPersistence` JSON round-trip as
everything else. The Settings panel's hint text is deliberately honest
about the tradeoff rather than framing it as "bigger is better": raising it
means fewer repeats, but per `BuildAvoidClause`'s own comment in
`ClaudeQuizProvider`, it also means questions sit on the longer/more-
elaborate end more of the time once a topic's list fills up — the same
"Clear history" buttons above are the other lever if that's the actual
problem, not just this number. Each topic's "Clear history" button also now
shows a live count ("↺ 3") of how many prompts are currently saved for it,
read from the same `recentPromptsByTopic` `localStorage` key via a new
`RefreshTopicHistoryCountsAsync` in `Home.razor`, loaded once on page init
(the only time it can go stale, since it's `Quiz.razor` — a different
component instance — that actually updates it) and patched locally the
moment a clear succeeds rather than waiting for a reload.

Also added: Regular Quiz's answer footer now has a "Next" button alongside
Submit, visible only before grading (`Result is null`) — previously Submit
was the *only* way to move forward and stayed disabled until an answer was
picked, so there was no way to skip a question you didn't want to answer.
Next just calls `LoadQuestion()` directly, bypassing `GradeAsync` entirely,
so it never touches `TopicStats`/`CorrectCount`/`TotalCount` — skipping
isn't graded, so it can't help or hurt your score. Styled as a secondary
outlined button (`.btn-skip`) next to the filled-gradient `.btn-next`, so it
reads as the lesser action.

Also fixed: `ClaudeQuizPlanner`'s "Suggested next" recommendation used to
completely ignore `QuizSettings.DifficultyFilter` — the prompt always told
Claude to start conservative regardless of what the student had already
configured ("Medium if 1-2 questions in, Hard only after 5+"), and every
fail-closed fallback (no history, no API key, API error, exception) was
hardcoded to `"Medium"` too. A student practicing at Hard would see
"Suggested next" recommend Medium even on the very first question of the
session, before any API call happened. `IQuizPlanner.RecommendNextAsync`
grew a new trailing optional `baselineDifficulty` parameter (same additive
pattern as everything else on that interface); `Quiz.razor`'s `Recommend()`
now passes `Settings.DifficultyFilter` through. The actual guidance is
`ClaudeQuizPlanner.BuildProgressionGuidance` (new, pure, unit tested): when
the baseline is a specific level (not empty, and not `"Mixed"` — the
"student wants variety" choice, which keeps the original
gradual-progression-from-Medium behavior unchanged), that level becomes the
default Claude is told to lean toward — explicitly "don't drop down just
because of one wrong answer." It can still recommend lower, but only when
the correct/total counts already in the prompt show the student is missing
*most* of their recent questions on that specific topic, and even then the
`reason` has to say the drop is temporary, so it reads as "shore up
fundamentals, then come back" rather than the original bug's silent,
unexplained downgrade. Recommending *above* the baseline is still allowed
when they're doing very well — this stays a genuine two-way adaptive
routing decision (the actual "Agents and Workflows" point of this feature)
rather than a rigid floor that can only ever say "stay put."
`ResolveFallbackDifficulty` keeps every fail-closed fallback path (no
history, no API key, API error, exception — none of which have any
performance data to judge "struggling" from anyway) pinned to the baseline
itself, so even a failed API call won't quietly drop a student below their
chosen difficulty before a real recommendation was ever computed.

## Open threads / next steps

- `WasTruncated` exists and is tested but **not wired into `GradeAsync` yet** —
  the plan is: on confirmed truncation, retry once with a higher `max_tokens`.
  `GradeResult.Outcome` (see above) is now in place as the prerequisite this
  needed — a retry can check `Outcome == GradeOutcome.GradingFailed` before
  firing instead of guessing from `IsCorrect` alone.
- SQLite logging discussed as a future step for an eval/audit trail (why a
  grade came back the way it did), not started.
- `create_multiple_choice_question`'s schema *describes* `correctAnswer` as
  needing to exactly match one of the `choices` entries, but nothing in
  `ClaudeQuestionToolParser` validates that — it's trusted the same way
  iteration 2/3's prompted JSON trusted the model to follow instructions.
  Grading itself is unaffected either way (`GradeAsync` judges meaning, not
  an exact string match against `Choices`), but if `choices` ever comes back
  without an entry matching `correctAnswer`, the user has no way to select
  the actually-correct option from the radio buttons at all. Not observed
  yet; worth adding a consistency check in the parser (fall back to `null`,
  same as a missing required field) if it turns out to happen.
- The format-hint fix (see above) hasn't been re-verified against a longer
  real run yet. `BuildFormatHintClause` nudges toward the minority format on
  any non-tied split (3-2 counts, not just 4-1/5-0) — worth watching whether
  that's enough correction pressure over a full study session, or whether
  Claude still drifts back toward multiple-choice once the window (`RecentFormats`
  caps at 5) rolls past a corrected question.
- The recommendation call in `Quiz.razor` fires automatically after every
  grade (see `Recommend()`) — a real cost/latency tradeoff (a third Claude
  call per question cycle) made on purpose to keep the routing decision
  demonstrable, called out explicitly in a code comment rather than hidden.
  Worth revisiting if it ever feels like it's getting in the way of a study
  session rather than helping it.
- Agents and Workflows (14.7%) went from zero coverage to `IQuizPlanner`
  above — Model Selection and Optimization (16.8%) got there first via the
  model picker.
- History-edit re-grading (see the Previous/Next navigation entry above) has
  no confirmation or cost hint before it fires — every Save on an
  already-graded question is a real paid `GradeAsync` call, same as the live
  question, but nothing in the UI says so up front the way the session-stats
  line does after the fact. Worth watching whether that surprises anyone in
  practice, or whether it needs an inline "this will re-grade" note before
  the click rather than only a cost that shows up in the running total.
- `IQuizProvider.GetQuestionAsync` is up to 5 parameters now (`topic`,
  `model`, `difficulty`, `recentPrompts`, `recentWasMultipleChoice`) —
  approaching "introduce a parameter object" territory, but not there yet.
  Deliberately not refactored: each parameter is its own documented,
  additive exception to "the interface never changes" (see Architecture),
  and collapsing them into one request type now would blur that per-iteration
  git history for a purely organizational win with no CCDV-F concept behind
  it — the kind of polish-for-its-own-sake this file says to avoid. Tripwire:
  if a 6th parameter shows up, that's the signal to actually do it.
- The prefetch + background-grading redesign (see above) hasn't been
  measured yet — worth confirming the ~6 second submit-to-next-question
  latency actually dropped now that the fetch happens ahead of time, and
  watching for any visible stutter when a background grade's `InvokeAsync`
  write-back lands while the student is mid-navigation.
- Also fixed: starting a Timed Quiz straight from the home screen (the
  normal path — `TimedQuiz.razor`'s `OnInitializedAsync`) ignored
  `Settings.PreferredFormat` entirely, leaving `TimedQuizConfig.PreferredFormat`
  at its default of `QuestionFormat.Any`. With `Any`, `ClaudeQuizProvider.
  BuildToolsAndPrompt` offers both the multiple-choice and short-answer tools
  with `tool_choice: "auto"` and lets Claude pick — so a student with
  "Multiple choice only" set in Settings could still get served a free-text
  question on a timed quiz. `Quiz.razor` already had the string→enum
  conversion (`StringToQuestionFormat`); `TimedQuizConfigForm`'s own
  "Configure" screen also already set `PreferredFormat` correctly from its
  own format picker — only the Settings-driven Quick-Start-on-load path was
  missing the conversion. Fixed by adding the same `StringToQuestionFormat`
  helper to `TimedQuiz.razor` and using it in `OnInitializedAsync`.
  Deliberately left `HandleQuickStart` (the explicit "Quick Start" button on
  `QuickStartOrConfigure.razor`) alone — its own doc comment says it means
  "ignore my settings, use defaults: 10 questions, mixed difficulty, no time
  limits," so `Any` format there is intentional, not the same bug. No unit
  test added for this one — like the pre-existing "Next Question stays
  disabled" bug, the wiring lives directly in component `@code`, not in one
  of the pure/testable logic classes, so there's no seam to test it through
  without spinning up the full Blazor component.
- Regular Quiz (Quiz.razor) rebuilt to match Timed Quiz's screen as closely
  as possible: a shared sticky `.quiz-header` (progress/score + a
  TimedQuiz-style "Quit Quiz" button, no progress bar or timers though —
  Regular Quiz has no fixed question count and is deliberately untimed, so
  neither would mean anything real here), and a shared `.answer-nav-footer`
  with Previous/Next replacing the old Submit → "Next Question" flow.
  "Submit" is gone: the same Next button now grades-and-shows-feedback on
  first click, then advances on a second click, exactly like TimedQuiz's
  Next already did double duty. Skip (renamed from "Next" to "Skip →" to
  stop colliding with the primary button's own "Next →" label) still moves
  on ungraded, still isn't recorded — unchanged behavior, matching TimedQuiz
  (no skip concept there either). Previous/Next history browsing is
  **read-only** (Regular Quiz can review a past answer's correctness and
  explanation, but not edit and re-grade it, unlike TimedQuiz's history
  mode) — a deliberate scope call to avoid TimedQuiz's
  GradeInBackground/run-id staleness machinery for a study mode that
  already gives immediate feedback anyway; revisit if editing history turns
  out to matter in practice. Reuses `QuestionAttempt` (TimedQuiz's model)
  and `TimedQuizLogic`'s pure `GetPreviousViewIndex`/`GetNextViewIndex`/
  `CanGoPrevious`/`CanGoNext`/`IsAtLastHistoryEntry` directly rather than
  duplicating them — they were already generic (plain ints/bools, no
  TimedQuiz-specific types) so there was nothing TimedQuiz-specific to
  extract.
  Doing this rewrite surfaced several live instances of the exact
  non-isolated-`<style>`-block collision this file already documents for
  `.eliminate-btn`: Quiz.razor and QuizRunner.razor were independently
  declaring `.question-header`/`.difficulty-badge`/`.question-type`/
  `.options-container`/`.option-button` (+ hover/selected/eliminated
  states)/`.option-radio`/`.selected-indicator`/`.radio-circle`/
  `.option-text` with different values (purple vs. blue accents in
  particular), and Quiz.razor's own `.btn-next`/`.answer-nav-footer` were
  quietly losing to TimedQuiz's more-specific `.answer-nav-footer .btn-next`
  selector regardless of which page was showing. All of these are now
  single definitions in app.css's "Shared quiz-runner visual language"
  block (blue, matching TimedQuiz, since "make Regular Quiz look like Timed
  Quiz" was the point) — TimedQuiz.razor and QuizRunner.razor's own
  `<style>` blocks had their duplicate copies removed and now just pick up
  the shared rules. `.load-error-detail` was a similar collision
  (Quiz.razor's bordered-box treatment vs. TimedQuiz's plain-text
  treatment) but for two genuinely different-looking cards by design, so
  that one was fixed by scoping (`.question.load-error .load-error-detail`)
  rather than consolidating.
- Added `StudyAI.ToolChoiceDemo`, a console app in the same solution
  (`ProjectReference` on `StudyAI.csproj`, no duplicated logic) isolating
  one concept: `tool_choice: "auto"` vs. forced, the mechanic
  `ClaudeQuizProvider.GetQuestionAsync` uses to let Claude pick between
  `create_multiple_choice_question`/`create_short_answer_question` instead
  of the app dictating the format. It's the one call site in StudyAI where
  `tool_choice` genuinely varies by caller preference — `GradeAsync`'s
  `submit_grade` and `RecommendNextAsync`'s `recommend_next` both force a
  single tool because there's only one valid output shape, so there's
  nothing to isolate there. Part 1 calls the pure static
  `BuildToolsAndPrompt` directly (no network) to show exactly what gets
  sent per `QuestionFormat`; Part 2 makes live calls through the real
  `GetQuestionAsync`, running the `Any` case four times in a row so the
  actual auto-pick can be seen varying. Config is read from StudyAI's own
  `appsettings.json`/`appsettings.Development.json` (both optional,
  relative paths) plus environment variables — no new place for the API
  key to live. See `StudyAI.ToolChoiceDemo/README.md` for how to run it.

## Known issue: live API key committed in `appsettings.json`

`StudyAI/appsettings.json` (the base config file, not just the
Development-only one) currently has a real `Claude:ApiKey` value in it.
`.gitignore` doesn't exclude `appsettings.json` anywhere, so this contradicts
the "Never commit a real key" rule below. As of this note the file is
still untracked (`git status` shows it as `??`), so nothing has actually
been pushed with the key in it yet — but the very next
`git add .`/`git add -A` would sweep it into history. Rotate the key and
move it into `appsettings.Development.json` (already conventionally
gitignored for ASP.NET Core projects — worth double-checking this repo's
`.gitignore` actually covers it, since as of this note it doesn't appear
to) or user-secrets, then confirm `appsettings.json` itself only ever
ships placeholder/empty values.

## Conventions

- **Never `git commit` automatically.** Stage changes if asked, and hand off a
  commit message — the user runs the commit themselves.
- Code comments that touch Claude/AI logic tie back to the specific CCDV-F
  exam domain and its published weight, e.g. `// Tools and MCPs (10.6%): ...`.
  Domains: Applications and Integration (33.1%), Model Selection and
  Optimization (16.8%), Agents and Workflows (14.7%), Prompt and Context
  Engineering (11.0%), Tools and MCPs (10.6%), Security and Safety (8.1%),
  Claude Code (3.1%), Eval/Testing/Debugging (2.6%).
- One concept per git commit where practical — the git history itself is part
  of the study material, meant to be diffed iteration-to-iteration.
- `Claude:ApiKey` / `Claude:Model` come from `appsettings.json` / user-secrets.
  Never commit a real key. Model defaults to `claude-sonnet-4-5`.
