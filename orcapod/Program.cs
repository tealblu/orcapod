using OrcaPod.Service;
using OrcaPod;
using System.Threading;

namespace OrcaPod
{
    class Program
    {
        public static void Main(string[] args)
        {
            var service = new MainService();

            if (Environment.UserInteractive)
            {
                service.Start();
                Console.WriteLine($"{service.ServiceName} running interactively. Press Enter to stop.");
                Console.ReadLine();
                service.Stop();
                return;
            }

            // Try to discover a platform-specific host (implemented elsewhere).
            var hostType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => typeof(IPlatformServiceHost).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (hostType != null)
            {
                var host = Activator.CreateInstance(hostType) as IPlatformServiceHost;
                if (host != null)
                {
                    host.RunHosted(service);
                    return;
                }
            }

            // Fallback: start and block indefinitely (allows running as a simple daemon)
            service.Start();
            Console.WriteLine($"{service.ServiceName} running in background (no platform host found).");
            Thread.Sleep(Timeout.Infinite);
        }
    }
}