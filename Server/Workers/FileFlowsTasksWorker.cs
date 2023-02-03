using System.Text;
using FileFlows.Plugin;
using FileFlows.ScriptExecution;
using FileFlows.Server.Controllers;
using FileFlows.Server.Helpers;
using FileFlows.ServerShared.Workers;
using FileFlows.Shared.Helpers;
using FileFlows.Shared.Models;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Logger = FileFlows.Shared.Logger;

namespace FileFlows.Server.Workers;

/// <summary>
/// A worker that runs FileFlows Tasks
/// </summary>
public class FileFlowsTasksWorker: Worker
{
    /// <summary>
    /// Gets the instance of the tasks worker
    /// </summary>
    internal static FileFlowsTasksWorker Instance { get;private set; }
    private readonly List<FileFlowsTask> Tasks = new ();
    private readonly Dictionary<string, object> Variables = new ();
    /// <summary>
    /// A list of tasks and the quarter they last ran in
    /// </summary>
    private Dictionary<Guid, int> TaskLastRun = new ();
    
    
    /// <summary>
    /// Creates a new instance of the Scheduled Task Worker
    /// </summary>
    public FileFlowsTasksWorker() : base(ScheduleType.Minute, 1)
    {
        Instance = this;
        ReloadTasks();
        ReloadVariables();
        
        SystemEvents.OnLibraryFileAdd += SystemEventsOnOnLibraryFileAdd;
        SystemEvents.OnLibraryFileProcessed += SystemEventsOnOnLibraryFileProcessed;
        SystemEvents.OnLibraryFileProcessedFailed += SystemEventsOnOnLibraryFileProcessedFailed;
        SystemEvents.OnLibraryFileProcessedSuceess += SystemEventsOnOnLibraryFileProcessedSuceess;
        SystemEvents.OnLibraryFileProcessingStarted += SystemEventsOnOnLibraryFileProcessingStarted;
        SystemEvents.OnServerUpdating += SystemEventsOnOnServerUpdating;
        SystemEvents.OnServerUpdateAvailable += SystemEventsOnOnServerUpdateAvailable;
    }

    /// <summary>
    /// Reloads the stored list of tasks
    /// </summary>
    public static void ReloadTasks()
    {
        var list = new TaskController().GetAll().Result?.ToList();
        Instance.Tasks.Clear();
        if(list?.Any() == true)
            Instance.Tasks.AddRange(list);
    }
    
    /// <summary>
    /// Reloads the stored list of variables
    /// </summary>
    public static void ReloadVariables()
    {
        var list = new VariableController().GetAll().Result?.ToList();
        Instance.Variables.Clear();
        foreach (var var in list ?? new ())
        {
            Instance.Variables.Add(var.Name, var.Value);
        }
        
        if(Instance.Variables.ContainsKey("FileFlows.Url") == false)
            Instance.Variables.Add("FileFlows.Url", ServerShared.Services.Service.ServiceBaseUrl);
    }

    /// <summary>
    /// Executes any tasks
    /// </summary>
    protected override void Execute()
    {
        if (LicenseHelper.IsLicensed() == false)
            return;
        
        int quarter = TimeHelper.GetCurrentQuarter();
        // 0, 1, 2, 3, 4
        foreach (var task in this.Tasks)
        {
            if (task.Type != TaskType.Schedule)
                continue;
            if (task.Schedule[quarter] != '1')
                continue;
            if (TaskLastRun.ContainsKey(task.Uid) && TaskLastRun[task.Uid] == quarter)
                continue;
            _ = RunTask(task);
            if (TaskLastRun.ContainsKey(task.Uid))
                TaskLastRun[task.Uid] = quarter;
            else
                TaskLastRun.Add(task.Uid, quarter);
        }
    }

    /// <summary>
    /// Runs a task by its UID
    /// </summary>
    /// <param name="uid">The UID of the task to run</param>
    /// <returns>the result of the executed task</returns>
    internal async Task<ScriptExecutor.RunScriptResult> RunByUid(Guid uid)
    {
        var task = Tasks.FirstOrDefault(x => x.Uid == uid);
        if (task == null)
            return new() { Success = false, Log = "Task not found" };
        return await RunTask(task);
    } 

    /// <summary>
    /// Runs a task
    /// </summary>
    /// <param name="task">the task to run</param>
    /// <param name="additionalVariables">any additional variables</param>
    private async Task<ScriptExecutor.RunScriptResult> RunTask(FileFlowsTask task, Dictionary<string, object> additionalVariables = null)
    {
        string code = await new ScriptController().GetCode(task.Script, type: ScriptType.System);
        if (string.IsNullOrWhiteSpace(code))
        {
            var msg = $"No code found for Task '{task.Name}' using script: {task.Script}";
            Logger.Instance.WLog(msg);
            return new() { Success = false, Log = msg };
        }
        Logger.Instance.ILog("Executing task: " + task.Name);
        DateTime dtStart = DateTime.Now;

        var variables = Variables.ToDictionary(x => x.Key, x => x.Value);
        if (additionalVariables?.Any() == true)
        {
            foreach (var variable in additionalVariables)
            {
                if (variables.ContainsKey(variable.Key))
                    variables[variable.Key] = variable.Value;
                else
                    variables.Add(variable.Key, variable.Value);
            }
        }

        var result = ScriptExecutor.Execute(code, variables);
        if(result.Success)
            Logger.Instance.ILog($"Task '{task.Name}' completed in: " + (DateTime.Now.Subtract(dtStart)) + "\n" + result.Log);
        else
            Logger.Instance.ELog($"Error executing task '{task.Name}: " + result.ReturnValue + "\n" + result.Log);
        new TaskController().UpdateLastRun(task.Uid);
        return result;
    }
    
    private void TriggerTaskType(TaskType type, Dictionary<string, object> variables)
    {
        var tasks = this.Tasks.Where(x => x.Type == type).ToArray();
        foreach (var task in tasks)
        {
            _ = RunTask(task, variables);
        }
    }

    private void UpdateEventTriggered(TaskType type, SystemEvents.UpdateEventArgs args)
    {
        TriggerTaskType(type, new Dictionary<string, object>
        {
            { nameof(args.Version), args.Version },
            { nameof(args.CurrentVersion), args.CurrentVersion },
        });
    }

    private void SystemEventsOnOnServerUpdateAvailable(SystemEvents.UpdateEventArgs args)
        => UpdateEventTriggered(TaskType.FileFlowsServerUpdateAvailable, args);
    private void SystemEventsOnOnServerUpdating(SystemEvents.UpdateEventArgs args)
        => UpdateEventTriggered(TaskType.FileFlowsServerUpdating, args);

    private void LibraryFileEventTriggered(TaskType type, SystemEvents.LibraryFileEventArgs args)
    {
        TriggerTaskType(type, new Dictionary<string, object>
        {
            { "FileName", args.File.Name },
            { "LibraryFile", args.File },
            { "Library", args.Library }
        });
    }

    private void SystemEventsOnOnLibraryFileAdd(SystemEvents.LibraryFileEventArgs args) =>
        LibraryFileEventTriggered(TaskType.FileAdded, args);
    private void SystemEventsOnOnLibraryFileProcessingStarted(SystemEvents.LibraryFileEventArgs args)
        => LibraryFileEventTriggered(TaskType.FileProcessing, args);
    private void SystemEventsOnOnLibraryFileProcessed(SystemEvents.LibraryFileEventArgs args)
        => LibraryFileEventTriggered(TaskType.FileProcessed, args);
    private void SystemEventsOnOnLibraryFileProcessedSuceess(SystemEvents.LibraryFileEventArgs args)
        => LibraryFileEventTriggered(TaskType.FileProcessSuccess, args);
    private void SystemEventsOnOnLibraryFileProcessedFailed(SystemEvents.LibraryFileEventArgs args)
        => LibraryFileEventTriggered(TaskType.FileProcessFailed, args);

}