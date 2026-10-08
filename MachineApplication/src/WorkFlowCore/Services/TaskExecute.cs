namespace WorkFlowCore.Services;

public class TaskExecute
{
    private readonly string _workflowDir;

    public TaskExecute(string workflowBaseDir)
    {
        _workflowDir = workflowBaseDir;
    }

    public void Execute()
    {
    }
}