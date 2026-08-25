using System.Text;
using System.Text.Json;
using StudyAI.Models;

namespace StudyAI.Services;

// Agents and Workflows (CCDV-F, 14.7%): this is Anthropic's "routing" workflow
// pattern — a forced tool call classifies the input (a student's per-topic
// performance so far this session) and directs the next step (topic +
// difficulty), rather than the app always leaving that choice to a plain
// dropdown or letting the model free-write a suggestion in prose. Same
// tool_choice-forced mechanics as ClaudeQuizProvider.GradeAsync (Tools and
// MCPs, 10.6%), applied to a decision instead of a grade.
public class ClaudeQuizPlanner : IQuizPlanner
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public ClaudeQuizPlanner(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    // → RecommendNextAsync: main entry point for routing decisions
    public async Task<NextStepRecommendation> RecommendNextAsync(IReadOnlyList<TopicPerformance> history, string? model = null, bool restrictToCurrentTopic = false, string? baselineDifficulty = null)
    {
    // → Extract fallback topic for error cases
        var fallbackTopic = history.Count > 0 ? history[^1].Topic : "C# Basics";

        // → Fallback difficulty for every fail-closed path below (no history,
        // no API key, API error, exception): used to be a hardcoded "Medium"
        // regardless of what the student had configured, which is exactly the
        // bug this parameter fixes — a student practicing at Hard would see
        // "Suggested next" quietly regress to Medium even before any API call
        // happened. See ResolveFallbackDifficulty.
        var fallbackDifficulty = ResolveFallbackDifficulty(baselineDifficulty);

        // Checked before the API key: "no history yet" is knowable for free
        // and true on the very first question of a session, so there's no
        // reason to make that case depend on configuration at all.
    // → Early exit: no history yet — don't call API
        if (history.Count == 0)
            return new NextStepRecommendation(fallbackTopic, fallbackDifficulty, "Answer a question first — no performance history yet.");

        var apiKey = _config["Claude:ApiKey"];
    // → Early exit: no API key configured
        if (string.IsNullOrWhiteSpace(apiKey))
            return new NextStepRecommendation(fallbackTopic, fallbackDifficulty, "No Claude API key configured.");

        var summary = string.Join("; ", history.Select(h => $"{h.Topic}: {h.Correct}/{h.Total} correct"));

    // → Build prompt constraint based on recommendation mode
        var topicConstraint = restrictToCurrentTopic && history.Count > 0
            ? $" The student must keep studying {history[0].Topic}; do not recommend a different topic."
            : "";

        var progressionGuidance = BuildProgressionGuidance(baselineDifficulty);

        var payload = new
        {
            model = ClaudeQuizProvider.ResolveModel(model, _config["Claude:Model"]),
            max_tokens = 300,
        // → Tool definitions: recommend_next (forced choice)
            tools = new object[]
            {
                new
                {
            // → Tool: recommend_next — routes to topic and difficulty
                    name = "recommend_next",
                    description = "Recommend the next quiz topic and difficulty for a student based on their performance so far.",
                    input_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            topic = new { type = "string", description = "The topic to study next." },
                            difficulty = new { type = "string", description = "Easy, Medium, or Hard." },
                            reason = new { type = "string", description = "One sentence explaining the recommendation." }
                        },
                        required = new[] { "topic", "difficulty", "reason" }
                    }
                }
            },
            tool_choice = new { type = "tool", name = "recommend_next" },
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $"A student's performance so far this session, by topic: {summary}. " +
                              $"Recommend the next difficulty level for them to practice.{topicConstraint} " +
                              $"{progressionGuidance} Call recommend_next with your pick."
                }
            }
        };

        // → Create POST request to Claude API
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
        // → Send request to Messages API
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new NextStepRecommendation(fallbackTopic, fallbackDifficulty, $"API error ({response.StatusCode}): recommendation unavailable.");

            // Parsing lives in ClaudeRecommendationParser so it's testable
            // without a network call. See
            // StudyAI.Tests/ClaudeRecommendationParserTests.cs.
            // → Parse tool response into recommendation object
            var recommendation = ClaudeRecommendationParser.Parse(body, fallbackTopic);

            // → Extract token usage for cost tracking
            var (inputTokens, outputTokens) = ClaudeUsageParser.Parse(body);
            return recommendation with { InputTokens = inputTokens, OutputTokens = outputTokens };
        }
        catch (Exception ex)
        {
            // Network-level failures only at this point — response-shape
            // failures are handled inside ClaudeRecommendationParser.
            return new NextStepRecommendation(fallbackTopic, fallbackDifficulty, $"Recommendation failed: {ex.Message}");
        }
    }

    // → Pulled out for the same reason ClaudeQuizProvider.BuildAvoidClause
    // was: directly testable without HttpClient. This is the actual fix for
    // a reported bug — a student practicing at Hard (QuizSettings.
    // DifficultyFilter) would see "Suggested next" recommend Medium anyway,
    // because the prompt always told Claude to start conservative ("Medium
    // if 1-2 questions in, Hard only after 5+") with no awareness of what
    // the student had already configured. When baselineDifficulty is a
    // specific level (not null/empty, and not "Mixed" — the "no fixed
    // target" choice), that level becomes the default AND the thing Claude
    // is told not to abandon lightly: don't drop down over a single wrong
    // answer, only when the correct/total counts already in the prompt show
    // the student is missing MOST of their recent questions on that specific
    // topic — and even then, say in the reason that it's temporary, so
    // dropping down still reads as "shore up fundamentals, then come back"
    // rather than a silent, unexplained downgrade (the original bug). Still
    // allowed to recommend above the baseline if they're doing very well —
    // this is the actual "Agents and Workflows" routing decision, not just a
    // fixed floor, so it stays adaptive both directions instead of only ever
    // saying "stay put." "Mixed" keeps the original
    // gradual-progression-from-Medium guidance unchanged, since there's no
    // single level to anchor to there.
    public static string BuildProgressionGuidance(string? baselineDifficulty)
    {
        if (!string.IsNullOrWhiteSpace(baselineDifficulty) && baselineDifficulty != "Mixed")
        {
            return $"The student has set their practice difficulty to {baselineDifficulty}. Recommend " +
                   $"{baselineDifficulty} by default and lean toward staying there — don't drop down just " +
                   "because of one wrong answer. Only recommend a lower difficulty if they're missing most " +
                   "of their recent questions on this specific topic (look at the correct/total counts " +
                   "above), and if you do, say in the reason that it's temporary so they can shore up " +
                   $"fundamentals before returning to {baselineDifficulty}. Only recommend higher than " +
                   $"{baselineDifficulty} if they're doing very well at it.";
        }

        return "Progression should be gradual: recommend Medium if they're 1-2 questions in and doing well, " +
               "Hard only if they've done 5+ questions well. Recommend Easy if they're struggling.";
    }

    // → Same "specific level vs. Mixed" rule as BuildProgressionGuidance,
    // applied to the fail-closed fallback value instead of the prompt —
    // kept as its own method (rather than inlining the same condition twice)
    // so the two can't silently drift apart.
    public static string ResolveFallbackDifficulty(string? baselineDifficulty) =>
        !string.IsNullOrWhiteSpace(baselineDifficulty) && baselineDifficulty != "Mixed" ? baselineDifficulty : "Medium";
}
