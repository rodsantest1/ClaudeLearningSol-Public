# StudyAI — commit-to-exam table of contents

A reading guide through the git history, ordered the way it happened. Each
row is a real commit (`git log --oneline`); rows with no exam domain listed
are plumbing/UX commits that don't demonstrate a specific CCDV-F (Claude
Certified Developer, Foundations) concept. See `CLAUDE.md` for the full
architecture writeup this table is indexing.

## Chronological

| Commit | Date | What it did | CCDV-F domain(s) |
|---|---|---|---|
| `d5b1247` | 08-06 | `.gitattributes`/`.gitignore` | — |
| `45bf34a` | 08-06 | Initial project scaffold | — |
| `95b343a` | 08-07 | **Iteration 1**: static quiz app — `IQuizProvider` seam established, hardcoded question bank, string-compare grading | — (foundation: the seam every later domain plugs into) |
| `ae3087f` | 08-07 | Fix: `RenderMode` static using | — |
| `f0b4fab` | 08-07 | **Iteration 2**: `ClaudeQuizProvider` — one-line DI swap from the hardcoded bank to live Messages API calls | Applications and Integration (33.1%), Prompt and Context Engineering (11.0%), Security and Safety (8.1%) |
| `7db7592` | 08-07 | Unit tests for provider contract + response parsing | Eval, Testing, and Debugging (2.6%) |
| `08df929` | 08-07 | Process-of-elimination + mark-for-review UX | — |
| `43ddf39` | 08-07 | Eliminated-choice/button styling | — |
| `566cc00` | 08-07 | "Claude Certified Developer" practice-question topic added to `StaticQuizProvider` | Content spans all 8 domains (offline, not live) |
| `c24f7e9` | 08-07 | Loading/grading state indicators | — |
| `f94f3dd` | 08-08 | **Iteration 3**: free-text questions + forced `tool_choice` grading (`submit_grade`), `ClaudeGradeParser` extracted | Tools and MCPs (10.6%), Eval, Testing, and Debugging (2.6%) |
| `832add8` | 08-08 | `CLAUDE.md` project documentation added | Claude Code (3.1%) |
| `5268037` | 08-08 | Haiku/Sonnet model picker + Stopwatch timing display | Model Selection and Optimization (16.8%) |
| `aeff438` | 08-08 | `ResolveModel` extracted for testability | Model Selection and Optimization (16.8%), Eval, Testing, and Debugging (2.6%) |
| `f634b37` | 08-08 | Token usage tracking (`ClaudeUsageParser`, session cost aggregate) | Model Selection and Optimization (16.8%) |
| *(uncommitted)* | 08-10 | **Iteration 4**: `IQuizPlanner`/`ClaudeQuizPlanner` — adaptive topic/difficulty routing via forced `recommend_next` tool call | Agents and Workflows (14.7%), Tools and MCPs (10.6%) |

## By domain (heaviest weight first)

Use this list to prioritize review time against what's actually tested most.

| Domain | Weight | Covered by |
|---|---|---|
| Applications and Integration | 33.1% | `f0b4fab` |
| Model Selection and Optimization | 16.8% | `5268037`, `aeff438`, `f634b37` |
| Agents and Workflows | 14.7% | *(uncommitted)* iteration 4 |
| Prompt and Context Engineering | 11.0% | `f0b4fab` |
| Tools and MCPs | 10.6% | `f94f3dd`, *(uncommitted)* iteration 4 — tool-calling covered; MCP (Model Context Protocol) itself is not |
| Security and Safety | 8.1% | `f0b4fab` (fail-closed patterns run through every provider/planner after this) |
| Claude Code | 3.1% | `832add8` |
| Eval, Testing, and Debugging | 2.6% | `7db7592`, `f94f3dd`, `aeff438` |

## Gaps

- Model Context Protocol (the "MCP" half of Tools and MCPs) — not touched.
  Everything under Tools and MCPs so far is tool-calling, a related but
  distinct concept.
- Claude Code and Eval/Testing/Debugging are covered structurally (this repo
  *is* a Claude Code project; the test suite exists) but not deeply — lowest
  combined weight (5.7%), so lowest priority to shore up.
