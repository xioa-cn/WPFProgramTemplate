using AIAgentFrameworkModule.Agent;

namespace AIAgentFrameworkModule.Models;

public enum AgentWorkflowMode { Parallel, Sequential }
public enum AgentStepStatus { Pending, Running, Completed, Failed, Cancelled, Skipped }
public sealed record AgentRoleDefinition(string Id, string Name, string Instructions, AgentModelOptions Model);
public sealed record AgentWorkflowDefinition(Guid RunId, string Task, AgentWorkflowMode Mode,
    AgentRoleDefinition Analyst, AgentRoleDefinition Reviewer, AgentRoleDefinition Coordinator, TimeSpan? Timeout = null)
{
    public bool GenerateFiles { get; init; }
    public bool EnableCommands { get; init; }
}
public sealed record AgentStepResult(string StepId, string RoleName, AgentStepStatus Status, string Output, string Error);
public sealed record AgentWorkflowUpdate(Guid RunId, AgentStepResult Step);
public sealed record AgentWorkflowRun(Guid RunId, AgentStepStatus Status, IReadOnlyList<AgentStepResult> Steps, string Message)
{
    public string FinalOutput => Status == AgentStepStatus.Completed ? Steps[^1].Output : "";
}
