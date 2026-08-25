# AgentWorkflows: Agents and Workflows Study Project

## Overview

AgentWorkflows is a .NET 8 console application demonstrating Anthropic's agentic workflow patterns for CCDV-F (Claude Certified Developer, Foundations) exam study. It isolates four distinct agent patterns, each in its own file, runnable from a menu. The project prioritizes readability: every pattern can be read top-to-bottom without UI framework noise, and each prints its control flow to the console so you can watch decisions happen in real time.

## Why This Matters

This project teaches one core lesson: the difference between **forced tool calls** (where there's one right output shape) and **autonomous tool routing** (`tool_choice: "auto"`, where Claude decides). Every pattern except the autonomous loop forces a specific tool because that step has exactly one valid answer shape. The autonomous loop is the only place `tool_choice: "auto"` appears — because it's the only place the model genuinely needs to steer itself.

This forced-vs-auto contrast is what the Agents and Workflows domain (14.7%) actually tests on the exam.

## Architecture

### ClaudeClient: The Shared Wrapper

A thin, deliberate wrapper around the raw Messages API (`POST /v1/messages`). Not using the official Anthropic SDK — seeing the request/response shape is the point.

**Key methods:**
- `SendAsync(payload)` — makes one API call, returns parsed `JsonDocument` (not a typed model). Fails loud on HTTP errors.
- `SendStreamingAsync(payload)` — same request mechanics, but reads the response as a live SSE stream (`IAsyncEnumerable<JsonDocument>`, one event per line). Only `StreamingExample` uses this.
- `GetText(response)` — extracts the first `text` block from a plain (non-tool) reply.
- `GetToolUses(response)` — pulls every `tool_use` block. A forced call has exactly one; an autonomous turn can have several.
- `GetUsage(response)` — extracts `input_tokens` and `output_tokens`. Printed after each call so cost is visible, not abstract.

Why thin? Each pattern file handles the interesting part ("what do we do with this response?"), not the plumbing.

---

## The Four Patterns

### 1. Routing Example

**Pattern:** Classify once, route once.

**Mechanics:**
- **Step 1** — forced tool call: `classify_ticket`. Claude receives a customer message and *must* call the tool, returning a category (`billing`, `technical`, `account`, `general`) and reasoning.
- **Step 2** — free-text call: uses the category-specific system prompt from a dictionary, answers the customer.

**Why forced in step 1?** Exactly one right output shape (a category). No ambiguity, no interpretation.

**Why free-text in step 2?** The answer can vary widely. A system prompt steers the tone and depth, but Claude doesn't need to fill a rigid structure.

**CCDV-F domains:**
- Agents and Workflows (14.7%) — routing as a named pattern.
- Tools and MCPs (10.6%) — forced `tool_choice`.
- Prompt and Context Engineering (11.0%) — system prompt specialization per route.

**Tokens:** ~200 in + ~400 out per run (two calls, ~600 total).

---

### 2. Orchestrator-Workers Example

**Pattern:** Plan variably, work independently, synthesize.

**Mechanics:**
- **Step 1 — Orchestrator** — forced tool call: `plan_sections`. Claude breaks a report topic into 2-4 focused sections. Output shape is guaranteed: an array of sections, each with title and brief.
- **Step 2 — Workers** — concurrent calls, one per planned section. Each worker only sees its own section brief, not the overall plan. Run in parallel via `Task.WhenAll` because they're independent.
- **Step 3 — Synthesis** — free-text call. Takes all the independently-written sections and stitches them into one cohesive report.

**Key difference from routing:** The number and shape of subtasks isn't fixed in the code — it's decided by the orchestrator call's output. You can't write the worker calls until you know how many sections the plan has.

**Why parallel is safe here:** Each worker is independent. Orchestrator-workers *enables* parallelization. Other patterns (evaluator-optimizer) don't — each step depends on the previous one's output.

**CCDV-F domains:**
- Agents and Workflows (14.7%) — orchestrator-workers as a named pattern, parallelization via `Task.WhenAll`.
- Tools and MCPs (10.6%) — forced `tool_choice` to guarantee the plan structure.
- Applications and Integration (33.1%) — async/await, concurrent task handling.

**Tokens:** ~400 in step 1, ~400 per worker × N workers (usually 3), ~800 in synthesis. Total ~2000+ for a full run.

---

### 3. Evaluator-Optimizer Example

**Pattern:** Generate, critique, retry with feedback, loop until approved.

**Mechanics:**
- **Iteration N** — Generate: free-text call. On the first pass, no feedback yet. On passes 2+, folds the previous iteration's critique straight into the prompt as "here's the feedback you need to address."
- **Evaluate** — forced tool call: `evaluate_haiku`. Claude reads the draft haiku and judges it against explicit criteria (5-7-5 syllables, concrete imagery, no clichés). Returns `approved` (boolean) and `feedback` (string).
- Loop: if approved, done. Otherwise, feed feedback back into the next generation attempt.
- **MaxIterations = 3:** Safety cap. If Claude's evaluator never approves, give up after 3 tries and say so. Prevents runaway API spend.

**Why forced in evaluate?** The rubric is explicit and checkable. The tool isn't just vibes-checking — it's judging against stated criteria every time.

**Sequential, not parallel:** Each iteration depends on the previous one's output. No way to parallelize.

**Contrast with orchestrator-workers:** Orchestrator-workers runs many things in parallel (independent subtasks). Evaluator-optimizer runs one thing sequentially, repeatedly (dependent iterations). Same project, opposite tradeoff — worth noticing back-to-back.

**CCDV-F domains:**
- Agents and Workflows (14.7%) — evaluator-optimizer as a named pattern, feedback loops.
- Tools and MCPs (10.6%) — forced `tool_choice` with explicit criteria.
- Security and Safety (8.1%) — `MaxIterations` cap prevents unbounded cost.
- Prompt and Context Engineering (11.0%) — feedback threaded back into the next prompt.

**Tokens:** ~150 per generation + ~250 per evaluation. With 3 iterations max, ~1200 total in the worst case.

---

### 4. Autonomous Agent Loop Example

**Pattern:** Claude decides its own next action. Multi-turn. No script.

**Mechanics:**
- **Setup:** Two tools available: `calculate` (evaluates arithmetic expressions) and `finish_task` (Claude calls this when it has the final answer).
- **tool_choice = "auto"** — *not* forced. Claude decides each turn whether to call a tool, which one, or just reply with text.
- **Turn loop:**
  1. Send the current messages (growing each turn with prior assistant replies and tool results).
  2. Get the response. If no tool call → Claude answered directly, end.
  3. If `finish_task` → Claude has the answer, extract it, end.
  4. For any `calculate` calls → execute locally (via `DataTable.Compute` for arithmetic), wrap each result as a `tool_result` message.
  5. Append the assistant's content block to messages, append the tool results as a user message, loop to turn 2.
- **MaxTurns = 8:** Same safety cap as Evaluator-Optimizer. Prevents runaway loops.

**Why "auto" here?** Because finishing (calling `finish_task`) and continuing (calling `calculate`) are *both* valid next steps. The model has to steer itself. This is the only place in the entire project where the model genuinely controls the flow, not just filling in a shape the code decided.

**Growing messages:** Unlike every other pattern (which are stateless one-shot calls), this maintains a real, growing conversation. Messages list keeps expanding: `[user request, assistant content, tool results, assistant content, tool results, ...]`. Each turn sees the full history.

**Tool result mechanics:** Claude never runs a tool itself. Every `tool_use` gets executed locally → the result comes back as a `tool_result` message → Claude sees it and decides what to do next. That request/execute/respond cycle, repeated, is the mechanical core of "an agent."

**CCDV-F domains:**
- Agents and Workflows (14.7%) — autonomous agent loop, the general agent pattern itself.
- Tools and MCPs (10.6%) — `tool_choice: "auto"`, tool_result message type.
- Applications and Integration (33.1%) — growing conversation state, message list management.
- Security and Safety (8.1%) — `MaxTurns` cap for safety.

**Tokens:** ~500 per turn max, up to 8 turns. Worst case ~4000, but typically 1000-2000 for a realistic problem.

---

## Forced vs. Auto: The Core Lesson

| Aspect | Forced Tool (`tool_choice: "tool", name: "X"`) | Auto Tool (`tool_choice: "auto"`) |
|--------|---|---|
| Use when | One right output shape for this step | Multiple valid next steps, model decides |
| Examples | Routing classifier, orchestrator planner, evaluator | Autonomous agent loop |
| Does Claude decide to call a tool? | No — forced. | Yes — Claude picks. |
| Can Claude refuse to call the tool? | No. | Yes — can reply with text or call a different tool. |
| Predictability | High — you know exactly what you're getting. | Lower — you have to handle multiple response types. |
| Complexity | Simpler code: you know the tool will be called. | More complex: you handle "no tool call", "tool A", "tool B", etc. |

The exam tests this: when do you force a tool, and when do you let Claude choose? Answer: force when there's one right shape, let it choose when multiple valid paths exist.

---

## Running the Examples

### Setup
```bash
cd AgentWorkflows
dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."
```

### Run
```bash
dotnet run
```
A menu appears. Pick 1-5 to run a pattern, 0 to exit.

### What to Watch
- **Routing:** Notice step 1 forces a category, step 2 adapts the prompt. Costs split: ~200 in + ~400 out.
- **Orchestrator:** Watch the "Step 2" line show N concurrent calls finishing. Then step 3 combines them. Token cost visible per call.
- **Evaluator:** Watch the loop. If approved in iteration 1, done fast. If not, feedback gets threaded back, next draft addresses it.
- **Agent:** Watch the messages list grow. Each turn adds the assistant's reply and tool results. Claude sees its own prior work.

---

## Tools Defined in This Project

| Pattern | Tool Name | Input | Output |
|---------|-----------|-------|--------|
| Routing | `classify_ticket` | category, reasoning | category enum, reasoning string |
| Orchestrator | `plan_sections` | sections array | array of {title, brief} |
| Evaluator | `evaluate_haiku` | approved, feedback | boolean, string |
| Agent | `calculate` | expression string | numeric result |
| Agent | `finish_task` | answer string | final answer |

---

## Key Code Patterns

### Pattern 1: Forced Tool Call
```csharp
var payload = new {
    model = client.Model,
    max_tokens = 200,
    tools = new object[] { /* tool definition */ },
    tool_choice = new { type = "tool", name = "classify_ticket" },
    messages = new[] { new { role = "user", content = input } }
};
var response = await client.SendAsync(payload);
var toolUse = ClaudeClient.GetToolUses(response).First();
var category = toolUse.GetProperty("input").GetProperty("category").GetString();
```

### Pattern 2: Autonomous Tool Loop
```csharp
var messages = new List<object> { new { role = "user", content = task } };
for (var turn = 1; turn <= MaxTurns; turn++) {
    var payload = new {
        // ...
        tool_choice = new { type = "auto" },
        messages
    };
    var response = await client.SendAsync(payload);
    var toolUses = ClaudeClient.GetToolUses(response);
    
    // Append assistant reply to messages
    messages.Add(new { role = "assistant", content = response.RootElement.GetProperty("content").Clone() });
    
    // If tools were called, execute and append results
    if (toolUses.Count > 0) {
        var results = /* execute each tool */;
        messages.Add(new { role = "user", content = results.ToArray() });
    } else {
        break;  // No tool call = done
    }
}
```

---

## CCDV-F Domain Mapping

This project touches these domains across all patterns:

- **Agents and Workflows (14.7%)** — routing, orchestrator-workers, evaluator-optimizer, autonomous loop.
- **Tools and MCPs (10.6%)** — forced tool calls, auto tool routing, tool_result messages.
- **Applications and Integration (33.1%)** — raw HTTP mechanics, streaming, async/await, message management.
- **Prompt and Context Engineering (11.0%)** — system prompts, user messages, feedback threading.
- **Model Selection and Optimization (16.8%)** — token counting, cost visibility.
- **Security and Safety (8.1%)** — MaxIterations/MaxTurns caps.

---

## Study Strategy

1. **Read the code first.** Pick one pattern file. Read top to bottom. Predict what will happen.
2. **Run it.** Execute the pattern from the menu. Watch the trace. How close was your prediction?
3. **Change it.** Modify `MaxIterations` (evaluator), `MaxTurns` (agent), or a prompt. Re-run. Feel the impact on cost and behavior.
4. **Compare side-by-side.** Routing (parallel fixed paths) vs. Orchestrator (parallel variable paths) vs. Evaluator (sequential iterations) vs. Agent (sequential autonomous). See the tradeoffs.
5. **Answer the core question:** When would you use each pattern? When would you force a tool vs. let Claude choose? Your answer to that question is what the exam tests.

---

## Open Threads

- No tests yet. The `Calculate` method could be tested independently; worth pulling into a helper if more logic accumulates.
- `StreamingExample` only handles `text_delta` events. Tool inputs stream as `input_json_delta` (partial JSON fragments that need reassembly) — not yet built.
- The `calculate` and `classify_ticket` examples are intentionally simple placeholders. The pattern, not the task, is the lesson.
