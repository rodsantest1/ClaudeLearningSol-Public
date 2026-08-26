# StudyAI

A Blazor Server exam trainer for the **Claude Certified Developer, Foundations (CCDV-F)**
certification — built specifically so the app itself is the study material. Every
feature exists to demonstrate a real CCDV-F concept in working code, not just to make
the quiz app nicer. If you're studying for this exam, the fastest way to use this repo
is to read it, not just run it.

This README is the entry point for a developer who wants to either (a) run the app and
quiz themselves, or (b) trace exam concepts to the actual code that demonstrates them.
For the full build history and architecture rationale, see [`CLAUDE.md`](./CLAUDE.md);
for a commit-by-commit map of what landed when, see [`EXAM_TOC.md`](./EXAM_TOC.md) (note:
that file's commit table stops at iteration 4 — the domain tour below is the current,
complete picture).

## Quick start

**Stack:** .NET 8, Blazor Server (Interactive Server render mode), no database. Tests
are xUnit, with zero network calls anywhere in the suite.

1. Clone the repo and open `ClaudeLearningSol.slnx`, or run everything from the CLI:
   ```
   cd StudyAI
   dotnet user-secrets init
   dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."
   dotnet run
   ```
2. Open the URL `dotnet run` prints (typically `https://localhost:5001` or similar).
3. Pick **Quiz** (self-paced, one question at a time, immediate grading) or **Timed
   Quiz** (configurable question count/time limits, mark-for-review, a results
   breakdown by difficulty) from the home screen.

**Use your own API key via `dotnet user-secrets`, not `appsettings.json`.** See
[Before you share this repo](#before-you-share-this-repo) below — this matters more
than it sounds.

## What you can do in the app

- **Quiz** (`/quiz`) — one question at a time, topic/model/difficulty/format pickers,
  immediate grading, a running session cost readout, mark-for-review, and an optional
  "suggested next" recommendation powered by a separate routing call.
- **Timed Quiz** (`/timed-quiz`) — Quick Start (sensible defaults) or Configure (question
  count, per-question/total time limits, difficulty filter, format preference). Supports
  pause/resume, mark-for-review with deferred grading, and a results screen with a
  difficulty breakdown and a collapsible "review missed questions" section.
- **Settings** (the ⚙️ panel on the home screen) — model tier (Haiku vs. Sonnet),
  difficulty filter, question count, format preference, topic list management, avoid-list
  size, and a next-question prefetch toggle. These aren't just preferences — they're
  meant to be experimented with (see the domain tour below).

## The exam domain tour

This is the part that makes the app worth reading, not just playing with. Each row is a
CCDV-F domain, its exam weight, and where in this codebase to actually see it — with a
short note on what to pay attention to once you're there. Ordered by weight, heaviest
first, since that's how you'd actually want to prioritize review time.

| Domain | Weight | Where to look | What to notice |
|---|---|---|---|
| **Applications and Integration** | 33.1% | `Program.cs` (DI/`HttpClient` registration); `ClaudeQuizProvider.cs` (the whole class) | Raw Messages API mechanics: `AddHttpClient<TInterface, TImpl>`, the `x-api-key`/`anthropic-version` headers, POST body construction, and validating model output before trusting it (`ClaudeQuestionToolParser`) instead of assuming the response is well-formed. |
| **Model Selection and Optimization** | 16.8% | `IQuizProvider.cs`'s `model` parameter; `Settings` panel's Haiku/Sonnet picker; `ClaudeUsageParser.cs`; the session cost readout in `Quiz.razor`/`TimedQuiz.razor`; `max_tokens` handling in `ClaudeQuizProvider.GetQuestionAsync` | Token counts are what "cost" actually means — `ClaudeUsageParser` pulls `usage.input_tokens`/`output_tokens` off every response and both quiz modes show a running "N API calls · tokens total (in/out)" line. `max_tokens` itself is a real lever here too: too tight and generation gets truncated (`stop_reason: "max_tokens"`) before the tool call finishes — the app auto-retries once with a larger budget and shows why (see `TimedQuizLogic.BumpMaxTokens` and the transient notice in `TimedQuiz.razor`). |
| **Agents and Workflows** | 14.7% | `IQuizPlanner.cs` / `ClaudeQuizPlanner.cs`; `TopicPerformance.cs` | A deliberately separate seam from `IQuizProvider` — it doesn't generate or grade content, it answers "what should happen next" via a *forced* tool call (`recommend_next`) reading per-topic performance so far. This is Anthropic's "routing" workflow pattern: classify the input, direct the next step, rather than a plain dropdown or a free-written suggestion. |
| **Prompt and Context Engineering** | 11.0% | `ClaudeQuizProvider.BuildAvoidClause`/`BuildToolsAndPrompt`; `RecentHistoryLogic.cs`; the `system` vs. user-message split in `GetQuestionAsync` | The system prompt sets a persistent role ("you generate exam questions"); the user prompt carries the per-request instruction (topic + difficulty + avoid-list) — splitting role instructions from task instructions is the actual lesson. The avoid-list itself is a case study in a prompt design failure mode worth reading closely: telling a model "don't repeat these" with no length constraint anywhere pushed it toward writing longer, more elaborate questions to *prove* it was different, which compounded over a run. The fix (an explicit "vary the concept, not the amount of detail" instruction, plus capping how much of each past prompt gets replayed back) is a small, concrete example of how prompt wording — not just content — shapes model behavior. |
| **Tools and MCPs** | 10.6% | `ClaudeQuestionToolParser.cs`; `ClaudeGradeParser.cs`/`ClaudeRecommendationParser.cs`; `ClaudeQuizProvider.BuildToolsAndPrompt` | Three different `tool_choice` values live in this codebase and each means something different: `"auto"` (`GetQuestionAsync` — Claude picks between two question-shape tools, or could reply with plain text, which is why `Parse` returns `null` as a real outcome), and forced `{type: "tool", name: "..."}` (`GradeAsync`/`RecommendNextAsync` — there's only one right output shape, so the model literally cannot reply any other way). Tool *selection* is also a lever independent of the prompt: `preferredFormat` changes which tools get offered at all. Note this covers tool-calling specifically — Model Context Protocol (the "MCP" half of this domain) isn't touched anywhere in this repo. |
| **Security and Safety** | 8.1% | The `ErrorQuestion` sentinel pattern throughout `ClaudeQuizProvider.cs`; `ClaudeGradeParser`'s `GradingFailed` flag; `StaticQuizProvider`'s own practice question on API-key handling | Fail-closed is the running theme: a missing API key, a non-2xx response, a truncated or malformed tool call — none of these throw or crash the UI. They return an explicit, typed "this didn't work" result the caller has to handle, with the specific reason attached (see `ClaudeQuestionToolParser.DescribeFailure`) rather than a bare `null` or a generic error. Separately — and this is a live example, not just a lesson — see [Before you share this repo](#before-you-share-this-repo) below. |
| **Claude Code** | 3.1% | [`CLAUDE.md`](./CLAUDE.md) itself | This repo *is* a Claude Code project — `CLAUDE.md` is the persistent project context read at session start, kept current as a stated convention ("update the relevant section here as part of that work — don't wait to be asked"). Reading how it's structured and maintained is itself the demonstration. |
| **Eval, Testing, and Debugging** | 2.6% | `StudyAI.Tests/` (the whole project); the extraction pattern described below | The test suite makes zero network calls — every `Claude*Parser`/`*Logic` class takes a canned request/response shape in and returns a typed result out, so behavior is asserted against fixed inputs instead of a live, non-deterministic API. That's the actual eval strategy this app teaches: you can't unit-test "did Claude write a good question," but you can unit-test "given this exact truncated response body, does the parser fail closed with the right reason." |

## The pattern worth copying: pull pure logic out of anything that touches HTTP

The single architectural idea repeated across almost every file in `Services/` is this:
if logic can be expressed as *typed input in, typed output out* — no `HttpClient`, no
`IJSRuntime`, no Blazor component lifecycle — pull it into its own static class so it's
directly unit-testable without mocking anything. `ClaudeQuizProvider` and
`ClaudeQuizPlanner` are thin orchestration shells around HTTP calls; essentially all of
the actual decision-making lives in classes like `ClaudeQuestionToolParser`,
`ClaudeGradeParser`, `ClaudeUsageParser`, `TimedQuizLogic`, `RecentHistoryLogic`, and
`QuizTopicsLogic` — each with a matching test file in `StudyAI.Tests/` built from canned
JSON bodies, not live API responses. Every one of `StudyAI.Tests`' ~20 test files follows
this shape. If you're adding something new to this repo, that's the pattern to match.

## Running the tests

```
cd StudyAI.Tests
dotnet test
```

No API key or network access needed — the whole suite runs offline.

## Before you share this repo

`StudyAI/appsettings.json` currently has a live Anthropic API key committed in plain
text (already flagged as a known issue in `CLAUDE.md`). If you're handing this repo to
other developers to use as a teaching tool, **rotate/revoke that key and remove it from
git history first** — otherwise anyone who clones the repo gets a working credential on
your account. Have each developer set their own key locally instead:

```
dotnet user-secrets set "Claude:ApiKey" "sk-ant-their-own-key"
```

This is, unhelpfully, exactly the scenario `StaticQuizProvider`'s own Security and
Safety practice question warns about: *"Store it via environment variables, a secrets
manager, or user-secrets — never commit it."*

## Going deeper

- [`CLAUDE.md`](./CLAUDE.md) — full architecture writeup and iteration-by-iteration
  build history, written to be read by both Claude Code and humans.
- [`EXAM_TOC.md`](./EXAM_TOC.md) — a commit-to-exam-domain table of contents (currently
  covers through iteration 4; the domain tour above is the up-to-date superset).
- [`quiz-architecture-diagram.html`](./quiz-architecture-diagram.html) — a visual diagram
  of `GetQuestionAsync`/`GradeAsync`/`RecommendNextAsync` and which caller (Quiz vs.
  Timed Quiz) reaches which tool.
