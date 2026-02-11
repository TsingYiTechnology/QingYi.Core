using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace QingYi.Core.Timer
{
    /// <summary>
    /// Provides ultra-high precision timing with decimal nanosecond resolution.
    /// Uses platform-specific APIs for maximum accuracy with decimal arithmetic.
    /// - Windows: QueryPerformanceCounter / QueryPerformanceFrequency
    /// - Linux/macOS: clock_gettime with MONOTONIC_RAW
    /// Falls back to Stopwatch for compatibility.
    /// </summary>
    public unsafe sealed class DecimalUltraHighPrecisionTimer : IDisposable
    {
        /// <summary>
        /// Represents different timer modes available.
        /// </summary>
        public enum TimerMode
        {
            /// <summary>
            /// Automatically selects the best timer for the current platform.
            /// </summary>
            AutoDetect,

            /// <summary>
            /// Uses Windows QueryPerformanceCounter API.
            /// </summary>
            WindowsHighPrecision,

            /// <summary>
            /// Uses Unix clock_gettime with CLOCK_MONOTONIC_RAW.
            /// </summary>
            UnixMonotonicRaw,

            /// <summary>
            /// Uses Unix clock_gettime with CLOCK_MONOTONIC.
            /// </summary>
            UnixMonotonic,

            /// <summary>
            /// Uses managed System.Diagnostics.Stopwatch.
            /// </summary>
            ManagedStopwatch
        }

        #region Platform-Specific Native Imports

        [SupportedOSPlatform("windows")]
        private static class WindowsNative
        {
            [DllImport("kernel32.dll")]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            public static extern int QueryPerformanceCounter(long* lpPerformanceCount);

            [DllImport("kernel32.dll")]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            public static extern int QueryPerformanceFrequency(long* lpFrequency);
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static class UnixNative
        {
            public const int CLOCK_MONOTONIC_RAW = 4;
            public const int CLOCK_MONOTONIC = 1;
            public const int CLOCK_REALTIME = 0;

            [DllImport("libc", SetLastError = true)]
            public static extern int clock_gettime(int clk_id, TimeSpec* tp);

            [DllImport("libc", SetLastError = true)]
            public static extern int clock_getres(int clk_id, TimeSpec* tp);

            [StructLayout(LayoutKind.Sequential)]
            public struct TimeSpec
            {
                public long tv_sec;  // seconds
                public long tv_nsec; // nanoseconds
            }
        }

        #endregion

        #region Constants and Fields

        private TimerMode _mode;
        private bool _useUnsafeMethods;
        private bool _use128BitPrecision;
        private long _frequency;
        private decimal _tickDuration; // Time in seconds per tick as decimal
        private decimal _nanosecondsPerTick; // Nanoseconds per tick as decimal
        private decimal _picosecondsPerTick; // Picoseconds per tick as decimal

        // High precision constants for decimal conversions
        private static readonly decimal OneBillionDecimal = 1_000_000_000m;
        private static readonly decimal OneTrillionDecimal = 1_000_000_000_000m;

        // Cached high precision frequency as decimal
        private decimal _frequencyDecimal;

        // Delegates for performance
        private Func<long> _getTimestamp;
        private Func<long, decimal> _getElapsedSeconds;
        private Func<long, decimal> _getElapsedNanoseconds;
        private Func<long, decimal> _getElapsedPicoseconds;

        // For averaging and calibration
        private long[] _calibrationBuffer;
        private int _calibrationIndex;
        private decimal _calibrationOffset;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the timer resolution in picoseconds (10^-12 seconds).
        /// Lower values indicate higher precision.
        /// </summary>
        public decimal ResolutionPicoseconds { get; private set; }

        /// <summary>
        /// Gets the timer resolution in nanoseconds.
        /// Lower values indicate higher precision.
        /// </summary>
        public decimal ResolutionNanoseconds => ResolutionPicoseconds / 1000m;

        /// <summary>
        /// Gets the timer frequency in Hz (ticks per second) as decimal.
        /// </summary>
        public decimal FrequencyDecimal => _frequencyDecimal;

        /// <summary>
        /// Gets the timer frequency in Hz (ticks per second) as long.
        /// </summary>
        public long Frequency => _frequency;

        /// <summary>
        /// Gets the timer mode currently in use.
        /// </summary>
        public TimerMode Mode => _mode;

        /// <summary>
        /// Indicates whether unsafe methods are being used for maximum performance.
        /// </summary>
        public bool IsUsingUnsafeMethods => _useUnsafeMethods;

        /// <summary>
        /// Indicates whether 128-bit precision is being used.
        /// </summary>
        public bool IsUsing128BitPrecision => _use128BitPrecision;

        /// <summary>
        /// Gets the calibration offset in ticks for reducing systematic error.
        /// </summary>
        public decimal CalibrationOffsetTicks => _calibrationOffset;

        /// <summary>
        /// Gets the calibration offset in nanoseconds.
        /// </summary>
        public decimal CalibrationOffsetNanoseconds => _calibrationOffset * _nanosecondsPerTick;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance with maximum precision settings.
        /// </summary>
        /// <param name="calibrationSamples">Number of samples for calibration buffer.</param>
        /// <param name="use128BitPrecision">Whether to use 128-bit integer arithmetic where available.</param>
        public DecimalUltraHighPrecisionTimer(int calibrationSamples = 1024, bool use128BitPrecision = true)
        {
            _use128BitPrecision = use128BitPrecision && Has128BitSupport();
            _useUnsafeMethods = true;
            _calibrationBuffer = new long[calibrationSamples];

            InitializePlatformSpecificTimer();
            PerformCalibration();

            // Create delegates
            CreateDelegateMethods();
        }

        /// <summary>
        /// Initializes a new instance with specified timer mode.
        /// </summary>
        /// <param name="mode">The timer mode to use.</param>
        /// <param name="calibrationSamples">Number of samples for calibration buffer.</param>
        /// <param name="use128BitPrecision">Whether to use 128-bit integer arithmetic where available.</param>
        public DecimalUltraHighPrecisionTimer(TimerMode mode, int calibrationSamples = 1024, bool use128BitPrecision = true)
        {
            _mode = mode;
            _use128BitPrecision = use128BitPrecision && Has128BitSupport();
            _useUnsafeMethods = (mode == TimerMode.WindowsHighPrecision ||
                               mode == TimerMode.UnixMonotonicRaw ||
                               mode == TimerMode.UnixMonotonic);
            _calibrationBuffer = new long[calibrationSamples];

            InitializePlatformSpecificTimer();
            PerformCalibration();

            CreateDelegateMethods();
        }

        private void InitializePlatformSpecificTimer()
        {
            if (_mode != TimerMode.AutoDetect)
            {
                SetupTimerForMode(_mode);
                return;
            }

            // Auto-detect best available timer
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (TryInitializeWindowsHighPrecision())
                {
                    _mode = TimerMode.WindowsHighPrecision;
                }
                else
                {
                    SetupStopwatchTimer();
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (TryInitializeUnixMonotonicRaw())
                {
                    _mode = TimerMode.UnixMonotonicRaw;
                }
                else if (TryInitializeUnixMonotonic())
                {
                    _mode = TimerMode.UnixMonotonic;
                }
                else
                {
                    SetupStopwatchTimer();
                }
            }
            else
            {
                SetupStopwatchTimer();
            }

            CalculateResolution();
        }

        #endregion

        #region Platform-Specific Initialization

        private bool TryInitializeWindowsHighPrecision()
        {
            if (!_useUnsafeMethods)
                return false;

            try
            {
                long frequency = 0;
                long counter = 0;

#pragma warning disable CA1416 // 验证平台兼容性
                if (WindowsNative.QueryPerformanceFrequency(&frequency) != 0 &&
                    WindowsNative.QueryPerformanceCounter(&counter) != 0)
                {
                    _frequency = frequency;
                    _frequencyDecimal = frequency;
                    _tickDuration = 1m / _frequencyDecimal;
                    _nanosecondsPerTick = OneBillionDecimal / _frequencyDecimal;
                    _picosecondsPerTick = OneTrillionDecimal / _frequencyDecimal;
                    return true;
                }
#pragma warning restore CA1416 // 验证平台兼容性
            }
            catch
            {
                // Fall through to return false
            }

            return false;
        }

        private bool TryInitializeUnixMonotonicRaw()
        {
            if (!_useUnsafeMethods)
                return false;

            try
            {
                UnixNative.TimeSpec ts = default;

#pragma warning disable CA1416 // 验证平台兼容性
                if (UnixNative.clock_gettime(UnixNative.CLOCK_MONOTONIC_RAW, &ts) == 0)
                {
                    // Get actual resolution
                    UnixNative.TimeSpec res = default;
                    if (UnixNative.clock_getres(UnixNative.CLOCK_MONOTONIC_RAW, &res) == 0)
                    {
                        // Use actual resolution for better accuracy
                        _frequency = (long)(1_000_000_000_000m / ((decimal)res.tv_nsec * 1000m + (decimal)res.tv_sec * OneTrillionDecimal));
                    }
                    else
                    {
                        // Fallback to Stopwatch frequency
                        _frequency = Stopwatch.Frequency;
                    }

                    _frequencyDecimal = _frequency;
                    _tickDuration = 1m / _frequencyDecimal;
                    _nanosecondsPerTick = OneBillionDecimal / _frequencyDecimal;
                    _picosecondsPerTick = OneTrillionDecimal / _frequencyDecimal;
                    return true;
                }
#pragma warning restore CA1416 // 验证平台兼容性
            }
            catch
            {
                // Fall through to return false
            }

            return false;
        }

        private bool TryInitializeUnixMonotonic()
        {
            if (!_useUnsafeMethods)
                return false;

            try
            {
                UnixNative.TimeSpec ts = default;

#pragma warning disable CA1416 // 验证平台兼容性
                if (UnixNative.clock_gettime(UnixNative.CLOCK_MONOTONIC, &ts) == 0)
                {
                    // Get actual resolution
                    UnixNative.TimeSpec res = default;
                    if (UnixNative.clock_getres(UnixNative.CLOCK_MONOTONIC, &res) == 0)
                    {
                        _frequency = (long)(1_000_000_000_000m / ((decimal)res.tv_nsec * 1000m + (decimal)res.tv_sec * OneTrillionDecimal));
                    }
                    else
                    {
                        _frequency = Stopwatch.Frequency;
                    }

                    _frequencyDecimal = _frequency;
                    _tickDuration = 1m / _frequencyDecimal;
                    _nanosecondsPerTick = OneBillionDecimal / _frequencyDecimal;
                    _picosecondsPerTick = OneTrillionDecimal / _frequencyDecimal;
                    return true;
                }
#pragma warning restore CA1416 // 验证平台兼容性
            }
            catch
            {
                // Fall through to return false
            }

            return false;
        }

        private void SetupStopwatchTimer()
        {
            _mode = TimerMode.ManagedStopwatch;
            _frequency = Stopwatch.Frequency;
            _frequencyDecimal = _frequency;
            _tickDuration = 1m / _frequencyDecimal;
            _nanosecondsPerTick = OneBillionDecimal / _frequencyDecimal;
            _picosecondsPerTick = OneTrillionDecimal / _frequencyDecimal;
        }

        private void SetupTimerForMode(TimerMode mode)
        {
            switch (mode)
            {
                case TimerMode.WindowsHighPrecision:
                    if (!TryInitializeWindowsHighPrecision())
                        throw new PlatformNotSupportedException(
                            "Windows high precision timer is not available");
                    break;

                case TimerMode.UnixMonotonicRaw:
                    if (!TryInitializeUnixMonotonicRaw())
                        throw new PlatformNotSupportedException(
                            "Unix monotonic raw timer is not available");
                    break;

                case TimerMode.UnixMonotonic:
                    if (!TryInitializeUnixMonotonic())
                        throw new PlatformNotSupportedException(
                            "Unix monotonic timer is not available");
                    break;

                case TimerMode.ManagedStopwatch:
                    SetupStopwatchTimer();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode),
                        $"Unsupported timer mode: {mode}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the current timestamp in timer ticks with calibration applied.
        /// </summary>
        /// <returns>The calibrated timestamp in ticks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetTimestamp()
        {
            long timestamp = _getTimestamp();

            // Apply calibration offset if significant
            if (_calibrationOffset != 0m)
            {
                timestamp = (long)(timestamp + _calibrationOffset);
            }

            return timestamp;
        }

        /// <summary>
        /// Gets the current timestamp in raw ticks without calibration.
        /// </summary>
        /// <returns>The raw timestamp in ticks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetRawTimestamp() => _getTimestamp();

        /// <summary>
        /// Gets the elapsed time in seconds between two timestamps as decimal.
        /// </summary>
        /// <param name="startTimestamp">The starting timestamp.</param>
        /// <param name="endTimestamp">The ending timestamp.</param>
        /// <returns>Elapsed time in seconds as decimal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetElapsedSeconds(long startTimestamp, long endTimestamp) =>
            (endTimestamp - startTimestamp) * _tickDuration;

        /// <summary>
        /// Gets the elapsed time in nanoseconds between two timestamps as decimal.
        /// </summary>
        /// <param name="startTimestamp">The starting timestamp.</param>
        /// <param name="endTimestamp">The ending timestamp.</param>
        /// <returns>Elapsed time in nanoseconds as decimal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetElapsedNanoseconds(long startTimestamp, long endTimestamp) =>
            (endTimestamp - startTimestamp) * _nanosecondsPerTick;

        /// <summary>
        /// Gets the elapsed time in picoseconds between two timestamps as decimal.
        /// </summary>
        /// <param name="startTimestamp">The starting timestamp.</param>
        /// <param name="endTimestamp">The ending timestamp.</param>
        /// <returns>Elapsed time in picoseconds as decimal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetElapsedPicoseconds(long startTimestamp, long endTimestamp) =>
            (endTimestamp - startTimestamp) * _picosecondsPerTick;

        /// <summary>
        /// Gets the elapsed time in seconds since the specified timestamp as decimal.
        /// </summary>
        /// <param name="startTimestamp">The starting timestamp.</param>
        /// <returns>Elapsed time in seconds as decimal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetElapsedSeconds(long startTimestamp) =>
            _getElapsedSeconds(startTimestamp);

        /// <summary>
        /// Gets the elapsed time in nanoseconds since the specified timestamp as decimal.
        /// </summary>
        /// <param name="startTimestamp">The starting timestamp.</param>
        /// <returns>Elapsed time in nanoseconds as decimal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetElapsedNanoseconds(long startTimestamp) =>
            _getElapsedNanoseconds(startTimestamp);

        /// <summary>
        /// Gets the elapsed time in picoseconds since the specified timestamp as decimal.
        /// </summary>
        /// <param name="startTimestamp">The starting timestamp.</param>
        /// <returns>Elapsed time in picoseconds as decimal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetElapsedPicoseconds(long startTimestamp) =>
            _getElapsedPicoseconds(startTimestamp);

        /// <summary>
        /// Creates a timestamp measurement context for easy elapsed time calculation.
        /// </summary>
        /// <returns>A new timestamp measurement context.</returns>
        public DecimalTimestampContext CreateContext() => new(this);

        /// <summary>
        /// Measures the execution time of an action with picosecond precision.
        /// </summary>
        /// <param name="action">The action to measure.</param>
        /// <returns>Elapsed time in picoseconds as decimal.</returns>
        public decimal MeasureExecutionTime(Action action)
        {
            long start = GetTimestamp();
            action();
            long end = GetTimestamp();
            return GetElapsedPicoseconds(start, end);
        }

        /// <summary>
        /// Measures the execution time of a function with picosecond precision.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The function to measure.</param>
        /// <returns>A tuple containing the result and elapsed time in picoseconds as decimal.</returns>
        public (T Result, decimal ElapsedPicoseconds) MeasureExecutionTime<T>(Func<T> function)
        {
            long start = GetTimestamp();
            T result = function();
            long end = GetTimestamp();
            return (result, GetElapsedPicoseconds(start, end));
        }

        /// <summary>
        /// Measures the execution time multiple times and returns statistical information.
        /// </summary>
        /// <param name="action">The action to measure.</param>
        /// <param name="iterations">Number of measurement iterations.</param>
        /// <returns>Statistical timing information.</returns>
        public TimingStatistics MeasureExecutionTimeStatistics(Action action, int iterations = 1000)
        {
            if (iterations < 3)
                throw new ArgumentException("At least 3 iterations are required", nameof(iterations));

            decimal[] measurements = new decimal[iterations];

            for (int i = 0; i < iterations; i++)
            {
                long start = GetTimestamp();
                action();
                long end = GetTimestamp();
                measurements[i] = GetElapsedPicoseconds(start, end);
            }

            return new TimingStatistics(measurements);
        }

        /// <summary>
        /// Performs a new calibration to reduce systematic error.
        /// </summary>
        /// <param name="samples">Number of calibration samples to collect.</param>
        public void Recalibrate(int samples = 10000)
        {
            if (samples < 100)
                throw new ArgumentException("At least 100 samples are required", nameof(samples));

            PerformCalibration(samples);
        }

        #endregion

        #region Private Helper Methods

        private void CreateDelegateMethods()
        {
            if (_useUnsafeMethods && _mode == TimerMode.WindowsHighPrecision)
            {
                _getTimestamp = GetWindowsHighPrecisionTimestamp;
                _getElapsedSeconds = (start) =>
                    (GetWindowsHighPrecisionTimestamp() - start) * _tickDuration;
                _getElapsedNanoseconds = (start) =>
                    (GetWindowsHighPrecisionTimestamp() - start) * _nanosecondsPerTick;
                _getElapsedPicoseconds = (start) =>
                    (GetWindowsHighPrecisionTimestamp() - start) * _picosecondsPerTick;
            }
            else if (_useUnsafeMethods && _mode == TimerMode.UnixMonotonicRaw)
            {
                _getTimestamp = GetUnixMonotonicRawTimestamp;
                _getElapsedSeconds = (start) =>
                    (GetUnixMonotonicRawTimestamp() - start) * _tickDuration;
                _getElapsedNanoseconds = (start) =>
                    (GetUnixMonotonicRawTimestamp() - start) * _nanosecondsPerTick;
                _getElapsedPicoseconds = (start) =>
                    (GetUnixMonotonicRawTimestamp() - start) * _picosecondsPerTick;
            }
            else if (_useUnsafeMethods && _mode == TimerMode.UnixMonotonic)
            {
                _getTimestamp = GetUnixMonotonicTimestamp;
                _getElapsedSeconds = (start) =>
                    (GetUnixMonotonicTimestamp() - start) * _tickDuration;
                _getElapsedNanoseconds = (start) =>
                    (GetUnixMonotonicTimestamp() - start) * _nanosecondsPerTick;
                _getElapsedPicoseconds = (start) =>
                    (GetUnixMonotonicTimestamp() - start) * _picosecondsPerTick;
            }
            else
            {
                _getTimestamp = Stopwatch.GetTimestamp;
                _getElapsedSeconds = (start) =>
                    (Stopwatch.GetTimestamp() - start) * _tickDuration;
                _getElapsedNanoseconds = (start) =>
                    (Stopwatch.GetTimestamp() - start) * _nanosecondsPerTick;
                _getElapsedPicoseconds = (start) =>
                    (Stopwatch.GetTimestamp() - start) * _picosecondsPerTick;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetWindowsHighPrecisionTimestamp()
        {
            long counter = 0;
#pragma warning disable CA1416 // 验证平台兼容性
            WindowsNative.QueryPerformanceCounter(&counter);
#pragma warning restore CA1416 // 验证平台兼容性
            return counter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetUnixMonotonicRawTimestamp()
        {
            UnixNative.TimeSpec ts = default;
#pragma warning disable CA1416 // 验证平台兼容性
            UnixNative.clock_gettime(UnixNative.CLOCK_MONOTONIC_RAW, &ts);
#pragma warning restore CA1416 // 验证平台兼容性

            // Convert to ticks using high precision arithmetic
            if (_use128BitPrecision)
            {
                // Use 128-bit arithmetic for better precision
#pragma warning disable CA1416 // 验证平台兼容性
                ulong secs = (ulong)ts.tv_sec;
                ulong nsecs = (ulong)ts.tv_nsec;
#pragma warning restore CA1416 // 验证平台兼容性
                ulong freq = (ulong)_frequency;

                // Calculate using 128-bit multiplication
                UInt128 nanoseconds = (UInt128)secs * 1_000_000_000UL + (UInt128)nsecs;
                UInt128 ticks = nanoseconds * (UInt128)freq / 1_000_000_000UL;
                return (long)ticks;
            }
            else
            {
                // Fallback to decimal arithmetic
#pragma warning disable CA1416 // 验证平台兼容性
                decimal nanoseconds = ts.tv_sec * OneBillionDecimal + ts.tv_nsec;
#pragma warning restore CA1416 // 验证平台兼容性
                return (long)(nanoseconds * _frequencyDecimal / OneBillionDecimal);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetUnixMonotonicTimestamp()
        {
            UnixNative.TimeSpec ts = default;
#pragma warning disable CA1416 // 验证平台兼容性
            UnixNative.clock_gettime(UnixNative.CLOCK_MONOTONIC, &ts);
#pragma warning restore CA1416 // 验证平台兼容性

            if (_use128BitPrecision)
            {
#pragma warning disable CA1416 // 验证平台兼容性
                ulong secs = (ulong)ts.tv_sec;
                ulong nsecs = (ulong)ts.tv_nsec;
#pragma warning restore CA1416 // 验证平台兼容性
                ulong freq = (ulong)_frequency;

                UInt128 nanoseconds = (UInt128)secs * 1_000_000_000UL + (UInt128)nsecs;
                UInt128 ticks = nanoseconds * (UInt128)freq / 1_000_000_000UL;
                return (long)ticks;
            }
            else
            {
#pragma warning disable CA1416 // 验证平台兼容性
                decimal nanoseconds = ts.tv_sec * OneBillionDecimal + ts.tv_nsec;
#pragma warning restore CA1416 // 验证平台兼容性
                return (long)(nanoseconds * _frequencyDecimal / OneBillionDecimal);
            }
        }

        private void CalculateResolution()
        {
            // Measure timer resolution by taking multiple samples
            const int samples = 1000;
            long[] deltas = new long[samples];

            for (int i = 0; i < samples; i++)
            {
                long t1 = GetRawTimestamp();
                long t2 = GetRawTimestamp();

                // Spin until we get a different timestamp
                int spinCount = 0;
                while (t2 == t1 && spinCount < 1000000)
                {
                    t2 = GetRawTimestamp();
                    spinCount++;
                }

                deltas[i] = t2 - t1;
            }

            // Use minimum delta as resolution estimate
            long minDelta = 1;
            foreach (var delta in deltas)
            {
                if (delta > 0 && delta < minDelta)
                {
                    minDelta = delta;
                }
            }

            ResolutionPicoseconds = minDelta * _picosecondsPerTick;
        }

        private void PerformCalibration(int samples = 10000)
        {
            if (samples < 100) return;

            decimal totalOffset = 0m;
            int validSamples = 0;

            for (int i = 0; i < samples; i++)
            {
                long t1 = GetRawTimestamp();
                long t2 = GetRawTimestamp();

                if (t2 > t1)
                {
                    // Store in circular buffer for running average
                    _calibrationBuffer[_calibrationIndex] = t2 - t1;
                    _calibrationIndex = (_calibrationIndex + 1) % _calibrationBuffer.Length;

                    // Calculate ideal offset to minimize quantization error
                    decimal delta = t2 - t1;
                    if (delta > 0)
                    {
                        decimal idealDelta = 1m / _tickDuration; // 1 tick in ideal world
                        totalOffset += idealDelta - delta;
                        validSamples++;
                    }
                }
            }

            if (validSamples > 0)
            {
                // Calculate average offset to apply
                _calibrationOffset = totalOffset / validSamples;

                // Limit calibration to reasonable bounds
                if (_calibrationOffset > 1m || _calibrationOffset < -1m)
                {
                    _calibrationOffset = 0m;
                }
            }
        }

        private static bool Has128BitSupport()
        {
            // Check if 128-bit integers are available in .NET 8
            return true; // .NET 8 supports System.UInt128
        }

        private static bool IsUnsafeSupported()
        {
            try
            {
                Unsafe.SizeOf<int>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Advanced Timing Methods

        /// <summary>
        /// Measures the execution time with warmup and statistical analysis.
        /// </summary>
        /// <param name="action">The action to measure.</param>
        /// <param name="warmupIterations">Number of warmup iterations.</param>
        /// <param name="measurementIterations">Number of measurement iterations.</param>
        /// <returns>Detailed timing analysis.</returns>
        public DetailedTimingAnalysis MeasureWithAnalysis(
            Action action,
            int warmupIterations = 1000,
            int measurementIterations = 10000)
        {
            // Warmup
            for (int i = 0; i < warmupIterations; i++)
            {
                action();
            }

            // Measurement
            decimal[] measurements = new decimal[measurementIterations];
            decimal min = decimal.MaxValue;
            decimal max = decimal.MinValue;
            decimal sum = 0m;

            for (int i = 0; i < measurementIterations; i++)
            {
                long start = GetTimestamp();
                action();
                long end = GetTimestamp();

                decimal elapsed = GetElapsedPicoseconds(start, end);
                measurements[i] = elapsed;

                if (elapsed < min) min = elapsed;
                if (elapsed > max) max = elapsed;
                sum += elapsed;
            }

            decimal average = sum / measurementIterations;

            // Calculate standard deviation
            decimal varianceSum = 0m;
            for (int i = 0; i < measurementIterations; i++)
            {
                decimal diff = measurements[i] - average;
                varianceSum += diff * diff;
            }
            decimal variance = varianceSum / measurementIterations;
            decimal stdDev = SqrtDecimal(variance);

            // Sort for percentiles
            Array.Sort(measurements);
            decimal p50 = measurements[measurementIterations / 2];
            decimal p90 = measurements[(int)(measurementIterations * 0.9)];
            decimal p95 = measurements[(int)(measurementIterations * 0.95)];
            decimal p99 = measurements[(int)(measurementIterations * 0.99)];

            return new DetailedTimingAnalysis(
                min, max, average, stdDev, p50, p90, p95, p99, measurements);
        }

        private static decimal SqrtDecimal(decimal x)
        {
            if (x < 0) throw new ArgumentException("Cannot calculate square root of negative number");
            if (x == 0) return 0;

            decimal current = (decimal)Math.Sqrt((double)x);
            decimal previous;

            do
            {
                previous = current;
                current = (previous + x / previous) / 2;
            }
            while (Math.Abs(previous - current) > 0.0000000001m);

            return current;
        }

        #endregion

        #region IDisposable Implementation

        private bool _disposed;

        /// <summary>
        /// Releases all resources used by the timer.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Releases all resources used by the timer.
        /// </summary>
        ~DecimalUltraHighPrecisionTimer()
        {
            Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Provides a convenient context for measuring elapsed time with decimal precision.
    /// </summary>
    public ref struct DecimalTimestampContext
    {
        private readonly DecimalUltraHighPrecisionTimer _timer;
        private long _startTimestamp;

        /// <summary>
        /// Initializes a new instance of the <see cref="DecimalTimestampContext"/> struct.
        /// </summary>
        /// <param name="timer">The timer instance to use for measurements.</param>
        public DecimalTimestampContext(DecimalUltraHighPrecisionTimer timer)
        {
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _startTimestamp = timer.GetTimestamp();
        }

        /// <summary>
        /// Gets the elapsed time in seconds since the context was created.
        /// </summary>
        public decimal ElapsedSeconds => _timer.GetElapsedSeconds(_startTimestamp);

        /// <summary>
        /// Gets the elapsed time in nanoseconds since the context was created.
        /// </summary>
        public decimal ElapsedNanoseconds => _timer.GetElapsedNanoseconds(_startTimestamp);

        /// <summary>
        /// Gets the elapsed time in picoseconds since the context was created.
        /// </summary>
        public decimal ElapsedPicoseconds => _timer.GetElapsedPicoseconds(_startTimestamp);

        /// <summary>
        /// Restarts the measurement and returns the elapsed time since the last start.
        /// </summary>
        /// <returns>The elapsed time in picoseconds since the context was created or last restarted.</returns>
        public decimal Restart()
        {
            long newTimestamp = _timer.GetTimestamp();
            decimal elapsed = _timer.GetElapsedPicoseconds(_startTimestamp);
            _startTimestamp = newTimestamp;
            return elapsed;
        }
    }

    /// <summary>
    /// Represents detailed timing statistics.
    /// </summary>
    public readonly record struct TimingStatistics
    {
        /// <summary>
        /// Minimum measured time in picoseconds.
        /// </summary>
        public decimal Minimum { get; init; }

        /// <summary>
        /// Maximum measured time in picoseconds.
        /// </summary>
        public decimal Maximum { get; init; }

        /// <summary>
        /// Average measured time in picoseconds.
        /// </summary>
        public decimal Average { get; init; }

        /// <summary>
        /// Median measured time in picoseconds.
        /// </summary>
        public decimal Median { get; init; }

        /// <summary>
        /// Standard deviation in picoseconds.
        /// </summary>
        public decimal StandardDeviation { get; init; }

        /// <summary>
        /// Number of measurements.
        /// </summary>
        public int SampleCount { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimingStatistics"/> class with the specified measurements.
        /// </summary>
        /// <param name="measurements">An array of decimal timing measurements to analyze. Must contain at least one element.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="measurements"/> is null or empty.</exception>
        public TimingStatistics(decimal[] measurements)
        {
            if (measurements == null || measurements.Length == 0)
                throw new ArgumentException("Measurements cannot be null or empty", nameof(measurements));

            SampleCount = measurements.Length;

            // Calculate min, max, sum
            decimal min = decimal.MaxValue;
            decimal max = decimal.MinValue;
            decimal sum = 0m;

            foreach (var m in measurements)
            {
                if (m < min) min = m;
                if (m > max) max = m;
                sum += m;
            }

            Minimum = min;
            Maximum = max;
            Average = sum / SampleCount;

            // Calculate variance
            decimal varianceSum = 0m;
            foreach (var m in measurements)
            {
                decimal diff = m - Average;
                varianceSum += diff * diff;
            }

            decimal variance = varianceSum / SampleCount;
            StandardDeviation = Sqrt(variance);

            // Calculate median
            var sorted = measurements.OrderBy(x => x).ToArray();
            Median = SampleCount % 2 == 0
                ? (sorted[SampleCount / 2 - 1] + sorted[SampleCount / 2]) / 2
                : sorted[SampleCount / 2];
        }

        private static decimal Sqrt(decimal x)
        {
            if (x < 0) return 0;
            if (x == 0) return 0;

            decimal current = (decimal)Math.Sqrt((double)x);
            for (int i = 0; i < 10; i++)
            {
                current = (current + x / current) / 2;
            }
            return current;
        }
    }

    /// <summary>
    /// Represents detailed timing analysis with percentiles.
    /// </summary>
    public readonly record struct DetailedTimingAnalysis(
        decimal Minimum,
        decimal Maximum,
        decimal Average,
        decimal StandardDeviation,
        decimal Percentile50,
        decimal Percentile90,
        decimal Percentile95,
        decimal Percentile99,
        IReadOnlyList<decimal> Measurements);

    /// <summary>
    /// Provides extension methods for <see cref="DecimalUltraHighPrecisionTimer"/>.
    /// </summary>
    public static class DecimalUltraHighPrecisionTimerExtensions
    {
        /// <summary>
        /// Measures the execution time of an asynchronous action with picosecond precision.
        /// </summary>
        public static async Task<decimal> MeasureExecutionTimeAsync(
            this DecimalUltraHighPrecisionTimer timer,
            Func<Task> asyncAction)
        {
            long start = timer.GetTimestamp();
            await asyncAction();
            long end = timer.GetTimestamp();
            return timer.GetElapsedPicoseconds(start, end);
        }

        /// <summary>
        /// Measures the execution time of an asynchronous function with picosecond precision.
        /// </summary>
        public static async Task<(T Result, decimal ElapsedPicoseconds)>
            MeasureExecutionTimeAsync<T>(
                this DecimalUltraHighPrecisionTimer timer,
                Func<Task<T>> asyncFunction)
        {
            long start = timer.GetTimestamp();
            T result = await asyncFunction();
            long end = timer.GetTimestamp();
            return (result, timer.GetElapsedPicoseconds(start, end));
        }

        /// <summary>
        /// Creates a high precision delay with exact timing control.
        /// </summary>
        public static async Task HighPrecisionDelay(
            this DecimalUltraHighPrecisionTimer timer,
            decimal delayPicoseconds)
        {
            if (delayPicoseconds <= 0)
                return;

            long start = timer.GetTimestamp();
            decimal targetElapsed = 0m;

            while (targetElapsed < delayPicoseconds)
            {
                // Use small increments to avoid overshoot
                decimal remaining = delayPicoseconds - targetElapsed;

                if (remaining > 1_000_000_000m) // > 1ms
                {
                    await Task.Delay(1);
                }
                else if (remaining > 100_000m) // > 100ns
                {
                    Thread.SpinWait(100);
                }
                else
                {
                    Thread.SpinWait(1);
                }

                targetElapsed = timer.GetElapsedPicoseconds(start);
            }
        }
    }
}
