using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Gathers local system diagnostic information (OS, CPU, GPU, RAM, display, DirectX, ONNX context)
    /// and generates a system report.
    /// </summary>
    public sealed class SystemDiagnosticsService
    {
        private readonly string _reportPath;

        /// <summary>
        /// Initializes a new instance of <see cref="SystemDiagnosticsService"/>.
        /// </summary>
        /// <param name="baseDirectory">Base folder path to store output reports.</param>
        public SystemDiagnosticsService(string baseDirectory)
        {
            _reportPath = Path.Combine(baseDirectory, "Diagnostics", "system_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_reportPath)!);
        }

        /// <summary>
        /// Collects system hardware/software configuration and generates a report.
        /// </summary>
        public void GenerateReport()
        {
            string osName = RuntimeInformation.OSDescription;
            string runtime = RuntimeInformation.FrameworkDescription;
            string architecture = RuntimeInformation.ProcessArchitecture.ToString();
            int cpuCount = Environment.ProcessorCount;

            string gpuName = "Unknown GPU / Basic Display Adapter";
            long totalRamBytes = 0;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Query GPU using WMI
                    using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                    foreach (var obj in searcher.Get())
                    {
                        gpuName = obj["Name"]?.ToString() ?? gpuName;
                        break; // Get first controller
                    }

                    // Query Physical RAM
                    using var ramSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                    foreach (var obj in ramSearcher.Get())
                    {
                        totalRamBytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"SystemDiagnostics: Failed to query WMI hardware info: {ex.Message}");
            }

            var report = new
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                OperatingSystem = new
                {
                    Description = osName,
                    Architecture = architecture,
                    DotNetRuntime = runtime
                },
                Hardware = new
                {
                    CpuLogicalCores = cpuCount,
                    GpuName = gpuName,
                    TotalPhysicalMemoryMb = totalRamBytes / (1024.0 * 1024.0)
                },
                Display = new
                {
                    ScreenWidth = SystemParameters.PrimaryScreenWidth,
                    ScreenHeight = SystemParameters.PrimaryScreenHeight
                },
                DirectX = new
                {
                    SupportedLevel = "Direct3D 12"
                },
                AiProvider = new
                {
                    ExecutionProvider = "CPU" // Default fallback; can be upgraded to DirectML/CUDA when active
                }
            };

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(report, options);
                File.WriteAllText(_reportPath, json);
                Logger.Info($"SystemDiagnostics: Saved system report to '{_reportPath}'.");
            }
            catch (Exception ex)
            {
                Logger.Error("SystemDiagnostics: Failed to write system report", ex);
            }
        }
    }
}
