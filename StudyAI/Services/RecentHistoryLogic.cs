namespace StudyAI.Services;

// Prompt and Context Engineering (CCDV-F, 11.0%): pure logic behind Quiz.razor's
// per-topic avoid-list (RecentPromptsByTopic) and format history
// (RecentFormatsByTopic) — pulled out for the same reason QuizTopicsLogic was:
// directly testable without touching Blazor, IJSRuntime, or HttpClient.
//
// This is also where the actual bug lived that made questions get longer and
// longer once a second topic entered the mix (see CLAUDE.md): a single flat
// list used to be shared across every topic, so playing "Claude Certified
// Architect" fed prompts into the SAME avoid-list Claude saw for "Claude
// Certified Developer" — and per ClaudeQuizProvider.BuildAvoidClause's own
// comment, a longer, less-relevant avoid-list measurably pushes Claude toward
// longer, more-elaborate questions to differentiate against it. AddToTopic
// below is what guarantees adding to one topic's history can never touch
// another topic's — the property the old single-list design didn't have, and
// the property RecentHistoryLogicTests.cs exists to lock in against regressing.
public static class RecentHistoryLogic
{
    // → Non-mutating (same convention as QuizTopicsLogic): returns a new
    // dictionary rather than modifying byTopic or any list inside it, so a
    // caller holding a reference to the old state never sees it change out
    // from under it. Every OTHER topic's list in the result is untouched —
    // only `topic`'s list is new — which is the actual guarantee this class
    // exists to provide.
    public static Dictionary<string, List<T>> AddToTopic<T>(
        IReadOnlyDictionary<string, List<T>> byTopic,
        string topic,
        T item,
        int maxItems)
    {
        var result = new Dictionary<string, List<T>>(byTopic);

        var existing = result.TryGetValue(topic, out var list) ? list : new List<T>();
        var updated = new List<T>(existing) { item };
        while (updated.Count > maxItems)
            updated.RemoveAt(0);

        result[topic] = updated;
        return result;
    }

    // → What Quiz.razor actually replays into GetQuestionAsync for whichever
    // topic is currently selected — every OTHER topic's history is irrelevant
    // to that request and must never leak in, which is the whole point of
    // this class. Returns an empty list (never null) for a topic with no
    // history yet, matching GetQuestionAsync's recentPrompts contract (an
    // empty/null list is treated the same as "nothing to avoid" by
    // BuildAvoidClause).
    public static IReadOnlyList<T> ForTopic<T>(IReadOnlyDictionary<string, List<T>> byTopic, string topic) =>
        byTopic.TryGetValue(topic, out var list) ? list : new List<T>();

    // → Backs Home.razor's "Clear history" action: a topic's avoid-list
    // naturally fills toward AddToTopic's maxItems cap with regular use
    // (even just loading Quiz.razor counts — a question is generated and
    // shown immediately on page load, whether or not it's ever answered),
    // and once full it keeps every future question for that topic on the
    // longer/more-elaborate end (see AddToTopic's header comment) until
    // it's reset. Removing the key entirely rather than setting an empty
    // list is equivalent from ForTopic's perspective (both read back as
    // empty) but keeps the persisted JSON from accumulating empty entries
    // for topics nobody's touched. A topic with no existing entry is a
    // no-op, not an error — same "already in the desired state" shape as
    // QuizTopicsLogic.RemoveTopic.
    public static Dictionary<string, List<T>> RemoveTopic<T>(IReadOnlyDictionary<string, List<T>> byTopic, string topic)
    {
        var result = new Dictionary<string, List<T>>(byTopic);
        result.Remove(topic);
        return result;
    }
}
