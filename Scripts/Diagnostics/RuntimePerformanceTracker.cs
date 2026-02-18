using Godot;
using System;
using System.Diagnostics;

namespace ImmortalIdle.Diagnostics
{
    public readonly struct RuntimePerformanceSummary
    {
        public int SampleCount { get; init; }
        public double SampledSeconds { get; init; }

        public double CpuPercentCurrent { get; init; }
        public double CpuPercentAverage { get; init; }
        public double CpuPercentPeak { get; init; }

        public double WorkingSetMbCurrent { get; init; }
        public double WorkingSetMbAverage { get; init; }
        public double WorkingSetMbPeak { get; init; }

        public double PrivateMemoryMbCurrent { get; init; }
        public double PrivateMemoryMbAverage { get; init; }
        public double PrivateMemoryMbPeak { get; init; }

        public double ManagedMemoryMbCurrent { get; init; }
        public double ManagedMemoryMbAverage { get; init; }
        public double ManagedMemoryMbPeak { get; init; }

        public bool GpuUsageAvailable { get; init; }
        public double GpuUsagePercentCurrent { get; init; }
        public double GpuUsagePercentAverage { get; init; }
        public double GpuUsagePercentPeak { get; init; }

        public bool GpuVramAvailable { get; init; }
        public double GpuVramMbCurrent { get; init; }
        public double GpuVramMbAverage { get; init; }
        public double GpuVramMbPeak { get; init; }

        public double FpsCurrent { get; init; }
        public double FpsAverage { get; init; }
        public double FpsMin { get; init; }

        public double FrameTimeMsCurrent { get; init; }
        public double FrameTimeMsAverage { get; init; }
        public double FrameTimeMsPeak { get; init; }

        public double ThreadCountCurrent { get; init; }
        public double ThreadCountAverage { get; init; }
        public double ThreadCountPeak { get; init; }
    }

    public sealed class RuntimePerformanceTracker
    {
        private const double SampleIntervalSec = 1.0;
        private const double ByteToMb = 1024.0 * 1024.0;

        private readonly Process _process;
        private readonly int _cpuCoreCount;

        private bool _cpuInitialized;
        private TimeSpan _lastCpuTime;
        private double _lastCpuWallTimeSec;
        private double _sampleTimerSec;
        private double _sampledSeconds;
        private int _sampleCount;

        private double _cpuCurrent;
        private double _cpuSum;
        private double _cpuPeak;

        private double _workingSetCurrent;
        private double _workingSetSum;
        private double _workingSetPeak;

        private double _privateMemCurrent;
        private double _privateMemSum;
        private double _privateMemPeak;

        private double _managedMemCurrent;
        private double _managedMemSum;
        private double _managedMemPeak;

        private bool _gpuUsageAvailable;
        private double _gpuUsageCurrent;
        private double _gpuUsageSum;
        private double _gpuUsagePeak;

        private bool _gpuVramAvailable;
        private double _gpuVramCurrent;
        private double _gpuVramSum;
        private double _gpuVramPeak;
        private bool _gpuMonitorsResolved;
        private Performance.Monitor _gpuUsageMonitor;
        private Performance.Monitor _gpuVramMonitor;

        private double _fpsCurrent;
        private double _fpsSum;
        private double _fpsMin = double.MaxValue;

        private double _frameTimeCurrent;
        private double _frameTimeSum;
        private double _frameTimePeak;

        private double _threadCountCurrent;
        private double _threadCountSum;
        private double _threadCountPeak;

        public RuntimePerformanceTracker()
        {
            _process = Process.GetCurrentProcess();
            _cpuCoreCount = Math.Max(1, System.Environment.ProcessorCount);
        }

        public void Update(double deltaSeconds)
        {
            _sampleTimerSec += Math.Max(0.0, deltaSeconds);
            while (_sampleTimerSec >= SampleIntervalSec)
            {
                _sampleTimerSec -= SampleIntervalSec;
                SampleOnce();
            }
        }

        public void Reset()
        {
            _sampleTimerSec = 0;
            _sampledSeconds = 0;
            _sampleCount = 0;

            _cpuInitialized = false;
            _lastCpuTime = TimeSpan.Zero;
            _lastCpuWallTimeSec = 0;

            _cpuCurrent = 0;
            _cpuSum = 0;
            _cpuPeak = 0;

            _workingSetCurrent = 0;
            _workingSetSum = 0;
            _workingSetPeak = 0;

            _privateMemCurrent = 0;
            _privateMemSum = 0;
            _privateMemPeak = 0;

            _managedMemCurrent = 0;
            _managedMemSum = 0;
            _managedMemPeak = 0;

            _gpuUsageAvailable = false;
            _gpuUsageCurrent = 0;
            _gpuUsageSum = 0;
            _gpuUsagePeak = 0;

            _gpuVramAvailable = false;
            _gpuVramCurrent = 0;
            _gpuVramSum = 0;
            _gpuVramPeak = 0;
            _gpuMonitorsResolved = false;

            _fpsCurrent = 0;
            _fpsSum = 0;
            _fpsMin = double.MaxValue;

            _frameTimeCurrent = 0;
            _frameTimeSum = 0;
            _frameTimePeak = 0;

            _threadCountCurrent = 0;
            _threadCountSum = 0;
            _threadCountPeak = 0;
        }

        public RuntimePerformanceSummary GetSummary()
        {
            double divisor = Math.Max(1, _sampleCount);
            double fpsMin = _fpsMin == double.MaxValue ? 0 : _fpsMin;

            return new RuntimePerformanceSummary
            {
                SampleCount = _sampleCount,
                SampledSeconds = _sampledSeconds,

                CpuPercentCurrent = _cpuCurrent,
                CpuPercentAverage = _cpuSum / divisor,
                CpuPercentPeak = _cpuPeak,

                WorkingSetMbCurrent = _workingSetCurrent,
                WorkingSetMbAverage = _workingSetSum / divisor,
                WorkingSetMbPeak = _workingSetPeak,

                PrivateMemoryMbCurrent = _privateMemCurrent,
                PrivateMemoryMbAverage = _privateMemSum / divisor,
                PrivateMemoryMbPeak = _privateMemPeak,

                ManagedMemoryMbCurrent = _managedMemCurrent,
                ManagedMemoryMbAverage = _managedMemSum / divisor,
                ManagedMemoryMbPeak = _managedMemPeak,

                GpuUsageAvailable = _gpuUsageAvailable,
                GpuUsagePercentCurrent = _gpuUsageCurrent,
                GpuUsagePercentAverage = _gpuUsageSum / divisor,
                GpuUsagePercentPeak = _gpuUsagePeak,

                GpuVramAvailable = _gpuVramAvailable,
                GpuVramMbCurrent = _gpuVramCurrent,
                GpuVramMbAverage = _gpuVramSum / divisor,
                GpuVramMbPeak = _gpuVramPeak,

                FpsCurrent = _fpsCurrent,
                FpsAverage = _fpsSum / divisor,
                FpsMin = fpsMin,

                FrameTimeMsCurrent = _frameTimeCurrent,
                FrameTimeMsAverage = _frameTimeSum / divisor,
                FrameTimeMsPeak = _frameTimePeak,

                ThreadCountCurrent = _threadCountCurrent,
                ThreadCountAverage = _threadCountSum / divisor,
                ThreadCountPeak = _threadCountPeak
            };
        }

        private void SampleOnce()
        {
            _sampleCount++;
            _sampledSeconds += SampleIntervalSec;

            SampleCpu();
            SampleMemory();
            SampleFpsAndFrameTime();
            SampleThreadCount();
            SampleGpu();
        }

        private void SampleCpu()
        {
            try
            {
                _process.Refresh();
                TimeSpan cpuNow = _process.TotalProcessorTime;
                double wallNow = Time.GetTicksMsec() / 1000.0;

                if (!_cpuInitialized)
                {
                    _cpuInitialized = true;
                    _lastCpuTime = cpuNow;
                    _lastCpuWallTimeSec = wallNow;
                    _cpuCurrent = 0;
                }
                else
                {
                    double wallDelta = Math.Max(0.001, wallNow - _lastCpuWallTimeSec);
                    TimeSpan cpuDelta = cpuNow - _lastCpuTime;
                    double cpuPercent = cpuDelta.TotalMilliseconds / (wallDelta * 1000.0 * _cpuCoreCount) * 100.0;
                    _cpuCurrent = Math.Clamp(cpuPercent, 0.0, 100.0);

                    _lastCpuTime = cpuNow;
                    _lastCpuWallTimeSec = wallNow;
                }
            }
            catch
            {
                _cpuCurrent = 0;
            }

            _cpuSum += _cpuCurrent;
            _cpuPeak = Math.Max(_cpuPeak, _cpuCurrent);
        }

        private void SampleMemory()
        {
            try
            {
                _process.Refresh();
                _workingSetCurrent = _process.WorkingSet64 / ByteToMb;
                _privateMemCurrent = _process.PrivateMemorySize64 / ByteToMb;
            }
            catch
            {
                _workingSetCurrent = 0;
                _privateMemCurrent = 0;
            }

            _managedMemCurrent = GC.GetTotalMemory(false) / ByteToMb;

            _workingSetSum += _workingSetCurrent;
            _workingSetPeak = Math.Max(_workingSetPeak, _workingSetCurrent);

            _privateMemSum += _privateMemCurrent;
            _privateMemPeak = Math.Max(_privateMemPeak, _privateMemCurrent);

            _managedMemSum += _managedMemCurrent;
            _managedMemPeak = Math.Max(_managedMemPeak, _managedMemCurrent);
        }

        private void SampleFpsAndFrameTime()
        {
            _fpsCurrent = Math.Max(0, Engine.GetFramesPerSecond());
            _frameTimeCurrent = _fpsCurrent > 0.0 ? 1000.0 / _fpsCurrent : 0.0;

            _fpsSum += _fpsCurrent;
            _fpsMin = Math.Min(_fpsMin, _fpsCurrent);

            _frameTimeSum += _frameTimeCurrent;
            _frameTimePeak = Math.Max(_frameTimePeak, _frameTimeCurrent);
        }

        private void SampleThreadCount()
        {
            try
            {
                _process.Refresh();
                _threadCountCurrent = _process.Threads.Count;
            }
            catch
            {
                _threadCountCurrent = 0;
            }

            _threadCountSum += _threadCountCurrent;
            _threadCountPeak = Math.Max(_threadCountPeak, _threadCountCurrent);
        }

        private void SampleGpu()
        {
            if (!_gpuMonitorsResolved)
            {
                ResolveGpuMonitors();
                _gpuMonitorsResolved = true;
            }

            _gpuUsageCurrent = 0;
            if (_gpuUsageAvailable)
            {
                try
                {
                    _gpuUsageCurrent = Math.Max(0.0, Godot.Performance.GetMonitor(_gpuUsageMonitor));
                }
                catch
                {
                    _gpuUsageAvailable = false;
                    _gpuUsageCurrent = 0;
                }
            }

            _gpuVramCurrent = 0;
            if (_gpuVramAvailable)
            {
                try
                {
                    _gpuVramCurrent = Math.Max(0.0, Godot.Performance.GetMonitor(_gpuVramMonitor) / ByteToMb);
                }
                catch
                {
                    _gpuVramAvailable = false;
                    _gpuVramCurrent = 0;
                }
            }

            _gpuUsageSum += _gpuUsageCurrent;
            _gpuUsagePeak = Math.Max(_gpuUsagePeak, _gpuUsageCurrent);

            _gpuVramSum += _gpuVramCurrent;
            _gpuVramPeak = Math.Max(_gpuVramPeak, _gpuVramCurrent);
        }

        private void ResolveGpuMonitors()
        {
            _gpuUsageAvailable = TryResolveMonitor(
                out _gpuUsageMonitor,
                "TimeGpu",
                "RenderingTimeGpu");

            _gpuVramAvailable = TryResolveMonitor(
                out _gpuVramMonitor,
                "RenderVideoMemUsed",
                "RenderingVideoMemUsed");
        }

        private static bool TryResolveMonitor(out Performance.Monitor monitor, params string[] names)
        {
            foreach (string name in names)
            {
                if (Enum.TryParse(name, out monitor))
                {
                    return true;
                }
            }

            monitor = default;
            return false;
        }
    }
}
