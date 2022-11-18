using System.Text.RegularExpressions;
using Esprima.Ast;
using FileFlows.Plugin;
using FileFlows.Server.Controllers;
using FileFlows.Server.Helpers;
using FileFlows.ServerShared.Services;
using FileFlows.ServerShared.Workers;
using FileFlows.Shared.Models;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace FileFlows.Server.Workers;

/// <summary>
/// Worker to monitor libraries
/// </summary>
public class LibraryWorker : Worker
{
    private static LibraryWorker Instance;
    
    private Dictionary<string, WatchedLibrary> WatchedLibraries = new ();

    /// <summary>
    /// Creates a new instance of the library worker
    /// </summary>
    public LibraryWorker() : base(ScheduleType.Second, 10)
    {
        Trigger();
        Instance = this;
    }

    public override void Start()
    {
        base.Start();
    }

    public override void Stop()
    {
        base.Stop();
    }

    /// <summary>
    /// Triggers a scan now
    /// </summary>
    public static void ScanNow()
    {
        Instance?.Trigger();
    }

    private void Watch(params Library[] libraries)
    {
        foreach(var lib in libraries)
        {
            WatchedLibraries.Add(lib.Uid + ":" + lib.Path, new WatchedLibrary(lib));
        }
    }

    private void Unwatch(params string[] keys)
    {
        foreach(string key in keys)
        {
            if (WatchedLibraries.ContainsKey(key) == false)
                continue;
            var watcher = WatchedLibraries[key];
            watcher.Dispose();
            WatchedLibraries.Remove(key);
            watcher = null;
        }
    }

    protected override void Execute()
    {
        var libController = new LibraryController();
        var libraries = libController.GetAll().Result.ToArray();
        bool scannedLibraries = libraries.Any(x => x.Scan);
        if (scannedLibraries)
        {
            this.Interval = 30;
            this.Schedule = ScheduleType.Second;
        }
        else
        {
            this.Interval = 5;
            this.Schedule = ScheduleType.Minute;
        }
        var libraryUids = libraries.Select(x => x.Uid + ":" + x.Path).ToList();            


        Watch(libraries.Where(x => WatchedLibraries.ContainsKey(x.Uid + ":" + x.Path) == false).ToArray());
        Unwatch(WatchedLibraries.Keys.Where(x => libraryUids.Contains(x) == false).ToArray());

        foreach(var libwatcher in WatchedLibraries.Values)
        {
            var library = libraries.FirstOrDefault(x => libwatcher.Library.Uid == x.Uid);
            if (library == null)
                continue;
            libwatcher.UpdateLibrary(library);
            if (library.Enabled == false)
                continue;
            if (libwatcher.ScanComplete)
            {
                // hasn't been scanned yet, we scan when the app starts or library is first added
            }
            else if (library.Scan == false)
            {
                if (library.FullScanDisabled)
                    continue;
                
                // need to check full scan interval
                if (library.LastScannedAgo.TotalMinutes < library.FullScanIntervalMinutes)
                    continue;
            }
            else if (library.LastScannedAgo.TotalSeconds < library.ScanInterval)
                continue;
            
            libwatcher.Scan();
        }
    }

    /// <summary>
    /// Resets processing of all library files that are currently marked as Processing to Unprocessed
    /// </summary>
    /// <param name="internalOnly">If only the internal processing node should be reset</param>
    internal static void ResetProcessing(bool internalOnly = true)
    {
        var service = new Server.Services.LibraryFileService();
        if (internalOnly)
        {
            service.ResetProcessingStatus(Globals.InternalNodeUid).Wait();
        }
        else
        {
            // special case can use dbhelper directly
            // this is called at the start up of FileFlows
            service.ResetProcessingStatus().Wait();
        }
    }
}
