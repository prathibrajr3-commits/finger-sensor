using System;
using System.Collections.Generic;

namespace AirGestureAI.Analytics
{
    /// <summary>
    /// Collects hardware metrics, frame processing rates, and pipeline execution latencies.
    /// </summary>
    public class PerformanceAnalytics
    {
        private readonly List<double> _cpuUsageHistory = new();
        private readonly List<double> _ramUsageHistory = new();
        private readonly List<double> _latencyHistory = new();
        private readonly List<double> _fpsHistory = new();

        /// <summary>Gets the CPU usage history.</summary>
        public IReadOnlyList<double> CpuHistory => _cpuUsageHistory;

        /// <summary>Gets the RAM usage history.</summary>
        public IReadOnlyList<double> RamHistory => _ramUsageHistory;

        /// <summary>Gets the processing latency history.</summary>
        public IReadOnlyList<double> LatencyHistory => _latencyHistory;

        /// <summary>Gets the FPS history.</summary>
        public IReadOnlyList<double> FpsHistory => _fpsHistory;

        /// <summary>
        /// Logs performance snapshots.
        /// </summary>
        public void LogMetrics(double cpu, double ramMb, double latencyMs, double fps)
        {
            _cpuUsageHistory.Add(cpu);
            _ramUsageHistory.Add(ramMb);
            _latencyHistory.Add(latencyMs);
            _fpsHistory.Add(fps);

            // Cap the list lengths to 500 items
            if (_cpuUsageHistory.Count > 500)
            {
                _cpuUsageHistory.RemoveAt(0);
                _ramUsageHistory.RemoveAt(0);
                _latencyHistory.RemoveAt(0);
                _fpsHistory.RemoveAt(0);
            }
        }
    }
}
