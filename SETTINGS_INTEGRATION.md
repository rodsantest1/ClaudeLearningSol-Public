# Settings Panel Integration

## What's been added

A new **Settings panel** on the home screen (`Home.razor`) with a **Prefetch toggle** that lets students experiment with the latency/cost tradeoff of question prefetching.

### Files created/modified:

1. **`Services/AppSettings.cs`** (NEW)
   - Global app settings service with `PrefetchNextQuestion` boolean
   - Includes a `SettingsChanged` event for reactive updates
   - Documented as a teaching tool so students can experiment with optimizations

2. **`Components/Pages/Home.razor`** (MODIFIED)
   - Added a gear icon (⚙️) button in the hero section to open Settings
   - Settings panel modal with prefetch checkbox
   - Overlay when panel is open
   - Responsive styling for mobile

3. **`Components/Pages/TimedQuiz.razor`** (MODIFIED)
   - Injected `AppSettings` 
   - Made the prefetch logic conditional: `Settings.PrefetchNextQuestion`
   - Updated comment to explain the teaching purpose

## Next steps: Register AppSettings in DI

To make this work, you need to register `AppSettings` as a scoped service in `Program.cs`:

```csharp
builder.Services.AddScoped<AppSettings>();
```

This should go in the services setup section alongside the existing `IQuizProvider` and `IQuizPlanner` registrations.

## How it works

1. Student lands on home screen
2. Clicks the ⚙️ Settings button to open the Settings panel
3. Toggles "Prefetch next question" on/off
4. The setting applies to all quiz modes (Regular, Timed/Configure, Timed/QuickStart)
5. When disabled, questions load synchronously after submit (slower, fewer API calls)
6. When enabled (default), questions load in the background (faster, more API calls)

## Teaching value

Students can:
- Toggle prefetch and immediately feel the latency difference
- See how much API calls increase/decrease in the session stats
- Understand the real tradeoff between responsiveness and cost
- Experiment with optimization strategies (Model Selection and Optimization, 16.8%)

## Future extensibility

The `AppSettings` class is designed to grow — you can add more toggles here:
- Background grading on/off
- Difficulty distribution preferences
- Retry limits
- etc.

Each one becomes a hands-on learning opportunity.
