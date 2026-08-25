# StudyAI: Full CCDV-F Exam Concepts in a Running App

## Overview

StudyAI is a Blazor Server web app — a quiz/exam trainer powered by Claude. Unlike AgentWorkflows (console patterns in isolation), StudyAI demonstrates the *full breadth* of CCDV-F domains inside one running system. It was built deliberately iteration-by-iteration so each exam concept lands as a distinct, diffable commit in git history, not buried in one giant build. The app itself is secondary; it exists to demonstrate concepts, not be production-ready.

**Key difference from AgentWorkflows:** AgentWorkflows goes deep on Agents and Workflows (one domain, five patterns). StudyAI goes wide: every domain gets some coverage, all five iterations stacked in one app. You study by reading git diffs, not just code files.

## Why This Structure

Each iteration answers one question:
- Iteration 1: Can you build a quiz app with zero AI?
- Iteration 2: Can you swap in AI via DI and keep the rest of the app untouched?
- Iteration 3: Can you move from defensive JSON parsing to guaranteed tool schemas?
- Iteration 4: Can you add a routing decision layer without changing the question-generation layer?
- Iteration 5: Can you let Claude choose between two valid output shapes (tool_choice: "auto")?

By the end, you've seen: seams and DI, multiple prompt engineering strategies, forced vs. auto tool routing, state management for self-correction, defensive error handling, token counting, and adaptive workflows — all in context.

## Stack

- **.NET 8** Blazor Server (Interactive Server render mode)
- **No database** — everything is in-memory per session
- **xUnit tests** — zero network calls in the test suite. All Claude calls tested via static mock providers.
- **DI container** — two seams: `IQuizProvider` and `IQuizPlanner`

## Architecture

### Two Seams

**IQuizProvider:** The core quiz logic.
- `GetQuestionAsync(topic, model?, difficulty?, recentPrompts?, recentWasMultipleChoice?)` — generates a question.
- `GradeAsync(question, userAnswer, model?)` — grades a user's answer.

Two implementations:
- `StaticQuizProvider` — hardcoded MCQ bank, no network. Offline fallback and test fixture.
- `ClaudeQuizProvider` — talks to the API. Currently registered in DI.

**IQuizPlanner:** The routing decision (added iteration 4).
- `RecommendNextAsync(history, model?)` — looks at session performance and recommends "try topic X at difficulty Y".

One implementation:
- `ClaudeQuizPlanner` — calls Claude with a forced `recommend_next` tool, gets back a routing decision.

### Why Two Seams?

The routing decision is genuinely separate from content generation. `IQuizPlanner` is its own interface (not a third method on `IQuizProvider`) so routing-as-a-concept is visible in the architecture, not buried as a side effect.

### Pure Logic Classes (Testable Without Mocking HttpClient)

- `ClaudeResponseParser` (iterations 2-3) — parsed prompted JSON from raw text. Kept for git history (shows the "before" of the tool_choice evolution).
- `ClaudeQuestionToolParser` (iteration 5+) — reads `tool_use` blocks, branches on which tool was called.
- `ClaudeGradeParser` — turns a grading response into a `GradeResult` with proper failure handling.
- `ClaudeRecommendationParser` — turns a routing response into a `NextStepRecommendation`.
- `ClaudeUsageParser` — pulls token counts from every response.
- `ClaudeQuizProvider.ResolveModel` — model tier fallback logic.
- `ClaudeQuizProvider.BuildAvoidClause` — turns recent prompts into an avoid-list instruction.
- `ClaudeQuizProvider.BuildFormatHintClause` — nudges toward whichever question format has been underused.

Pattern: if logic is pure (text/data in, typed result out), pull it into a static class so it's unit testable without mocking `HttpClient`.

---

## Iteration Walkthrough

### Iteration 1: Static Quiz App
**Concept:** Build a working app with zero AI, zero network.

**What's here:** 
- Hardcoded MCQ bank in `StaticQuizProvider` (5 questions, 4 choices each).
- Quiz page (`Quiz.razor`) loops: load question, user picks a choice, check against `correctAnswer` via string compare, show result.
- Score tracking.

**CCDV-F coverage:** Applications and Integration (33.1%) — basic Blazor state management.

**Why this matters:** Establishes the app shell and DI seam. Iteration 2 replaces the provider *without touching the rest of the app*.

---

### Iteration 2: Claude-Generated MCQ (Prompted JSON)
**Concept:** Swap in AI via DI. Same app, different provider.

**What's here:**
- `ClaudeQuizProvider` implements `IQuizProvider`.
- `GetQuestionAsync` sends a prompt asking for JSON. Raw response comes back as text, possibly wrapped in prose or code fences. `ClaudeResponseParser` defensively reads out the JSON, tries to parse it, returns `null` on failure.
- Grading still uses string compare (identical to iteration 1 on purpose — shows the progression).

**CCDV-F coverage:**
- Applications and Integration (33.1%) — `HttpClient`, request/response mechanics, DI.
- Prompt and Context Engineering (11.0%) — asking nicely for JSON output.
- Tools and MCPs (10.6%) — zero tools here, but establishes the baseline for later iterations.

**Why this matters:** Shows that swapping the provider doesn't require rewriting the UI. Also establishes the problem: "asked nicely for JSON, parsed defensively" is fragile. Set up for iteration 3.

---

### Iteration 3: Free-Text Questions + Tool-Use Grading
**Concept:** Move from defensive parsing to guaranteed tool schemas.

**What's here:**
- Questions are now free-text (no fixed choices). `Choices` array comes back empty from `ClaudeQuestionToolParser`.
- `GradeAsync` no longer does string compare. It calls Claude with a forced `tool_choice`: a `submit_grade` tool that Claude must call. Input schema specifies `isCorrect` and `explanation`. Output shape is *guaranteed* by the API, not hoped for from a prompt.
- New: `GradeResult` can distinguish "graded as wrong" from "grading failed" (missing/incomplete tool call, API error, etc.).

**CCDV-F coverage:**
- Tools and MCPs (10.6%) — forced `tool_choice`, input/output schemas as the source of truth.
- Prompt and Context Engineering (11.0%) — system prompt specialization, "judges meaning not wording" instructions.
- Eval/Testing/Debugging (2.6%) — handling tool call failures, incomplete responses.

**Why this matters:** Shows the power of tools: one JSON schema replaces pages of defensive parsing logic. Also introduces failure modes that matter: truncation (checked via `WasTruncated`), malformed responses, etc. Sets up for the grading-failure-is-not-a-wrong-answer fix later.

---

### Iteration 4: Adaptive Routing (IQuizPlanner)
**Concept:** Add a routing decision layer. After grading, ask Claude "what should the user try next?"

**What's here:**
- New interface: `IQuizPlanner`. `RecommendNextAsync` takes session history (per-topic stats: questions asked, pass rate) and calls Claude with a forced `recommend_next` tool.
- Tool input schema: empty (just triggers the call). Output: `topic`, `difficulty`, `reasoning` (one sentence).
- Quiz page gets a new "recommendation card" showing the suggested topic/difficulty, with a "Use this" button.
- `QuizQuestion` gains a `Difficulty` field so the recommendation can control what gets generated.
- After every grade (except grading failures), `Recommend()` fires and shows the card.

**CCDV-F coverage:**
- Agents and Workflows (14.7%) — routing pattern, adaptive decision-making.
- Tools and MCPs (10.6%) — forced tool call for routing (one right output shape).
- Applications and Integration (33.1%) — async state management, multiple seams.
- Prompt and Context Engineering (11.0%) — per-topic performance data threaded into the routing prompt.

**Why this matters:** This is the first hands-on agentic pattern in the app. Routing is one of Anthropic's core patterns. Also introduces the cost/latency tradeoff: an extra Claude call per question cycle, made on purpose so routing stays demonstrable instead of hidden. Open thread: is this cost worth it for UX?

---

### Iteration 5: Multi-Tool Question Generation (tool_choice: "auto")
**Concept:** Let Claude decide between two output shapes. First use of `tool_choice: "auto"`.

**What's here:**
- `GetQuestionAsync` offers two tools: `create_multiple_choice_question` and `create_short_answer_question`.
- `tool_choice = "auto"` — Claude picks which one fits the question better. Not forced.
- `ClaudeQuestionToolParser` reads the response, checks which tool got called, branches accordingly.
- If Claude replies with plain text instead of calling either tool, the parser returns `null` (the one real failure mode for "auto").
- **Side effect:** Process-of-elimination UI (built in iteration 1, dormant since iteration 3) works again whenever Claude picks MCQ.

**Also fixed along the way:**
- **Repeated questions:** `Quiz.razor` tracks the last 5 shown prompts in a `Queue<string>`. `BuildAvoidClause` turns that into "don't repeat/rephrase these" text appended to the generation prompt. *Lesson:* a stateless prompt converges on the "obvious" answer; feed it real history.
- **Grading failures:** `GradeResult` gained `GradingFailed` bool and a `Outcome` property (`Correct` / `Incorrect` / `GradingFailed`). `Quiz.razor` now skips score updates and routing calls if grading failed. *Lesson:* don't silently corrupt a score.
- **Tool_choice bias:** After iteration 5 landed, manual testing showed `tool_choice: "auto"` biasing 9-10/10 toward MCQ. Static instructions ("prefer short-answer") overcorrected in the other direction. The actual fix: neutral base prompt + `BuildFormatHintClause` that only nudges when recent history is actually lopsided. *Lesson:* static wording can't self-correct; feed real feedback.

**CCDV-F coverage:**
- Tools and MCPs (10.6%) — `tool_choice: "auto"` vs. forced, handling multiple valid shapes.
- Prompt and Context Engineering (11.0%) — system prompt designed to be neutral, feedback threaded back.
- Agents and Workflows (14.7%) — Claude steering its own output shape.
- Model Selection and Optimization (16.8%) — token counting visible per call and in session aggregate.

**Why this matters:** Forced tool calls guarantee one output shape. Auto tool calls require Claude to decide *and* the app to handle multiple cases. This is where the exam tests judgment: when do you force, and when do you let the model choose?

---

## The Forced vs. Auto Contrast (The Core Lesson)

| Aspect | Forced (`tool_choice: "tool", name: "X"`) | Auto (`tool_choice: "auto"`) |
|--------|---|---|
| Use when | One right output shape for this step | Two+ valid output shapes, let Claude pick |
| Examples | Grading (submit_grade), routing (recommend_next) | Question generation (MCQ vs. short-answer) |
| Guarantees | Tool will be called; schema is the truth | Model might reply with text; handle multiple cases |
| Parsing | One branch | Multiple branches (or no tool call) |
| Return value | Guaranteed tool_use | Might be null |

StudyAI shows this progression:
- Iteration 2: No tools (just ask nicely).
- Iteration 3: Forced tool for grading (one right shape).
- Iteration 4: Forced tool for routing (one right shape).
- Iteration 5: Auto tool for generation (two right shapes).

Each step demonstrates a tradeoff. The exam tests whether you know when to pick each.

---

## Key Code Patterns

### Pattern 1: DI Seam (Iteration 1 → 2 swap)
```csharp
// Program.cs
builder.Services.AddHttpClient<IQuizProvider, ClaudeQuizProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
});

// Quiz.razor
@inject IQuizProvider QuizProvider
...
var question = await QuizProvider.GetQuestionAsync(topic, model, difficulty, recentPrompts, recentWasMultipleChoice);
```

One line change in Program.cs swaps from `StaticQuizProvider` to `ClaudeQuizProvider`. The component code is identical.

### Pattern 2: Forced Tool Call (Iteration 3 Grading)
```csharp
var gradePayload = new
{
    model = client.Model,
    max_tokens = 300,
    tools = new object[]
    {
        new
        {
            name = "submit_grade",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    isCorrect = new { type = "boolean" },
                    explanation = new { type = "string" }
                },
                required = new[] { "isCorrect", "explanation" }
            }
        }
    },
    tool_choice = new { type = "tool", name = "submit_grade" },
    messages = new[] { new { role = "user", content = $"Grade this: {userAnswer}" } }
};
var response = await client.SendAsync(gradePayload);
var toolUse = response.RootElement.GetProperty("content")
    .EnumerateArray()
    .First(b => b.GetProperty("type").GetString() == "tool_use");
var isCorrect = toolUse.GetProperty("input").GetProperty("isCorrect").GetBoolean();
```

Forced call: Claude *must* call the tool. Parser expects exactly one tool_use block.

### Pattern 3: Auto Tool Call (Iteration 5 Generation)
```csharp
var generatePayload = new
{
    model = client.Model,
    max_tokens = 500,
    tools = new object[]
    {
        new { name = "create_multiple_choice_question", input_schema = /* ... */ },
        new { name = "create_short_answer_question", input_schema = /* ... */ }
    },
    tool_choice = new { type = "auto" },
    messages = new[] { new { role = "user", content = $"Generate a {difficulty} question about {topic}" } }
};
var response = await client.SendAsync(generatePayload);
var toolUses = response.RootElement.GetProperty("content")
    .EnumerateArray()
    .Where(b => b.GetProperty("type").GetString() == "tool_use")
    .ToList();

if (toolUses.Count == 0)
    return null;  // Claude replied with text, not a tool call

var toolName = toolUses[0].GetProperty("name").GetString();
if (toolName == "create_multiple_choice_question")
    return ClaudeQuestionToolParser.ParseMultipleChoice(toolUses[0]);
else
    return ClaudeQuestionToolParser.ParseShortAnswer(toolUses[0]);
```

Auto call: Claude might not call a tool, or call a different one. Parser branches on the result.

### Pattern 4: Feedback Loop (Iteration 5+ Format Hint)
```csharp
// Quiz.razor: track recent format choices
var recentFormats = new Queue<bool>();  // true = MCQ, false = short-answer
...
question = await QuizProvider.GetQuestionAsync(
    topic, 
    model, 
    difficulty, 
    recentPrompts,
    recentFormats  // <- real history fed back
);
if (question != null)
    recentFormats.Enqueue(question.Choices.Count > 0);  // <- history updated

// ClaudeQuizProvider
var formatHint = BuildFormatHintClause(recentWasMultipleChoice);
// formatHint says "nudge toward short-answer" only if recent history is actually lopsided
```

Real history fed into the prompt. No static preference; dynamic nudge based on actual behavior.

---

## CCDV-F Domain Coverage

| Domain | Coverage | Where |
|--------|----------|-------|
| Applications and Integration (33.1%) | HTTP mechanics, DI, async/await, state management, message handling | Iterations 1-5, all call sites |
| Agents and Workflows (14.7%) | Routing decision-making, adaptive paths, multi-step orchestration | Iteration 4 (IQuizPlanner), Iteration 5 (format hinting) |
| Model Selection and Optimization (16.8%) | Model tier fallback logic, token counting, cost visibility, speed tradeoffs | Model picker, usage display, `ResolveModel`, `ClaudeUsageParser` |
| Prompt and Context Engineering (11.0%) | System vs. user split, structured output, feedback threading, neutrality vs. bias | All iterations; especially iterations 3, 5 |
| Tools and MCPs (10.6%) | Forced tool calls, auto routing, input/output schemas, tool_use blocks, failure handling | Iterations 2-5; core to grading and routing |
| Security and Safety (8.1%) | Error handling, missing API keys, truncation detection, graceful degradation | All iterations; especially iteration 3+ grading failures |
| Eval/Testing/Debugging (2.6%) | Defensive parsing, null checks, `GradingFailed` state, test fixtures | `ClaudeResponseParser`, `ClaudeGradeParser`, tests |
| Claude Code (3.1%) | Not covered | — |

---

## Study Path

### Path 1: Understand Each Iteration (Git Diffs)
```bash
git log --oneline
# Pick a commit:
git show <commit>
```

Read the changes iteration by iteration. Each one is a small step. The git history *is* the study material.

### Path 2: Compare Parsing Strategies
- Iteration 2: `ClaudeResponseParser` — regex + defensive JSON parsing.
- Iteration 3/5: `ClaudeQuestionToolParser` / `ClaudeGradeParser` — tool_use blocks.

See how tool schemas remove entire categories of error handling.

### Path 3: Forced vs. Auto
- Grading (iteration 3): forced `submit_grade`. One right answer shape.
- Routing (iteration 4): forced `recommend_next`. One right answer shape.
- Generation (iteration 5): auto choice between two tools. Two right answer shapes.

Which pattern would you pick for a new task?

### Path 4: Self-Correction Mechanics
- Repeated questions (iteration 5): fed real history, not static bias.
- Format bias (iteration 5): nudge based on actual imbalance, not fixed preference.

How does the app learn and adapt?

### Path 5: Run It
```bash
cd StudyAI
dotnet run
```

Study for real. Watch the model picker's speed/cost tradeoff. Try different topics. See routing in action. Notice how the app avoids repeating questions.

---

## Open Threads (Next Steps)

- **Truncation retry:** `WasTruncated` exists but isn't wired. Plan: detect truncation → retry with higher `max_tokens`. Blocker: need a safe way to retry without corrupting state.
- **Consistency check:** MCQ `correctAnswer` must match one of `choices`. Not validated. If violated, user can't select the right answer.
- **Format-hint re-verification:** The nudge only applies on lopsided splits. Worth watching a longer study session to see if it overcorrects again.
- **Recommendation latency:** Every grade triggers a routing call. Real cost. Is the UX worth it?
- **Parameter object:** `GetQuestionAsync` is at 5 parameters. At 6, introduce a `QuestionRequest` record.

---

## Files to Know

- `StudyAI/Program.cs` — DI setup. Swap `IQuizProvider` or `IQuizPlanner` registration here.
- `StudyAI/Components/Pages/Quiz.razor` — the quiz page. State management, recommendation handling, topic/format tracking.
- `StudyAI/Services/IQuizProvider.cs` — the seam. Defines `GetQuestionAsync` and `GradeAsync`.
- `StudyAI/Services/ClaudeQuizProvider.cs` — the AI implementation. All API calls, payload building, token parsing.
- `StudyAI/Services/IQuizPlanner.cs` — routing seam.
- `StudyAI/Services/ClaudeQuizPlanner.cs` — routing implementation.
- `StudyAI/Services/ClaudeQuestionToolParser.cs` — branches on which tool was called.
- `StudyAI/Services/ClaudeGradeParser.cs` — parses grading responses.
- `StudyAI.Tests/` — all tests mock providers; zero network calls.
