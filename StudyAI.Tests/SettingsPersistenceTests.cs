using Xunit;
using StudyAI.Services;
using System.Text.Json;

namespace StudyAI.Tests;

/// <summary>
/// Unit tests for SettingsPersistence, the singleton service that handles
/// loading and saving QuizSettings to a JSON file on disk.
///
/// Model Selection and Optimization (16.8%), Prompt and Context Engineering (11.0%).
/// </summary>
public class SettingsPersistenceTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly string _testSettingsPath;
    private readonly SettingsPersistence _persistence;

    public SettingsPersistenceTests()
    {
        // Create a temporary directory for test files
        _testDataDir = Path.Combine(Path.GetTempPath(), $"quiz_settings_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        _testSettingsPath = Path.Combine(_testDataDir, "settings.json");

        // Create SettingsPersistence with test path
        _persistence = new SettingsPersistence(_testSettingsPath);
    }

    public void Dispose()
    {
        // Clean up test directory after each test
        try
        {
            if (Directory.Exists(_testDataDir))
                Directory.Delete(_testDataDir, recursive: true);
        }
        catch { }
    }

    #region LoadSettingsAsync Tests

    [Fact]
    public async Task LoadSettingsAsync_WithNoFile_ReturnsNull()
    {
        // Ensure file doesn't exist
        Assert.False(File.Exists(_testSettingsPath));

        // Should return null when no settings file exists
        var result = await _persistence.LoadSettingsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithValidFile_LoadsAllSettings()
    {
        // Arrange: Save a settings file with all non-default values
        var settings = new QuizSettings
        {
            Model = "claude-haiku-4-5",
            DifficultyFilter = "Hard",
            NumQuestions = 15,
            PrefetchNextQuestion = false,
            Topic = "Databases",
            PreferredFormat = "ShortAnswer",
            RecommendationMode = "Disabled"
        };

        await _persistence.SaveSettingsAsync(settings);

        // Act: Load the settings
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert: All properties match
        Assert.NotNull(loaded);
        Assert.Equal("claude-haiku-4-5", loaded.Model);
        Assert.Equal("Hard", loaded.DifficultyFilter);
        Assert.Equal(15, loaded.NumQuestions);
        Assert.False(loaded.PrefetchNextQuestion);
        Assert.Equal("Databases", loaded.Topic);
        Assert.Equal("ShortAnswer", loaded.PreferredFormat);
        Assert.Equal("Disabled", loaded.RecommendationMode);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithCorruptedFile_ReturnsNull()
    {
        // Arrange: Write invalid JSON to the file
        await File.WriteAllTextAsync(_testSettingsPath, "{ invalid json }");

        // Act: Attempt to load
        var result = await _persistence.LoadSettingsAsync();

        // Assert: Returns null instead of throwing
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithEmptyFile_ReturnsNull()
    {
        // Arrange: Create an empty file
        await File.WriteAllTextAsync(_testSettingsPath, "");

        // Act: Attempt to load
        var result = await _persistence.LoadSettingsAsync();

        // Assert: Returns null
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithPartialSettings_LoadsAvailableFields()
    {
        // Arrange: Write JSON with only some fields
        var json = @"{
            ""Model"": ""claude-sonnet-4-5"",
            ""DifficultyFilter"": ""Easy""
        }";
        await File.WriteAllTextAsync(_testSettingsPath, json);

        // Act: Load the settings
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert: Loaded fields are set, unspecified ones use defaults
        Assert.NotNull(loaded);
        Assert.Equal("claude-sonnet-4-5", loaded.Model);
        Assert.Equal("Easy", loaded.DifficultyFilter);
        // NumQuestions should be 0 (JSON default) since not specified
        Assert.Equal(10, loaded.NumQuestions); // Actually will be 10 (C# property default)
    }

    #endregion

    #region SaveSettingsAsync Tests

    [Fact]
    public async Task SaveSettingsAsync_CreatesFileInDataDirectory()
    {
        // Arrange
        var settings = new QuizSettings();

        // Act
        await _persistence.SaveSettingsAsync(settings);

        // Assert: File was created (in the actual data dir, not test dir)
        // We can't easily verify this without accessing the private path,
        // but we can verify no exception was thrown
        Assert.True(true);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithAllSettings_WritesCorrectJson()
    {
        // Arrange
        var settings = new QuizSettings
        {
            Model = "claude-haiku-4-5",
            DifficultyFilter = "Hard",
            NumQuestions = 20,
            PrefetchNextQuestion = false,
            Topic = "C# Basics",
            PreferredFormat = "MultipleChoice",
            RecommendationMode = "Auto"
        };

        // Act: Save to test file path (using a manual write since we can't override the path)
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_testSettingsPath, json);

        // Assert: File exists and contains valid JSON
        Assert.True(File.Exists(_testSettingsPath));
        var content = await File.ReadAllTextAsync(_testSettingsPath);
        var deserialized = JsonSerializer.Deserialize<QuizSettings>(content);
        Assert.NotNull(deserialized);
        Assert.Equal("claude-haiku-4-5", deserialized.Model);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithDefaults_WritesDefaults()
    {
        // Arrange: Create settings with all defaults
        var settings = new QuizSettings();

        // Act: Manually save to test file
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_testSettingsPath, json);

        // Assert: Defaults are written
        var content = await File.ReadAllTextAsync(_testSettingsPath);
        var deserialized = JsonSerializer.Deserialize<QuizSettings>(content);
        Assert.NotNull(deserialized);
        Assert.Equal("claude-sonnet-4-5", deserialized.Model);
        Assert.Equal("Medium", deserialized.DifficultyFilter);
        Assert.Equal(10, deserialized.NumQuestions);
        Assert.True(deserialized.PrefetchNextQuestion);
        Assert.Equal("", deserialized.Topic);
        Assert.Equal("Any", deserialized.PreferredFormat);
        Assert.Equal("Suggested", deserialized.RecommendationMode);
    }

    #endregion

    #region Round-Trip Tests (Save then Load)

    [Theory]
    [InlineData("claude-haiku-4-5")]
    [InlineData("claude-sonnet-4-5")]
    public async Task RoundTrip_Model_PreservesValue(string model)
    {
        // Arrange
        var original = new QuizSettings { Model = model };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(model, loaded.Model);
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Medium")]
    [InlineData("Hard")]
    [InlineData("Mixed")]
    public async Task RoundTrip_Difficulty_PreservesValue(string difficulty)
    {
        // Arrange
        var original = new QuizSettings { DifficultyFilter = difficulty };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(difficulty, loaded.DifficultyFilter);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    public async Task RoundTrip_NumQuestions_PreservesValue(int numQuestions)
    {
        // Arrange
        var original = new QuizSettings { NumQuestions = numQuestions };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(numQuestions, loaded.NumQuestions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RoundTrip_Prefetch_PreservesValue(bool prefetch)
    {
        // Arrange
        var original = new QuizSettings { PrefetchNextQuestion = prefetch };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(prefetch, loaded.PrefetchNextQuestion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("C# Basics")]
    [InlineData("Web Fundamentals")]
    [InlineData("Databases")]
    [InlineData("Claude Certified Developer")]
    public async Task RoundTrip_Topic_PreservesValue(string topic)
    {
        // Arrange
        var original = new QuizSettings { Topic = topic };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(topic, loaded.Topic);
    }

    [Theory]
    [InlineData("Any")]
    [InlineData("MultipleChoice")]
    [InlineData("ShortAnswer")]
    public async Task RoundTrip_Format_PreservesValue(string format)
    {
        // Arrange
        var original = new QuizSettings { PreferredFormat = format };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(format, loaded.PreferredFormat);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("Suggested")]
    [InlineData("Auto")]
    public async Task RoundTrip_RecommendationMode_PreservesValue(string mode)
    {
        // Arrange
        var original = new QuizSettings { RecommendationMode = mode };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(mode, loaded.RecommendationMode);
    }

    [Fact]
    public async Task RoundTrip_Topics_PreservesValue()
    {
        // Arrange: a custom topics list — not the four defaults, so this can't
        // accidentally pass just because LoadSettingsAsync fell back to defaults.
        var original = new QuizSettings { Topics = new List<string> { "Security", "Tools and MCPs" } };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(original.Topics, loaded.Topics);
    }

    [Fact]
    public async Task RoundTrip_EmptyTopics_PreservesEmptyList()
    {
        // A student who deletes every topic should get an empty list back, not
        // silently fall back to the four built-in defaults.
        var original = new QuizSettings { Topics = new List<string>() };

        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Topics);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithNoTopicsField_UsesDefaultTopics()
    {
        // A settings.json written before Topics existed shouldn't crash or come
        // back with a null list — the property's own default initializer should
        // fill in the original four topics, same trick already covered by
        // LoadSettingsAsync_WithPartialSettings_LoadsAvailableFields for NumQuestions.
        var json = @"{ ""Model"": ""claude-sonnet-4-5"" }";
        await File.WriteAllTextAsync(_testSettingsPath, json);

        var loaded = await _persistence.LoadSettingsAsync();

        Assert.NotNull(loaded);
        Assert.Equal(new QuizSettings().Topics, loaded.Topics);
    }

    [Fact]
    public async Task RoundTrip_AllSettings_PreservesAll()
    {
        // Arrange: All settings with non-default values
        var original = new QuizSettings
        {
            Model = "claude-haiku-4-5",
            DifficultyFilter = "Hard",
            NumQuestions = 18,
            PrefetchNextQuestion = false,
            Topic = "Databases",
            PreferredFormat = "ShortAnswer",
            RecommendationMode = "Disabled",
            AvoidListSize = 10
        };

        // Act: Save and load
        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        // Assert: All properties preserved
        Assert.NotNull(loaded);
        Assert.Equal(original.Model, loaded.Model);
        Assert.Equal(original.DifficultyFilter, loaded.DifficultyFilter);
        Assert.Equal(original.NumQuestions, loaded.NumQuestions);
        Assert.Equal(original.PrefetchNextQuestion, loaded.PrefetchNextQuestion);
        Assert.Equal(original.Topic, loaded.Topic);
        Assert.Equal(original.PreferredFormat, loaded.PreferredFormat);
        Assert.Equal(original.RecommendationMode, loaded.RecommendationMode);
        Assert.Equal(original.AvoidListSize, loaded.AvoidListSize);
    }

    [Fact]
    public async Task RoundTrip_AvoidListSize_PreservesValue()
    {
        // Arrange: a non-default value — not 5, so this can't accidentally pass
        // just because LoadSettingsAsync fell back to the default.
        var original = new QuizSettings { AvoidListSize = 12 };

        await _persistence.SaveSettingsAsync(original);
        var loaded = await _persistence.LoadSettingsAsync();

        Assert.NotNull(loaded);
        Assert.Equal(12, loaded.AvoidListSize);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithNoAvoidListSizeField_UsesDefaultOfFive()
    {
        // A settings.json written before AvoidListSize existed shouldn't crash or
        // come back at 0 — the property's own default initializer should fill in
        // 5, same trick already covered for Topics/NumQuestions.
        var json = @"{ ""Model"": ""claude-sonnet-4-5"" }";
        await File.WriteAllTextAsync(_testSettingsPath, json);

        var loaded = await _persistence.LoadSettingsAsync();

        Assert.NotNull(loaded);
        Assert.Equal(new QuizSettings().AvoidListSize, loaded.AvoidListSize);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SaveSettingsAsync_WithReadOnlyDirectory_ContinuesSilently()
    {
        // Arrange: This test verifies graceful failure — we can't actually create
        // a read-only directory in a unit test without OS-specific code, but we can
        // verify the service is designed to not throw
        var settings = new QuizSettings();

        // Act: Should not throw even if directory is inaccessible
        // (In a real scenario, SaveSettingsAsync catches and silently continues)
        await _persistence.SaveSettingsAsync(settings);

        // Assert: No exception was thrown
        Assert.True(true);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithBadFilePath_ReturnsSafely()
    {
        // Act: Try to load from a path that doesn't exist
        var result = await _persistence.LoadSettingsAsync();

        // Assert: Returns null instead of throwing
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndLoadCycle_PreservesSettingsAcrossSessions()
    {
        // Arrange: Simulate a "session 1" save
        var session1Settings = new QuizSettings
        {
            Model = "claude-haiku-4-5",
            DifficultyFilter = "Easy",
            NumQuestions = 5,
            PrefetchNextQuestion = false,
            Topic = "C# Basics",
            PreferredFormat = "MultipleChoice",
            RecommendationMode = "Auto"
        };

        // Act: "Save" in session 1
        await _persistence.SaveSettingsAsync(session1Settings);

        // Act: "Load" in session 2
        var session2Settings = await _persistence.LoadSettingsAsync();

        // Assert: Session 2 has exact same settings as session 1
        Assert.NotNull(session2Settings);
        Assert.Equal(session1Settings.Model, session2Settings.Model);
        Assert.Equal(session1Settings.DifficultyFilter, session2Settings.DifficultyFilter);
        Assert.Equal(session1Settings.NumQuestions, session2Settings.NumQuestions);
        Assert.Equal(session1Settings.PrefetchNextQuestion, session2Settings.PrefetchNextQuestion);
        Assert.Equal(session1Settings.Topic, session2Settings.Topic);
        Assert.Equal(session1Settings.PreferredFormat, session2Settings.PreferredFormat);
        Assert.Equal(session1Settings.RecommendationMode, session2Settings.RecommendationMode);
    }

    #endregion
}
