using OrcaPod;
using System.Threading;

Console.WriteLine("Hello, World!");

var wd = new OrcaPod.Utils.Watchdog();

wd.AddFile("../README.md");
wd.FileChanged += (s, e) =>
{
    Console.WriteLine($"File changed: {e.FilePath}");
};

wd.Start();

// block until user hits Ctrl+C
var stop = new ManualResetEventSlim(false);
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true; // prevent immediate process termination
    Console.WriteLine("Stopping...");
    stop.Set();
};

Console.WriteLine("Watching files. Press Ctrl+C to exit.");
stop.Wait();