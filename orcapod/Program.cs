using OrcaPod;
using System.Threading;

namespace OrcaPod
{
    class Program
    {
        static void Main(string[] args)
        {
            OrcaPod.Utils.Watchdog watchdog = new OrcaPod.Utils.Watchdog();
            watchdog.TestWatchdog();
        }
    }
}