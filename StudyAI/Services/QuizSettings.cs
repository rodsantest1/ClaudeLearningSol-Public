namespace StudyAI.Services;

/// <summary>
/// Global app settings shared across all quiz modes. Exposed on the home screen's
/// Settings panel so students can experiment with optimization tradeoffs (prefetch,
/// model selection, difficulty, question count) as a hands-on teaching tool.
/// Model Selection and Optimization (16.8%), Prompt and Context Engineering (11.0%).
/// </summary>
public class QuizSettings
{
    /// <summary>
    /// Which Claude model to use: "claude-haiku-4-5" or "claude-sonnet-4-5".
    /// Defaults to Sonnet (better quality). Students can switch to Haiku to feel
    /// the speed/quality tradeoff.
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-5";

    /// <summary>
    /// Question difficulty filter for generation: "Easy", "Medium", "Hard", or "Mixed".
    /// Applies to both Regular Quiz and Timed Quiz. Defaults to "Medium".
    /// </summary>
    public string DifficultyFilter { get; set; } = "Medium";

    /// <summary>
    /// Number of questions in a Timed Quiz (Regular Quiz has no fixed count).
    /// Range: 5-20. Defaults to 10.
    /// </summary>
    public int NumQuestions { get; set; } = 10;

    /// <summary>
    /// Prefetch the next question while the student is answering the current one.
    /// When true: faster response time (fetch happens in the background), but uses
    /// more API calls. When false: each question waits for the fetch before showing.
    /// Defaults to true (optimized for speed).
    /// </summary>
    public bool PrefetchNextQuestion { get; set; } = true;

    /// <summary>
    /// Topic filter for question generation: one of the entries in <see cref="Topics"/>,
    /// or "" (empty for any topic). Allows students to focus on one subject area.
    /// Defaults to "" (any topic).
    /// </summary>
    public string Topic { get; set; } = "";

    /// <summary>
    /// The student-editable list of topics offered in the Topic dropdown. Starts with
    /// the four topics that used to be hardcoded into Home.razor's markup; students can
    /// now add, rename, or remove entries via the Settings panel's "Manage Topics"
    /// section (see QuizTopicsLogic for the CRUD logic). Persisted through the same
    /// SettingsPersistence JSON round-trip as every other setting here.
    /// </summary>
    public List<string> Topics { get; set; } = new()
    {
        "C# Basics",
        "Web Fundamentals",
        "Databases",
        "Claude Certified Developer",
    };

    /// <summary>
    /// Question format preference: "Any", "MultipleChoice", or "ShortAnswer".
    /// Guides Claude's question generation. Defaults to "Any" (let Claude choose).
    /// </summary>
    public string PreferredFormat { get; set; } = "Any";

    /// <summary>
    /// How to handle AI routing recommendations: "Disabled", "Suggested", or "Auto".
    /// Disabled: no recommendations. Suggested: show and let student choose.
    /// Auto: apply automatically. Defaults to "Suggested".
    /// </summary>
    public string RecommendationMode { get; set; } = "Suggested";

    /// <summary>
    /// How many recently-shown prompts to remember per topic and feed back to
    /// Claude as a "don't repeat these" avoid-list — see
    /// <see cref="RecentHistoryLogic"/> (Regular Quiz, persisted per topic) and
    /// TimedQuiz.razor's own in-memory equivalent (reset every run). Applies
    /// to both. Higher means fewer repeated questions, but
    /// ClaudeQuizProvider.BuildAvoidClause's own comment documents the real
    /// tradeoff: a longer avoid-list measurably pushes Claude toward longer,
    /// more-elaborate questions to differentiate against it — so this isn't a
    /// free "bigger is always better" dial. Range: 1-15. Defaults to 5.
    /// </summary>
    public int AvoidListSize { get; set; } = 5;

    /// <summary>
    /// Raised whenever any setting changes, so components can react and quiz pages
    /// can pick up new settings on their next load.
    /// </summary>
    public event Action? SettingsChanged;

    public void NotifySettingsChanged() => SettingsChanged?.Invoke();
}
