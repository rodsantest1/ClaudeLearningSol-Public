using Microsoft.Extensions.Configuration;
using StudyAI.Models;
using StudyAI.Services;

// StudyAI.ToolChoiceDemo
//
// Isolates ONE concept out of the full Blazor app: tool_choice: "auto" vs.
// "forced", the specific mechanic ClaudeQuizProvider.GetQuestionAsync uses
// to let Claude pick between two tools (create_multiple_choice_question,
// create_short_answer_question) instead of the app dictating the format.
// Tools and MCPs is 10.6% of the CCDV-F exam, and this is the one call site
// in StudyAI where tool_choice actually varies based on a caller's
// preference — every other tool call (GradeAsync's submit_grade,
// RecommendNextAsync's recommend_next) forces one specific tool because
// there's only one valid output shape.
//
// This console app calls the SAME ClaudeQuizProvider the Blazor app uses —
// no duplicated logic, no simplified stand-in — so nothing here can drift
// out of sync with what StudyAI actually does. It just strips away Blazor,
// DI, localStorage, and page navigation so the mechanic is the only thing
// left to look at.

Console.WriteLine("=======================================================");
Console.WriteLine(" StudyAI.ToolChoiceDemo");
Console.WriteLine(" Tools and MCPs (CCDV-F, 10.6%): tool_choice auto vs. forced");
Console.WriteLine("=======================================================");
Console.WriteLine();

// → Config: reuses StudyAI's OWN appsettings.json / appsettings.Development.json
// rather than duplicating a Claude:ApiKey anywhere new — same "Claude" section,
// same keys ClaudeQuizProvider already reads via _config["Claude:ApiKey"] /
// _config["Claude:Model"]. Paths are relative to this project's own directory
// (where `dotnet run` sets the working directory), one level up and across into
// the sibling StudyAI project. All three sources are optional: if none of them
// have a key, ClaudeQuizProvider itself fails closed below with a clear message
// instead of this program crashing.
var config = new ConfigurationBuilder()
    .AddJsonFile("../StudyAI/appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile("../StudyAI/appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

// → Applications and Integration (33.1%): the same DI-managed-HttpClient
// shape Program.cs uses (AddHttpClient<IQuizProvider, ClaudeQuizProvider>),
// just constructed by hand instead of through the DI container — there's
// no ASP.NET Core host here to register services with.
var http = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/") };
var provider = new ClaudeQuizProvider(http, config);

const string topic = "Claude Certified Developer";
const string difficulty = "Medium";

// ── Part 1: BuildToolsAndPrompt is pure — no network call needed to see
// exactly which tools get offered and what tool_choice value each format
// preference resolves to. This is the actual control-lever code; the live
// API calls in Part 2 below are just proof it really does what this says.
Console.WriteLine("Part 1 — what BuildToolsAndPrompt sends, per preferred format");
Console.WriteLine("(pure method, no network call — this is the control lever itself)");
Console.WriteLine();

foreach (var format in new[] { QuestionFormat.MultipleChoice, QuestionFormat.ShortAnswer, QuestionFormat.Any })
{
    var (tools, toolChoice, _) = ClaudeQuizProvider.BuildToolsAndPrompt(
        topic, difficulty, format, recentPrompts: null, recentWasMultipleChoice: null);

    var toolNames = string.Join(", ", tools.Select(DescribeToolName));

    Console.WriteLine($"  preferredFormat: {format,-14} → tools offered: [{toolNames}]");
    Console.WriteLine($"  {"",25}   tool_choice: {DescribeToolChoice(toolChoice)}");
    Console.WriteLine();
}

// ── Part 2: live calls. MultipleChoice/ShortAnswer are forced — Claude
// literally cannot reply any other way once one tool is the only option, so
// these two are here mainly as a baseline. Any is the interesting one: it's
// called several times in a row so you can watch tool_choice: "auto" make a
// genuinely different pick call to call, instead of trusting a single
// sample.
if (string.IsNullOrWhiteSpace(config["Claude:ApiKey"]))
{
    Console.WriteLine("No Claude:ApiKey found (checked ../StudyAI/appsettings*.json and");
    Console.WriteLine("environment variables) — skipping the live API calls in Part 2.");
    Console.WriteLine("Set one the same way the main StudyAI app does and re-run to see it live.");
    return;
}

Console.WriteLine("-------------------------------------------------------");
Console.WriteLine("Part 2 — live calls through the real GetQuestionAsync");
Console.WriteLine("-------------------------------------------------------");
Console.WriteLine();

Console.WriteLine("Forced — MultipleChoice:");
await RunAndPrint(provider, topic, difficulty, QuestionFormat.MultipleChoice);

Console.WriteLine("Forced — ShortAnswer:");
await RunAndPrint(provider, topic, difficulty, QuestionFormat.ShortAnswer);

Console.WriteLine("Auto — Any (run 4x to watch the actual pick vary):");
for (var i = 1; i <= 4; i++)
{
    Console.Write($"  [{i}/4] ");
    await RunAndPrint(provider, topic, difficulty, QuestionFormat.Any, indent: "        ");
}

static async Task RunAndPrint(IQuizProvider provider, string topic, string difficulty, QuestionFormat format, string indent = "  ")
{
    var question = await provider.GetQuestionAsync(topic, difficulty: difficulty, preferredFormat: format);

    if (question.Topic == "Setup needed")
    {
        Console.WriteLine($"{indent}(failed: {question.Explanation})");
        return;
    }

    var actualFormat = question.Choices.Count > 0 ? "Multiple Choice" : "Short Answer";
    Console.WriteLine($"{indent}Claude picked: {actualFormat}");
    Console.WriteLine($"{indent}Prompt: {Truncate(question.Prompt, 100)}");
    Console.WriteLine();
}

static string Truncate(string text, int maxLength) =>
    text.Length <= maxLength ? text : text[..maxLength] + "…";

static string DescribeToolName(object tool)
{
    // → Each tool here is an anonymous object built inline in
    // BuildToolsAndPrompt (name/description/input_schema) — reflection is
    // the simplest way to read its "name" property back out from outside
    // that method without changing BuildToolsAndPrompt's return type just
    // for this demo's sake.
    var nameProp = tool.GetType().GetProperty("name");
    return nameProp?.GetValue(tool) as string ?? "?";
}

static string DescribeToolChoice(object toolChoice)
{
    var typeProp = toolChoice.GetType().GetProperty("type");
    var type = typeProp?.GetValue(toolChoice) as string ?? "?";

    if (type != "tool") return $"\"{type}\" (Claude picks)";

    var nameProp = toolChoice.GetType().GetProperty("name");
    var name = nameProp?.GetValue(toolChoice) as string ?? "?";
    return $"\"tool\", forced to {name}";
}
