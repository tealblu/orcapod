#pragma warning disable CA1416 // Validate platform compatibility - this is a windows only service

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using OrcaPod.Service; // reference to the cross-platform core service types

namespace OrcaPod.Service
{
    public class OrcaPodWindowsService : ServiceBase, IPlatformServiceHost
    {
        public const string DefaultServiceName = "OrcaPodService";

        // Reference to the cross-platform core service that does the real work.
        private MainService? _coreService;

        public OrcaPodWindowsService()
        {
            ServiceName = DefaultServiceName;
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        // IPlatformServiceHost implementation. The program's Main will create
        // an instance of this class and call RunHosted to hand control to the
        // SCM-backed ServiceBase loop.
        public void RunHosted(MainService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _coreService = service;
            // Start the ServiceBase loop. This will result in OnStart being called
            // by the service control manager, which will start the core service.
            ServiceBase.Run(new ServiceBase[] { this });
        }

        protected override void OnStart(string[] args)
        {
            // When the SCM starts this service, start the provided core MainService.
            _coreService?.Start();
        }

        protected override void OnStop()
        {
            // When the SCM stops this service, stop the provided core MainService.
            _coreService?.Stop();
        }
    }

    public static class ServiceManager
    {
        // Access rights and service type constants
        private const uint SC_MANAGER_CREATE_SERVICE = 0x0002;
        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_DEMAND_START = 0x00000003;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint SERVICE_QUERY_STATUS = 0x0004;
        private const uint SERVICE_START = 0x0010;
        private const uint SERVICE_STOP = 0x0020;
        private const uint DELETE = 0x00010000;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateService(
            IntPtr hSCManager,
            string lpServiceName,
            string lpDisplayName,
            uint dwDesiredAccess,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string? lpBinaryPathName,
            string? lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string? lpDependencies,
            string? lpServiceStartName,
            string? lpPassword);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        // Install (add) a service to the SCM
        public static void Install(string serviceName, string? displayName = null, string? binaryPath = null)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("serviceName is required.", nameof(serviceName));
            displayName = displayName ?? serviceName;
            // Assembly.GetEntryAssembly() can be null in some hosting scenarios; fall back to executing assembly
            binaryPath = binaryPath ?? System.Reflection.Assembly.GetEntryAssembly()?.Location ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            // Ensure path is quoted
            if (!binaryPath.StartsWith("\"")) binaryPath = "\"" + binaryPath + "\"";

            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CREATE_SERVICE);
            if (scm == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenSCManager failed");

            try
            {
                IntPtr svc = CreateService(
                    scm,
                    serviceName,
                    displayName,
                    SERVICE_ALL_ACCESS,
                    SERVICE_WIN32_OWN_PROCESS,
                    SERVICE_DEMAND_START,
                    SERVICE_ERROR_NORMAL,
                    binaryPath,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null);

                if (svc == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateService failed");

                CloseServiceHandle(svc);
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        // Remove (uninstall) a service from the SCM
        public static void Uninstall(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("serviceName is required.", nameof(serviceName));

            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CREATE_SERVICE);
            if (scm == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenSCManager failed");

            try
            {
                IntPtr svc = OpenService(scm, serviceName, DELETE);
                if (svc == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == 1060) // ERROR_SERVICE_DOES_NOT_EXIST
                        return;
                    throw new Win32Exception(err, "OpenService failed");
                }

                try
                {
                    if (!DeleteService(svc))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "DeleteService failed");
                }
                finally
                {
                    CloseServiceHandle(svc);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        // Start a service (uses ServiceController)
        public static void Start(string serviceName, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("serviceName is required.", nameof(serviceName));

            timeout ??= TimeSpan.FromSeconds(30);
            using (var sc = new ServiceController(serviceName))
            {
                if (sc.Status == ServiceControllerStatus.Running)
                    return;

                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, timeout.Value);
            }
        }

        // Stop a service (uses ServiceController)
        public static void Stop(string serviceName, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("serviceName is required.", nameof(serviceName));

            timeout ??= TimeSpan.FromSeconds(30);
            using (var sc = new ServiceController(serviceName))
            {
                if (sc.Status == ServiceControllerStatus.Stopped)
                    return;

                sc.Stop();

                sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout.Value);
            }
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
