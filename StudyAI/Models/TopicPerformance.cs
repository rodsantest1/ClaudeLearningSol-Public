namespace StudyAI.Models;

// Per-topic running score, computed in Quiz.razor from graded answers so far
// this session. Fed to IQuizPlanner as the "input" side of the routing
// decision — Agents and Workflows (CCDV-F, 14.7%): this is the state a
// workflow step reads before deciding what happens next.
public record TopicPerformance(string Topic, int Correct, int Total);
