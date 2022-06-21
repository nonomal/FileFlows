﻿using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using FileFlows.Node.Ui;
using FileFlows.Node.Utils;
using FileFlows.ServerShared;
using FileFlows.ServerShared.Helpers;
using FileFlows.ServerShared.Models;
using FileFlows.ServerShared.Services;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FileFlows.Node;
public class Program
{
    /// <summary>
    /// Gets an instance of a node manager
    /// </summary>
    internal static NodeManager? Manager { get; private set; }

    private static bool Exiting = false;
    private static Mutex appMutex;
    const string appName = "FileFlowsNode";
    public static void Main(string[] args)
    {
        args ??= new string[] { };
        #if(DEBUG)
        args = new[] { "--no-gui" };
        #endif
        if (args.Any(x => x.ToLower() == "--help" || x.ToLower() == "-?" || x.ToLower() == "/?" || x.ToLower() == "/help" || x.ToLower() == "-help"))
        {
            CommandLineOptions.PrintHelp();
            return;
        }
        Shared.Helpers.HttpHelper.Client = new HttpClient();

        var options = CommandLineOptions.Parse(args);
        if (Globals.IsLinux && options.InstallService)
        {
            Utils.SystemdService.Install(options.DotNet);
            return;
        }
        Globals.IsDocker = options.Docker;
        ServerShared.Globals.IsDocker = options.Docker;
        
        DirectoryHelper.Init(options.Docker, true);
        
        appMutex = new Mutex(true, appName, out bool createdNew);
        if (createdNew == false)
        {
            // app is already running
            if (options.NoGui)
            {
                Console.WriteLine("An instance of FileFlows Node is already running");
            }
            else
            {
                try
                {
                    var appBuilder = BuildAvaloniaApp(true);
                    appBuilder.StartWithClassicDesktopLifetime(args);
                }
                catch (Exception) { }
            }
            
            return;
        }
        
        try
        {
            LoadEnvironmentalVaraibles();
            
            Service.ServiceBaseUrl = AppSettings.Load().ServerUrl;
            #if(DEBUG)
            if (string.IsNullOrEmpty(Service.ServiceBaseUrl))
                Service.ServiceBaseUrl = "http://localhost:6868/";
            #endif


            if (string.IsNullOrEmpty(options.Server) == false)
                AppSettings.ForcedServerUrl = options.Server;
            if (string.IsNullOrEmpty(options.Temp) == false)
                AppSettings.ForcedTempPath = options.Temp;
            if (string.IsNullOrEmpty(options.Name) == false)
                AppSettings.ForcedHostName = options.Name;


            new ConsoleLogger();
            new FileLogger(DirectoryHelper.LoggingDirectory, "FileFlows-Node");
            new ServerLogger();
            
            Logger.Instance?.ILog("FileFlows Node version: " + Globals.Version);

            AppSettings.Init();


            bool showUi = options.Docker == false && options.NoGui == false;

            Manager = new ();
            
            if(File.Exists(Path.Combine(DirectoryHelper.BaseDirectory, "node-upgrade.bat")))
                File.Delete(Path.Combine(DirectoryHelper.BaseDirectory, "node-upgrade.bat"));
            if(File.Exists(Path.Combine(DirectoryHelper.BaseDirectory, "node-upgrade.sh")))
                File.Delete(Path.Combine(DirectoryHelper.BaseDirectory, "node-upgrade.sh"));
            
            #if(DEBUG)
            showUi = true;
            #endif

            if (showUi)
            {
                if(Globals.IsWindows)
                    Utils.WindowsConsoleManager.Hide();
                
                Logger.Instance?.ILog("Launching GUI");
                Task.Run(async () =>
                {
                    await Manager.Register();
                    Manager.Start();
                });
                try
                {
                    var appBuilder = BuildAvaloniaApp();
                    appBuilder.StartWithClassicDesktopLifetime(args);
                }
                catch (Exception) { }

            }
            else
            {
                if (AppSettings.IsConfigured() == false)
                {
                    Shared.Logger.Instance?.ELog("Configuration not set");
                    return;
                }

                Shared.Logger.Instance?.ILog("Registering FileFlow Node");

                if (Manager.Register().Result == false)
                    return;

                Shared.Logger.Instance?.ILog("FileFlows node starting");
                
                Manager.Start();

                Thread.Sleep(-1);

                Shared.Logger.Instance?.ILog("Stopping workers");

                Manager.Stop();

                Shared.Logger.Instance?.ILog("Exiting");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message + Environment.NewLine + ex.StackTrace);
        }
    }

    private static void LoadEnvironmentalVaraibles()
    {
        AppSettings.ForcedServerUrl = Environment.GetEnvironmentVariable("ServerUrl");
        AppSettings.ForcedTempPath = Environment.GetEnvironmentVariable("TempPath");
        AppSettings.ForcedHostName = Environment.GetEnvironmentVariable("NodeName");

        string mappings = Environment.GetEnvironmentVariable("NodeMappings");
        if (string.IsNullOrWhiteSpace(mappings) == false)
        {
            try
            {
                var mappingsArray = JsonSerializer.Deserialize<List<RegisterModelMapping>>(mappings);
                if (mappingsArray?.Any() == true)
                    AppSettings.EnvironmentalMappings = mappingsArray;
            }
            catch (Exception)
            {
            }
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("NodeRunnerCount") ?? string.Empty, out int runnerCount))
        {
            AppSettings.EnvironmentalRunnerCount = runnerCount;
        }
        if (bool.TryParse(Environment.GetEnvironmentVariable("NodeEnabled") ?? string.Empty, out bool enabled))
        {
            AppSettings.EnvironmentalEnabled = enabled;
        }
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(bool messagebox = false)
        => (messagebox ? AppBuilder.Configure<MessageApp>() : AppBuilder.Configure<App>())
            .UsePlatformDetect();

    /// <summary>
    /// Quits the node application
    /// </summary>
    internal static void Quit()
    {
        Exiting = true;
        MainWindow.Instance?.ForceQuit();
        Environment.Exit(0);
    }
    
}
