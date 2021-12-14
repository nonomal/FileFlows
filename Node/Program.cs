﻿// See https://aka.ms/new-console-template for more information
using FileFlows.Node.Workers;
using FileFlows.Server.Workers;
using FileFlows.ServerShared.Services;
using FileFlows.Shared.Helpers;

string nodeAddress = Environment.MachineName;
Console.WriteLine("FileFlows Processing node: " + nodeAddress);

FileFlows.Shared.Logger.Instance = FileFlows.ServerShared.Logger.Instance;

HttpHelper.Client = new HttpClient();


WorkerManager.StartWorkers(new FlowWorker());

Console.WriteLine("Press any key to exit");

Console.Read();
WorkerManager.StopWorkers();

Console.WriteLine("Exiting");