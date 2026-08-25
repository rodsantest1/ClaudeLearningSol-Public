namespace StudyAI.Services;

/// <summary>
/// Pure add/rename/remove logic for QuizSettings.Topics — pulled out of Home.razor's
/// code-behind for the same reason ResolveModel/BuildAvoidClause were pulled out of
/// ClaudeQuizProvider: text (and a list) in, typed result out, no HttpClient or
/// Blazor component to mock. See StudyAI.Tests/QuizTopicsLogicTests.cs.
///
/// None of these methods mutate the list passed in — each returns a new list (or
/// null for a rejected operation), the same "here's the updated value, you decide
/// what to do with it" shape ClaudeQuizProvider's pure helpers use.
/// </summary>
public static class QuizTopicsLogic
{
    /// <summary>
    /// Adds a new topic. Returns null (nothing changed) if the trimmed name is empty,
    /// or if it already exists in the list (case-insensitive — "databases" shouldn't
    /// create a second entry alongside "Databases").
    /// </summary>
    public static List<string>? AddTopic(IReadOnlyList<string> topics, string? newTopic)
    {
        var trimmed = newTopic?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (topics.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase)))
            return null;

        var updated = topics.ToList();
        updated.Add(trimmed);
        return updated;
    }

    /// <summary>
    /// Removes a topic by exact name. A no-op (returns the list unchanged, as a new
    /// list instance) if the name isn't present — the caller (a delete button click
    /// on a topic that's already gone) shouldn't need to special-case that.
    /// </summary>
    public static List<string> RemoveTopic(IReadOnlyList<string> topics, string topicToRemove)
    {
        return topics.Where(t => !string.Equals(t, topicToRemove, StringComparison.Ordinal)).ToList();
    }

    /// <summary>
    /// Renames an existing topic. Returns null (nothing changed) if the trimmed new
    /// name is empty, if oldName isn't actually in the list, or if the new name
    /// collides (case-insensitive) with a DIFFERENT existing topic — renaming
    /// "Databases" to "databases" (same entry, different casing) is allowed, but
    /// renaming it to collide with an unrelated existing topic is not.
    /// </summary>
    public static List<string>? RenameTopic(IReadOnlyList<string> topics, string oldName, string? newName)
    {
        var trimmed = newName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var index = topics.ToList().FindIndex(t => string.Equals(t, oldName, StringComparison.Ordinal));
        if (index < 0)
            return null;

        var collidesWithAnother = topics
            .Where((t, i) => i != index)
            .Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase));
        if (collidesWithAnother)
            return null;

        var updated = topics.ToList();
        updated[index] = trimmed;
        return updated;
    }
}
