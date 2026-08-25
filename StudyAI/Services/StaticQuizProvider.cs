using StudyAI.Models;

namespace StudyAI.Services;

// Iteration 1: no AI. A fixed question bank and a plain string comparison for
// grading. Everything here is deterministic and free to run.
public class StaticQuizProvider : IQuizProvider
{
    private static readonly List<QuizQuestion> Bank = new()
    {
        new("C# Basics", "Which keyword makes a field settable only in the constructor?",
            new[] { "const", "readonly", "static", "sealed" }, "readonly",
            "\"readonly\" fields can be assigned in the constructor but not after. \"const\" must be known at compile time."),

        new("C# Basics", "Which of these is a value type in C#?",
            new[] { "string", "struct", "class", "delegate" }, "struct",
            "Structs are value types (copied by value); classes, strings, and delegates are all reference types."),

        new("Web Fundamentals", "Which HTTP status code means \"created\"?",
            new[] { "200", "201", "301", "404" }, "201",
            "201 Created indicates a new resource was made, typically returned from a successful POST."),

        new("Web Fundamentals", "Which HTTP method is idempotent?",
            new[] { "POST", "PATCH", "PUT", "CONNECT" }, "PUT",
            "PUT replaces a resource wholesale, so repeating the same request has the same effect. POST usually isn't idempotent."),

        new("Databases", "Which SQL clause filters rows after grouping?",
            new[] { "WHERE", "HAVING", "ORDER BY", "GROUP BY" }, "HAVING",
            "HAVING filters aggregated groups; WHERE filters rows before grouping happens."),

        new("Databases", "In ACID, what does the \"I\" stand for?",
            new[] { "Integrity", "Isolation", "Idempotency", "Indexing" }, "Isolation",
            "Isolation ensures concurrent transactions don't see each other's uncommitted changes."),

        // Claude Certified Developer, Foundations (CCDV-F) practice questions —
        // written to match the published exam domain blueprint, not lifted from
        // the real exam (Anthropic doesn't publish real questions). One or two
        // per domain, roughly following the exam's own weighting.

        // Applications and Integration (33.1%)
        new("Claude Certified Developer",
            "In the Claude Messages API, where do persona/tone instructions that should apply to the whole conversation go?",
            new[] { "In the first user message", "In a top-level \"system\" parameter", "In a message with role \"system\"", "In the \"metadata\" field" },
            "In a top-level \"system\" parameter",
            "The Messages API takes system as its own top-level parameter, not a role:\"system\" message inside the messages array — that's different from some other chat APIs."),

        new("Claude Certified Developer",
            "Which response mode lets your app start rendering text before Claude finishes generating the full reply?",
            new[] { "Batch API", "Streaming (server-sent events)", "Prompt caching", "Extended thinking" },
            "Streaming (server-sent events)",
            "Streaming delivers the response as a series of events (like content_block_delta) so the client can render tokens as they arrive instead of waiting for completion."),

        new("Claude Certified Developer",
            "What's the main benefit of marking part of a prompt with cache_control?",
            new[] { "Higher max_tokens limit", "Lower cost and latency on repeated requests that reuse the same prefix", "Access to tool use", "Longer context window" },
            "Lower cost and latency on repeated requests that reuse the same prefix",
            "Prompt caching lets Claude skip reprocessing a repeated prefix (a long system prompt or document), cutting cost and time-to-first-token on cache hits."),

        // Model Selection and Optimization (16.8%)
        new("Claude Certified Developer",
            "For a simple, high-volume text classification task where speed and cost matter most, which model tier fits best?",
            new[] { "Opus", "Sonnet", "Haiku", "They're all priced and sized the same" },
            "Haiku",
            "Haiku models trade some reasoning depth for speed and lower cost — a good fit for simple, high-volume tasks."),

        // Agents and Workflows (14.7%)
        new("Claude Certified Developer",
            "What does the Claude Agent SDK add on top of the raw Messages API?",
            new[] { "A lower token price", "Agent-loop scaffolding: tool execution, session/context management, multi-turn orchestration", "A drag-and-drop prompt builder GUI", "Automatic model fine-tuning" },
            "Agent-loop scaffolding: tool execution, session/context management, multi-turn orchestration",
            "The Agent SDK wraps the Messages API with the plumbing an autonomous agent needs, so you're not hand-rolling tool-call handling and state tracking yourself."),

        // Tools and MCPs (10.6%)
        new("Claude Certified Developer",
            "When Claude decides to use a tool you've defined, what does the API actually return?",
            new[] { "A fully rendered final answer", "A tool_use content block naming the tool and its input, for your code to execute", "An error, since Claude can't call tools directly", "A redirect URL to run the tool" },
            "A tool_use content block naming the tool and its input, for your code to execute",
            "Claude never runs tools itself. It returns a tool_use block; your app executes it and sends a tool_result message back to continue the conversation."),

        new("Claude Certified Developer",
            "What does MCP (Model Context Protocol) standardize?",
            new[] { "How Claude models are trained", "A common way for apps to expose tools/data to any MCP-compatible AI client", "API pricing tiers", "How prompt caching is billed" },
            "A common way for apps to expose tools/data to any MCP-compatible AI client",
            "MCP is an open protocol — a tool or data source built once as an MCP server can be reused by any compatible client, instead of writing custom integration code per AI product."),

        // Security and Safety (8.1%)
        new("Claude Certified Developer",
            "What's the recommended way to keep an Anthropic API key out of a public git repository?",
            new[] { "Hardcode it in appsettings.json", "Store it via environment variables, a secrets manager, or user-secrets — never commit it", "Base64-encode it before committing", "Share it over email instead of committing it" },
            "Store it via environment variables, a secrets manager, or user-secrets — never commit it",
            "Encoding isn't encryption — base64 is trivially reversible. Keys belong in env vars, a secrets manager, or (for local dev) user-secrets, kept out of the repo entirely."),

        // Claude Code (3.1%)
        new("Claude Certified Developer",
            "What's the purpose of a CLAUDE.md file in a Claude Code project?",
            new[] { "Stores conversation history for replay", "Gives Claude Code persistent project context/conventions read at session start", "Required for Messages API authentication", "Configures which model version is billed" },
            "Gives Claude Code persistent project context/conventions read at session start",
            "CLAUDE.md documents project-specific conventions, commands, and context so Claude Code doesn't need to rediscover them every session."),

        // Eval, Testing, and Debugging (2.6%)
        new("Claude Certified Developer",
            "Why write an eval instead of just eyeballing a few chat responses when tuning a prompt?",
            new[] { "Evals are required for the API to accept requests", "A fixed, repeatable test set measures whether a change actually helped, instead of relying on impression", "Evals automatically fix bad prompts", "Evals lower your API bill" },
            "A fixed, repeatable test set measures whether a change actually helped, instead of relying on impression",
            "Without a consistent test set, it's easy to convince yourself a prompt tweak helped when you only checked the cases that happened to look good."),
    };

    private static readonly Random Rng = new();

    // model/difficulty/recentPrompts/recentWasMultipleChoice/preferredFormat/
    // maxTokens are unused here — a hardcoded bank has no concept of "which
    // model," "how hard," "steer away from these," "balance the format,"
    // "prefer one question type," or "how big an output budget." All six are
    // on the signature purely to satisfy IQuizProvider.
    public Task<QuizQuestion> GetQuestionAsync(string topic, string? model = null, string? difficulty = null, IReadOnlyList<string>? recentPrompts = null, IReadOnlyList<bool>? recentWasMultipleChoice = null, QuestionFormat preferredFormat = QuestionFormat.Any, int maxTokens = 700)
    {
        var pool = string.IsNullOrWhiteSpace(topic)
            ? Bank
            : Bank.Where(q => q.Topic == topic).ToList();

        if (pool.Count == 0) pool = Bank;

        return Task.FromResult(pool[Rng.Next(pool.Count)]);
    }

    public Task<GradeResult> GradeAsync(QuizQuestion question, string userAnswer, string? model = null)
    {
        var isCorrect = string.Equals(userAnswer, question.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new GradeResult(isCorrect, question.Explanation));
    }
}
