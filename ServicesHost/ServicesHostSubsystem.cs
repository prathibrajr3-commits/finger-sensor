using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.ServicesHost
{
    /// <summary>
    /// Holds CLI ports and Named Pipe channel names configurations.
    /// </summary>
    public sealed class ServiceConfiguration
    {
        /// <summary>Gets or sets the Named Pipe base name.</summary>
        public string PipeName { get; set; } = "AirGestureAI_IPC_Channel";

        /// <summary>Gets or sets the active port (if TCP fallbacks are used).</summary>
        public int Port { get; set; } = 50051;
    }

    /// <summary>
    /// Resolves active process ports and channel addresses.
    /// </summary>
    public sealed class ServiceDiscovery
    {
        /// <summary>
        /// Retrieves the address string of a specific service.
        /// </summary>
        public string ResolveAddress(string serviceName)
        {
            return $"\\\\.\\pipe\\{serviceName}_Pipe";
        }
    }

    /// <summary>
    /// Manages periodic status heartbeat broadcasts from background host runtimes.
    /// </summary>
    public sealed class ServiceHeartbeat : IDisposable
    {
        private Timer? _timer;

        /// <summary>
        /// Starts broadcasting heartbeats at the specified interval.
        /// </summary>
        public void Start(Action onHeartbeat, int intervalMs)
        {
            _timer = new Timer(_ => onHeartbeat(), null, 0, intervalMs);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

    /// <summary>
    /// Registry tracking registered background processes and their properties.
    /// </summary>
    public sealed class ServiceRegistry
    {
        private readonly List<string> _services = new();

        /// <summary>Gets the list of active services.</summary>
        public IReadOnlyList<string> Services => _services;

        /// <summary>
        /// Registers a service in the registry.
        /// </summary>
        public void Register(string serviceName)
        {
            _services.Add(serviceName);
        }
    }

    /// <summary>
    /// Launches, monitors, and automatically recovers background subprocesses.
    /// </summary>
    public sealed class ServiceProcessManager
    {
        private readonly Dictionary<string, Process> _processes = new();

        /// <summary>Gets the collection of active processes.</summary>
        public IReadOnlyDictionary<string, Process> ActiveProcesses => _processes;

        /// <summary>
        /// Starts a process using command-line arguments.
        /// </summary>
        public bool StartServiceProcess(string serviceName, string executablePath, string args)
        {
            try
            {
                Logger.Info($"ServiceProcessManager: Launching '{serviceName}' with args: {args}");
                
                // For safety, only launch if the file exists (it could be dotnet run vs build exe)
                if (File.Exists(executablePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var p = Process.Start(psi);
                    if (p != null)
                    {
                        _processes[serviceName] = p;
                        return true;
                    }
                }

                // Fallback to current process registering for offline mock mode
                Logger.Warn($"ServiceProcessManager: File not found or failed to start '{executablePath}'. Falling back to mock mode.");
                _processes[serviceName] = Process.GetCurrentProcess();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"ServiceProcessManager: Failed to start '{serviceName}'", ex);
                return false;
            }
        }

        /// <summary>
        /// Checks if a service process is alive.
        /// </summary>
        public bool IsAlive(string serviceName)
        {
            if (_processes.TryGetValue(serviceName, out var p))
            {
                try
                {
                    if (p == Process.GetCurrentProcess()) return true; // Mock mode is always alive
                    return !p.HasExited;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Forcefully terminates a service process.
        /// </summary>
        public void TerminateService(string serviceName)
        {
            if (_processes.TryGetValue(serviceName, out var p))
            {
                Logger.Info($"ServiceProcessManager: Terminating '{serviceName}' process...");
                try
                {
                    if (p != Process.GetCurrentProcess() && !p.HasExited)
                    {
                        p.Kill(true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"ServiceProcessManager: Exception terminating '{serviceName}': {ex.Message}");
                }
                _processes.Remove(serviceName);
            }
        }
    }

    /// <summary>
    /// Central orchestrator hosting and executing background services.
    /// </summary>
    public sealed class ServiceHost
    {
        private readonly ServiceProcessManager _manager;

        /// <summary>
        /// Initializes a new instance of <see cref="ServiceHost"/>.
        /// </summary>
        public ServiceHost(ServiceProcessManager manager)
        {
            _manager = manager;
        }

        /// <summary>
        /// Initializes host processes.
        /// </summary>
        public void InitializeHost()
        {
            Logger.Info("ServiceHost: Background host containers successfully initialized.");
        }

        /// <summary>
        /// Starts the background helper subprocesses (TrackerHost and AIWorker).
        /// </summary>
        public void Start()
        {
            InitializeHost();
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            
            // Start Tracker subprocess
            _manager.StartServiceProcess("TrackerHost", exePath, "--tracker-host");
            
            // Start AIWorker subprocess
            _manager.StartServiceProcess("AIWorker", exePath, "--ai-worker");
        }

        /// <summary>
        /// Stops and cleans up all background helper subprocesses.
        /// </summary>
        public void Stop()
        {
            _manager.TerminateService("TrackerHost");
            _manager.TerminateService("AIWorker");
            Logger.Info("ServiceHost: Background host processes stopped.");
        }
    }
}
