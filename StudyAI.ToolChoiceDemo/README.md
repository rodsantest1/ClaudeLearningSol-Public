# StudyAI.ToolChoiceDemo

A small console app that isolates one concept out of the full StudyAI Blazor
app: `tool_choice: "auto"` vs. forced tool choice — the mechanic
`ClaudeQuizProvider.GetQuestionAsync` uses to let Claude pick between two
tools (`create_multiple_choice_question`, `create_short_answer_question`)
instead of the app dictating the question format.

**Why this call site specifically:** Tools and MCPs is 10.6% of the CCDV-F
exam, and `GetQuestionAsync` is the *only* place in StudyAI where
`tool_choice` actually varies based on a caller's preference. Every other
tool call in the app (`GradeAsync`'s `submit_grade`, `RecommendNextAsync`'s
`recommend_next`) forces one specific tool because there's only one valid
output shape — there's no interesting "auto" case to show there.

## No duplicated logic

This project takes a `ProjectReference` on `StudyAI.csproj` and calls the
real `ClaudeQuizProvider` — same class, same code path the Blazor app uses.
Nothing here is a simplified stand-in that could drift out of sync with what
StudyAI actually does. It just strips away Blazor, DI, localStorage, and
page navigation so the `tool_choice` mechanic is the only thing left to
look at.

## What it shows

**Part 1** calls the pure static method `ClaudeQuizProvider.BuildToolsAndPrompt`
directly — no network call — and prints, for each `QuestionFormat`
(`MultipleChoice`, `ShortAnswer`, `Any`), which tools get offered and what
`tool_choice` value gets sent. This is the actual control-lever code.

**Part 2** makes live calls through the real `GetQuestionAsync` as proof the
above is really what happens. `MultipleChoice`/`ShortAnswer` are forced —
Claude can't reply any other way — so those two are just a baseline. `Any`
is run four times in a row so you can watch `tool_choice: "auto"` make a
genuinely different pick from call to call.

## Running it

From the solution root:

```
dotnet run --project StudyAI.ToolChoiceDemo
```

It needs a `Claude:ApiKey` to run Part 2 (the live calls). It reads
config the same way `ClaudeQuizProvider` expects, checking in order:

1. `StudyAI/appsettings.json`
2. `StudyAI/appsettings.Development.json`
3. Environment variables

All three are optional — if none of them have a key, the app prints a clear
message and skips Part 2 rather than crashing. Part 1 (the pure method) always
runs and needs no key at all.

No new config file is created here on purpose — it reuses StudyAI's existing
`Claude:ApiKey` rather than asking you to duplicate a secret into a second
place.
