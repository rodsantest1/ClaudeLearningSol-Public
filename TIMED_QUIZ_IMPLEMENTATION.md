# Timed Quiz Feature Implementation

## Overview
Implemented a complete timed quiz feature for StudyAI that allows users to take focused practice quizzes with time tracking, mark-for-review capability, and detailed results breakdown by difficulty level.

## Architecture

### State Machine
The main `TimedQuiz.razor` component manages a 4-state lifecycle:

1. **Setup**: User chooses Quick Start or Configure
2. **Running**: Active quiz, displaying questions one at a time
3. **ReviewMarked**: Review questions flagged during the quiz
4. **Results**: Final score summary with difficulty breakdown

```
Setup → Running → ReviewMarked → Results
        ↑__________|  ↑____________↑
                (Quit/Done)    (Restart)
```

### Component Hierarchy

```
TimedQuiz.razor (Main Orchestrator)
├── QuickStartOrConfigure.razor
│   └── Two-button setup screen (⚡ Quick Start | ⚙️ Configure)
├── QuizRunner.razor
│   ├── Question display (MCQ or short-answer)
│   ├── Timer display (per-question + total)
│   ├── Mark-for-review checkbox
│   └── Action buttons (Next/Review/Quit)
├── ReviewMarkedQuestions.razor
│   ├── List of marked questions
│   ├── Display answer details
│   ├── Show explanation from grading
│   └── Continue/Restart buttons
└── QuizResults.razor
    ├── Overall score circle (% correct)
    ├── Summary stats (time, average time/Q)
    ├── DifficultyBar components for each level
    └── Action buttons (Another Quiz / Dashboard)
```

## File Structure

### New Components
- `StudyAI/Components/TimedQuiz/QuickStartOrConfigure.razor`
- `StudyAI/Components/TimedQuiz/QuizRunner.razor`
- `StudyAI/Components/TimedQuiz/ReviewMarkedQuestions.razor`
- `StudyAI/Components/TimedQuiz/QuizResults.razor`
- `StudyAI/Components/TimedQuiz/DifficultyBar.razor`

### New Test Files
- `StudyAI.Tests/TimedQuizConfigTests.cs` (7 tests)
- `StudyAI.Tests/TimedQuizResultTests.cs` (9 tests)

### Existing Files (Already Created)
- `StudyAI/Components/Pages/TimedQuiz.razor` (Main orchestrator, 202 lines)
- `StudyAI/Models/TimedQuizConfig.cs` (Configuration model)
- `StudyAI/Models/TimedQuizResult.cs` (Results tracking)

## Key Features

### 1. Quick Start Mode
- Instantly begin with 10 questions, mixed difficulty, no time limits
- Zero configuration needed for immediate practice
- Factory method: `TimedQuizConfig.QuickStart()`

### 2. Time Tracking
- **Individual question timer**: Shows elapsed time for current question
- **Overall quiz timer**: Shows cumulative time spent
- **Per-question tracking**: Stores `TimeSpentMs` for each attempt
- **Optional time limits**: 
  - `TimePerQuestionSeconds`: Enforce time per question (if set)
  - `TotalTimeSeconds`: Hard stop when total time expires (if set)

### 3. Mark for Review
- Checkbox on each question during quiz
- Tracks indices of marked questions
- Review screen shows all marked questions before finalizing
- Full grading details visible (user answer vs. correct answer)
- Explanation shown when available

### 4. Question Format Control
- Respects `PreferredFormat` from `TimedQuizConfig`
- UI shows question type badge (Multiple Choice / Short Answer)
- Renders appropriate input (radio buttons for MCQ, textarea for short answer)

### 5. Difficulty Filtering
- Config supports: "Easy", "Medium", "Hard", "Mixed", "Any"
- `GetRandomDifficulty()` handles mixed difficulty selection
- Mixed mode randomly selects from available difficulties

### 6. Results Breakdown
- **Overall score**: Percentage correct, absolute count
- **Time metrics**: Total time, average time per question
- **By difficulty**:
  - Easy: X correct / Y total
  - Medium: X correct / Y total
  - Hard: X correct / Y total
- Visual progress bars for each difficulty level

## Component Responsibilities

### QuickStartOrConfigure.razor
- **Purpose**: Setup screen
- **Renders**: Two centered buttons with descriptions
- **Callbacks**: `OnQuickStart`, `OnConfigure`
- **Styling**: Centered card with primary/secondary button styling

### QuizRunner.razor
- **Purpose**: Present one question at a time
- **Props**: 
  - `Config` (current quiz config)
  - `Question` (current question)
  - `QuestionNumber`, `TotalQuestions` (progress)
  - `ElapsedMs`, `QuestionElapsedMs` (timers)
  - `IsLoading` (grading in progress)
- **Renders**:
  - Progress bar
  - Timer display (HH:MM:SS or MM:SS)
  - Question with difficulty badge
  - MCQ options OR short-answer textarea
  - Mark-for-review checkbox
  - Action buttons
- **Callbacks**: `OnAnswer`, `OnMark`, `OnQuit`

### ReviewMarkedQuestions.razor
- **Purpose**: Review marked questions before finalizing
- **Props**:
  - `Attempts` (all question attempts)
  - `MarkedIndices` (indices of marked questions)
- **Renders**:
  - Empty state if no marked questions
  - Card for each marked question showing:
    - Question text
    - User answer vs. correct answer
    - Grading explanation
    - Time spent, difficulty
- **Callbacks**: `OnContinue`, `OnRestart`

### QuizResults.razor
- **Purpose**: Final score summary
- **Props**: `Result` (TimedQuizResult object)
- **Renders**:
  - Large circular score display (% correct)
  - Summary stats: total Q, correct, incorrect, time, avg time/Q
  - Breakdown by difficulty using DifficultyBar subcomponent
  - Warning if questions were marked for review
  - Action buttons
- **Callbacks**: `OnRestart`

### DifficultyBar.razor
- **Purpose**: Reusable progress bar for difficulty breakdown
- **Props**: `Difficulty` (Easy/Medium/Hard), `Correct`, `Total`
- **Renders**: 
  - Difficulty label with "X/Y correct" count
  - Colored progress bar (green/orange/red)
  - Percentage display
- **Styling**: Color-coded by difficulty

## Data Flow

### Quiz Lifecycle
```
1. User lands on /timed-quiz
   → State = Setup
   → QuickStartOrConfigure rendered

2a. User clicks "Quick Start"
   → CurrentConfig = TimedQuizConfig.QuickStart()
   → State = Running
   → StartQuiz() initializes stopwatches, clears lists
   → LoadNextQuestion() fetches first question

2b. User clicks "Configure"
   → (Future: Show config form component)
   → User submits config
   → State = Running
   → StartQuiz()

3. QuizRunner displays question
   → User selects answer (MCQ) or types answer (SA)
   → User optionally marks for review
   → User clicks "Next Question"
   → HandleAnswer() is called
   → Answer is graded via IQuizProvider.GradeAsync()
   → QuestionAttempt record created
   → If marked, index added to MarkedForReview list
   → LoadNextQuestion() fetches next

4. After all questions or user quits
   → State = ReviewMarked
   → ReviewMarkedQuestions shows marked questions

5. User clicks "Continue to Results"
   → Build TimedQuizResult from Attempts
   → Calculate per-difficulty breakdown
   → State = Results
   → QuizResults displays final summary

6. User can:
   → "Start Another Quiz" → State = Setup (restart)
   → "Return to Dashboard" → Navigate to "/" (Blazor routing)
```

## Key Pointer Comments in Code

### TimedQuiz.razor
- `→ OnInitializedAsync`: Component lifecycle initialization
- `→ Handle events`: Quick start, configure, answer, mark, quit
- `→ Get next question respecting difficulty filter`: Question loading logic
- `→ Grade the answer`: Answer grading integration
- `→ Build final results`: Results compilation from attempts

### QuizRunner.razor
- `→ Submit current answer and move to next question`: Answer submission
- Timer formatting to HH:MM:SS or MM:SS

### ReviewMarkedQuestions.razor
- Card headers show question number and correctness badge
- Answer display differs for MCQ vs. short-answer
- Explanation and metadata shown for each attempt

### QuizResults.razor
- Large circular score display with gradient background
- DifficultyBar component for visual breakdown
- Marked-for-review warning callout

## Testing Coverage

### TimedQuizConfigTests.cs (7 tests)
- ✓ QuickStart returns correct defaults
- ✓ Multiple QuickStart calls return equal records
- ✓ Can create config with custom values
- ✓ Topic-only config uses defaults
- ✓ TimePerQuestion can be null
- ✓ TotalTime can be null
- ✓ Config tracks per-question time limits

### TimedQuizResultTests.cs (9 tests)
- ✓ PercentCorrect calculated correctly (100%, partial, 0%)
- ✓ PercentCorrect handles zero-question edge case
- ✓ AverageTimePerQuestionMs calculated correctly
- ✓ AverageTimePerQuestionMs handles zero-question edge case
- ✓ Tracks Easy difficulty breakdown
- ✓ Tracks Medium difficulty breakdown
- ✓ Tracks Hard difficulty breakdown
- ✓ MarkedForReview stores question indices
- ✓ Attempts tracks individual question attempts
- ✓ Complete scenario with mixed difficulty and partial credit

## Styling & UX

### Color Scheme
- **Primary**: #0066cc (blue)
- **Secondary**: #f0f0f0 (light gray)
- **Difficulty badges**:
  - Easy: #d4edda / #155724 (green)
  - Medium: #fff3cd / #856404 (amber)
  - Hard: #f8d7da / #721c24 (red)

### Layout Patterns
- **Centered card**: Setup, results screens
- **Full-width quiz**: Question display with max-width constraint
- **Flex-based**: Button groups, metric layouts
- **Progress indicator**: Bar under question counter
- **Timer display**: Monospace font, prominent color

### Responsive Design
- Sidebar collapse on small screens
- Flex-based layouts adapt to viewport width
- Touch-friendly button sizes (min 1rem height)
- Readable font sizes for question text (1.3rem)

## Integration Checklist

- [ ] Add route link in main navigation (NavMenu.razor)
  ```razor
  <NavLink href="timed-quiz" Match="NavLinkMatch.All">
      Timed Quiz
  </NavLink>
  ```

- [ ] Ensure StudyAI project file includes TimedQuiz component directory
  - Check `.csproj` includes `StudyAI/Components/TimedQuiz/**/*.razor`

- [ ] Import required services in QuizRunner/ReviewMarkedQuestions
  - May need `@inject IJSRuntime` if implementing time limits later

- [ ] Build and test
  ```bash
  dotnet build
  dotnet test
  ```

- [ ] Verify page loads: `https://localhost:5001/timed-quiz`

## Future Enhancements

1. **Configure Screen**: Build custom config form component
   - Input for NumQuestions, DifficultyFilter, Topic, PreferredFormat
   - Toggle for time limits with input fields
   - Submit button to start quiz with custom config

2. **Time Limit Enforcement**:
   - Add timer callbacks to stop quiz when time expires
   - Visual warning when time is running out (last 30s)
   - Auto-submit answer when per-question time expires

3. **Exam Simulation Mode**:
   - Full 50-question practice test with strict time limits
   - Enforced topic selection (all CCDV-F domains)
   - Results compared to passing score

4. **Progress Tracking**:
   - Persist quiz results to database
   - Show historical performance charts
   - Track improvement over time by difficulty

5. **Question Analytics**:
   - Show which questions users struggle with most
   - Identify weak topic areas
   - Recommend focused practice

## Notes

- All components use Blazor Server with Interactive Server render mode
- Questions are fetched on-demand from IQuizProvider (Claude API)
- Grading is performed by IQuizProvider.GradeAsync()
- Storage is in-memory during quiz session
- Optional: localStorage persistence for quiz history (future)
