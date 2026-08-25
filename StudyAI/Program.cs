using StudyAI.Components;
using StudyAI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Applications and Integration (CCDV-F, 33.1%): AddHttpClient<TInterface, TImpl>
// is the standard DI-managed HttpClient pattern — pooled connections, no manual
// disposal. This single line is also the entire "iteration 1 -> 2" swap: same
// IQuizProvider contract, StaticQuizProvider's in-memory list traded for
// ClaudeQuizProvider's API calls. Nothing else in the app changed to make that work.
builder.Services.AddHttpClient<IQuizProvider, ClaudeQuizProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
});

// Agents and Workflows (CCDV-F, 14.7%): IQuizPlanner is registered separately
// from IQuizProvider — it's a distinct capability (deciding what's next, not
// generating or grading content), so it gets its own DI-managed HttpClient
// rather than being bolted onto ClaudeQuizProvider.
builder.Services.AddHttpClient<IQuizPlanner, ClaudeQuizPlanner>(client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
});

// Model Selection and Optimization (CCDV-F, 16.8%), Prompt and Context Engineering (11.0%):
// QuizSettings is a scoped service holding quiz-specific settings (prefetch toggle, difficulty,
// model, etc.) that students can experiment with on the home screen. Exposed as a teaching tool
// so students can feel the latency/cost tradeoff of optimizations directly rather than just
// reading about it. SettingsPersistence is a singleton that handles saving/loading settings
// to a JSON file on disk, so preferences are retained across sessions.
builder.Services.AddScoped<QuizSettings>();
builder.Services.AddSingleton<SettingsPersistence>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
