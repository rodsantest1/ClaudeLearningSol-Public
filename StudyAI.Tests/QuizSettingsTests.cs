using Xunit;
using StudyAI.Services;

namespace StudyAI.Tests;

/// <summary>
/// Unit tests for QuizSettings, the scoped DI service that holds global app
/// configuration (model, difficulty, question count, prefetch toggle).
/// Tests default values, property mutations, and the SettingsChanged event.
///
/// Model Selection and Optimization (16.8%), Prompt and Context Engineering (11.0%).
/// </summary>
public class QuizSettingsTests
{
    #region Default Values

    [Fact]
    public void DefaultModel_IsSonnet()
    {
        var settings = new QuizSettings();
        Assert.Equal("claude-sonnet-4-5", settings.Model);
    }

    [Fact]
    public void DefaultDifficultyFilter_IsMedium()
    {
        var settings = new QuizSettings();
        Assert.Equal("Medium", settings.DifficultyFilter);
    }

    [Fact]
    public void DefaultNumQuestions_IsTen()
    {
        var settings = new QuizSettings();
        Assert.Equal(10, settings.NumQuestions);
    }

    [Fact]
    public void DefaultPrefetchNextQuestion_IsTrue()
    {
        var settings = new QuizSettings();
        Assert.True(settings.PrefetchNextQuestion);
    }

    #endregion

    #region Property Mutations

    [Fact]
    public void Model_CanSwitchToHaiku()
    {
        var settings = new QuizSettings();
        settings.Model = "claude-haiku-4-5";
        Assert.Equal("claude-haiku-4-5", settings.Model);
    }

    [Fact]
    public void Model_CanSwitchBackToSonnet()
    {
        var settings = new QuizSettings();
        settings.Model = "claude-haiku-4-5";
        settings.Model = "claude-sonnet-4-5";
        Assert.Equal("claude-sonnet-4-5", settings.Model);
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Medium")]
    [InlineData("Hard")]
    [InlineData("Mixed")]
    public void DifficultyFilter_AcceptsAllValidValues(string difficulty)
    {
        var settings = new QuizSettings();
        settings.DifficultyFilter = difficulty;
        Assert.Equal(difficulty, settings.DifficultyFilter);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    public void NumQuestions_AcceptsValidRange(int num)
    {
        var settings = new QuizSettings();
        settings.NumQuestions = num;
        Assert.Equal(num, settings.NumQuestions);
    }

    [Fact]
    public void PrefetchNextQuestion_CanToggle()
    {
        var settings = new QuizSettings();
        Assert.True(settings.PrefetchNextQuestion);

        settings.PrefetchNextQuestion = false;
        Assert.False(settings.PrefetchNextQuestion);

        settings.PrefetchNextQuestion = true;
        Assert.True(settings.PrefetchNextQuestion);
    }

    #endregion

    #region SettingsChanged Event

    [Fact]
    public void SettingsChanged_FiresWhenNotifySettingsChanged_IsCalled()
    {
        var settings = new QuizSettings();
        var callCount = 0;

        settings.SettingsChanged += () => callCount++;

        settings.NotifySettingsChanged();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void SettingsChanged_FiresMultipleTimes()
    {
        var settings = new QuizSettings();
        var callCount = 0;

        settings.SettingsChanged += () => callCount++;

        settings.NotifySettingsChanged();
        settings.NotifySettingsChanged();
        settings.NotifySettingsChanged();

        Assert.Equal(3, callCount);
    }

    [Fact]
    public void SettingsChanged_AllSubscribersAreNotified()
    {
        var settings = new QuizSettings();
        var subscriber1Called = false;
        var subscriber2Called = false;
        var subscriber3Called = false;

        settings.SettingsChanged += () => subscriber1Called = true;
        settings.SettingsChanged += () => subscriber2Called = true;
        settings.SettingsChanged += () => subscriber3Called = true;

        settings.NotifySettingsChanged();

        Assert.True(subscriber1Called);
        Assert.True(subscriber2Called);
        Assert.True(subscriber3Called);
    }

    [Fact]
    public void SettingsChanged_SubscriberCanUnsubscribe()
    {
        var settings = new QuizSettings();
        var callCount = 0;
        Action handler = () => callCount++;

        settings.SettingsChanged += handler;
        settings.NotifySettingsChanged();
        Assert.Equal(1, callCount);

        settings.SettingsChanged -= handler;
        settings.NotifySettingsChanged();
        Assert.Equal(1, callCount); // Still 1, handler not called
    }

    [Fact]
    public void SettingsChanged_DoesNotFireWhenNoSubscribers()
    {
        var settings = new QuizSettings();

        // This should not throw even though SettingsChanged is null
        settings.NotifySettingsChanged();

        // If we get here without an exception, the test passes
        Assert.True(true);
    }

    #endregion

    #region Event Subscription Patterns

    [Fact]
    public void MultipleSubscribers_EachFiredExactlyOncePerNotify()
    {
        var settings = new QuizSettings();
        var calls1 = 0;
        var calls2 = 0;

        settings.SettingsChanged += () => calls1++;
        settings.SettingsChanged += () => calls2++;

        settings.NotifySettingsChanged();
        Assert.Equal(1, calls1);
        Assert.Equal(1, calls2);

        settings.NotifySettingsChanged();
        Assert.Equal(2, calls1);
        Assert.Equal(2, calls2);
    }

    [Fact]
    public void PartialUnsubscribe_RemainsSubscribersStillFire()
    {
        var settings = new QuizSettings();
        var calls1 = 0;
        var calls2 = 0;
        Action handler1 = () => calls1++;
        Action handler2 = () => calls2++;

        settings.SettingsChanged += handler1;
        settings.SettingsChanged += handler2;

        settings.NotifySettingsChanged();
        Assert.Equal(1, calls1);
        Assert.Equal(1, calls2);

        settings.SettingsChanged -= handler1;
        settings.NotifySettingsChanged();
        Assert.Equal(1, calls1); // handler1 not fired
        Assert.Equal(2, calls2); // handler2 still fired
    }

    [Fact]
    public void ResubscribeAfterUnsubscribe_WorksCorrectly()
    {
        var settings = new QuizSettings();
        var callCount = 0;
        Action handler = () => callCount++;

        settings.SettingsChanged += handler;
        settings.NotifySettingsChanged();
        Assert.Equal(1, callCount);

        settings.SettingsChanged -= handler;
        settings.NotifySettingsChanged();
        Assert.Equal(1, callCount);

        settings.SettingsChanged += handler;
        settings.NotifySettingsChanged();
        Assert.Equal(2, callCount);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void SettingsChange_ThenNotify_SimulatesRealComponentFlow()
    {
        // Simulates a component subscribing to settings changes
        var settings = new QuizSettings();
        var componentReceivedNotification = false;

        settings.SettingsChanged += () => componentReceivedNotification = true;

        // User changes a setting (e.g., via Settings panel)
        settings.Model = "claude-haiku-4-5";
        settings.DifficultyFilter = "Hard";
        settings.NumQuestions = 15;

        // Component still doesn't know until NotifySettingsChanged is called
        Assert.False(componentReceivedNotification);

        // Notify subscribers
        settings.NotifySettingsChanged();

        // Now component is notified
        Assert.True(componentReceivedNotification);
    }

    [Fact]
    public void MultiplePropertiesChanged_SingleNotify_NotifiesAll()
    {
        var settings = new QuizSettings();
        var notifyCount = 0;

        settings.SettingsChanged += () => notifyCount++;

        // Change multiple settings
        settings.Model = "claude-haiku-4-5";
        settings.DifficultyFilter = "Easy";
        settings.NumQuestions = 5;
        settings.PrefetchNextQuestion = false;

        // Only one notify call for all changes
        settings.NotifySettingsChanged();

        Assert.Equal(1, notifyCount);
    }

    #endregion

    #region Topic, Format, RecommendationMode Defaults

    [Fact]
    public void DefaultTopic_IsEmpty()
    {
        var settings = new QuizSettings();
        Assert.Equal("", settings.Topic);
    }

    [Fact]
    public void DefaultPreferredFormat_IsAny()
    {
        var settings = new QuizSettings();
        Assert.Equal("Any", settings.PreferredFormat);
    }

    [Fact]
    public void DefaultRecommendationMode_IsSuggested()
    {
        var settings = new QuizSettings();
        Assert.Equal("Suggested", settings.RecommendationMode);
    }

    [Fact]
    public void DefaultAvoidListSize_IsFive()
    {
        var settings = new QuizSettings();
        Assert.Equal(5, settings.AvoidListSize);
    }

    #endregion

    #region Topic, Format, RecommendationMode Mutations

    [Theory]
    [InlineData("")]
    [InlineData("C# Basics")]
    [InlineData("Web Fundamentals")]
    [InlineData("Databases")]
    [InlineData("Claude Certified Developer")]
    public void Topic_AcceptsAllValidValues(string topic)
    {
        var settings = new QuizSettings();
        settings.Topic = topic;
        Assert.Equal(topic, settings.Topic);
    }

    [Theory]
    [InlineData("Any")]
    [InlineData("MultipleChoice")]
    [InlineData("ShortAnswer")]
    public void PreferredFormat_AcceptsAllValidValues(string format)
    {
        var settings = new QuizSettings();
        settings.PreferredFormat = format;
        Assert.Equal(format, settings.PreferredFormat);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("Suggested")]
    [InlineData("Auto")]
    public void RecommendationMode_AcceptsAllValidValues(string mode)
    {
        var settings = new QuizSettings();
        settings.RecommendationMode = mode;
        Assert.Equal(mode, settings.RecommendationMode);
    }

    [Fact]
    public void Topic_CanChangeMultipleTimes()
    {
        var settings = new QuizSettings();
        settings.Topic = "C# Basics";
        Assert.Equal("C# Basics", settings.Topic);

        settings.Topic = "Databases";
        Assert.Equal("Databases", settings.Topic);

        settings.Topic = "";
        Assert.Equal("", settings.Topic);
    }

    [Fact]
    public void PreferredFormat_CanToggleBetweenValues()
    {
        var settings = new QuizSettings();
        Assert.Equal("Any", settings.PreferredFormat);

        settings.PreferredFormat = "MultipleChoice";
        Assert.Equal("MultipleChoice", settings.PreferredFormat);

        settings.PreferredFormat = "ShortAnswer";
        Assert.Equal("ShortAnswer", settings.PreferredFormat);

        settings.PreferredFormat = "Any";
        Assert.Equal("Any", settings.PreferredFormat);
    }

    [Fact]
    public void RecommendationMode_CanToggleBetweenValues()
    {
        var settings = new QuizSettings();
        Assert.Equal("Suggested", settings.RecommendationMode);

        settings.RecommendationMode = "Disabled";
        Assert.Equal("Disabled", settings.RecommendationMode);

        settings.RecommendationMode = "Auto";
        Assert.Equal("Auto", settings.RecommendationMode);

        settings.RecommendationMode = "Suggested";
        Assert.Equal("Suggested", settings.RecommendationMode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    public void AvoidListSize_AcceptsValidRange(int size)
    {
        var settings = new QuizSettings();
        settings.AvoidListSize = size;
        Assert.Equal(size, settings.AvoidListSize);
    }

    #endregion

    #region New Properties with Event Notifications

    [Fact]
    public void TopicChange_TriggersSettingsChanged()
    {
        var settings = new QuizSettings();
        var notified = false;

        settings.SettingsChanged += () => notified = true;

        settings.Topic = "C# Basics";
        settings.NotifySettingsChanged();

        Assert.True(notified);
    }

    [Fact]
    public void PreferredFormatChange_TriggersSettingsChanged()
    {
        var settings = new QuizSettings();
        var notified = false;

        settings.SettingsChanged += () => notified = true;

        settings.PreferredFormat = "MultipleChoice";
        settings.NotifySettingsChanged();

        Assert.True(notified);
    }

    [Fact]
    public void RecommendationModeChange_TriggersSettingsChanged()
    {
        var settings = new QuizSettings();
        var notified = false;

        settings.SettingsChanged += () => notified = true;

        settings.RecommendationMode = "Auto";
        settings.NotifySettingsChanged();

        Assert.True(notified);
    }

    [Fact]
    public void AllPropertiesChanged_SingleNotify_NotifiesAll()
    {
        var settings = new QuizSettings();
        var notifyCount = 0;

        settings.SettingsChanged += () => notifyCount++;

        // Change all properties including new ones
        settings.Model = "claude-haiku-4-5";
        settings.DifficultyFilter = "Hard";
        settings.NumQuestions = 20;
        settings.PrefetchNextQuestion = false;
        settings.Topic = "Databases";
        settings.PreferredFormat = "ShortAnswer";
        settings.RecommendationMode = "Disabled";
        settings.AvoidListSize = 10;

        // Single notify call covers all
        settings.NotifySettingsChanged();

        Assert.Equal(1, notifyCount);
    }

    [Fact]
    public void AvoidListSizeChange_TriggersSettingsChanged()
    {
        var settings = new QuizSettings();
        var notified = false;

        settings.SettingsChanged += () => notified = true;

        settings.AvoidListSize = 10;
        settings.NotifySettingsChanged();

        Assert.True(notified);
    }

    #endregion
}
