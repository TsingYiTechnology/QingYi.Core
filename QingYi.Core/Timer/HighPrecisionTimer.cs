using System;
using System.Diagnostics;

namespace QingYi.Core.Timer
{
    /// <summary>
    /// Provides high-precision timing function, supports two time units of millisecond and second measurement, and retains accuracy to two decimal places.<br />
    /// 提供高精度计时功能，支持毫秒和秒两种时间单位测量，精度保留至两位小数。
    /// </summary>
    /// <remarks>
    /// Based on the System. Diagnostics. Stopwatch realize timing accuracy depends on the hardware and operating System support. Before using recommends a Stopwatch. IsHighResolution inspection precision timing state support.<br />
    /// 基于System.Diagnostics.Stopwatch实现，计时精度取决于硬件和操作系统支持。使用前建议通过Stopwatch.IsHighResolution检查高精度计时支持状态。
    /// </remarks>
    public class HighPrecisionTimer
    {
        private readonly Stopwatch _stopwatch;

        /// <summary>
        /// Initializes a new high-precision timer instance.<br />
        /// 初始化一个新的高精度计时器实例。
        /// </summary>
        /// <remarks>
        /// The Stopwatch instance is automatically created, but the timing does not start immediately.<br />
        /// 会自动创建Stopwatch实例，但不会立即开始计时。
        /// </remarks>
        public HighPrecisionTimer() => _stopwatch = new Stopwatch();

        /// <summary>
        /// Start timer<br />
        /// 启动计时器
        /// </summary>
        /// <remarks>
        /// If the timer is already running, calling this method has no effect. Multiple starts will not reset the timer, keeping the original timing state.<br />
        /// 如果计时器已经在运行中，调用本方法不会产生任何效果。多次启动不会重置计时器，保持原有计时状态。
        /// </remarks>
        public void Start()
        {
            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
            }
        }

        /// <summary>
        /// Stop timer<br />
        /// 停止计时器
        /// </summary>
        /// <remarks>
        /// <br />
        /// 如果计时器已经停止，调用本方法不会产生任何效果。停止后可以通过Start方法继续累积计时。
        /// </remarks>
        public void Stop()
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
            }
        }

        /// <summary>
        /// Reset the timer to its initial state<br />
        /// 重置计时器到初始状态
        /// </summary>
        /// <remarks>
        /// The timing is stopped and the recorded time cleared regardless of whether it is currently running. After the reset, you need to manually call Start to restart the timing.<br />
        /// 无论当前是否在运行中，都会停止计时并清零已记录的时间。重置后需要手动调用Start重新开始计时。
        /// </remarks>
        public void Reset() => _stopwatch.Reset();

        /// <summary>
        /// Gets the number of milliseconds passed<br />
        /// 获取经过的毫秒数
        /// </summary>
        /// <returns>A time value of type decimal, with two decimal digits reserved.<br />decimal类型的时间值，保留两位小数。</returns>
        /// <remarks>
        /// The result is rounded to two decimal places. The timer runtime call returns the currently timed value without affecting the timing status.<br />
        /// 返回结果经过四舍五入处理，精度为两位小数。计时器运行时调用将返回当前已计时数值，不会影响计时状态。
        /// </remarks>
        public decimal GetElapsedMilliseconds() => Math.Round((decimal)_stopwatch.Elapsed.TotalMilliseconds, 4);

        /// <summary>
        /// Gets the number of seconds passed<br />
        /// 获取经过的秒数
        /// </summary>
        /// <returns>A time value of type decimal, with two decimal digits reserved.<br />decimal类型的时间值，保留两位小数。</returns>
        /// <remarks>
        /// The result is rounded to two decimal places. The timer runtime call returns the currently timed value without affecting the timing status.<br />
        /// 返回结果经过四舍五入处理，精度为两位小数。计时器运行时调用将返回当前已计时数值，不会影响计时状态。
        /// </remarks>
        public decimal GetElapsedSeconds() => Math.Round((decimal)_stopwatch.Elapsed.TotalSeconds, 4);

        /// <summary>
        /// Get the time value in both milliseconds and seconds<br />
        /// 同时获取毫秒和秒两种单位的时间值
        /// </summary>
        /// <returns>
        /// A tuple containing two decimal values: <br />
        /// · Milliseconds - milliseconds (keep two decimal places) <br />
        /// · Seconds - Number of seconds (two decimal places reserved) <br />
        /// 包含两个decimal值的元组：<br />
        /// · Milliseconds - 毫秒数（保留两位小数）<br />
        /// · Seconds - 秒数（保留两位小数）
        /// </returns>
        /// <remarks>
        /// The two return values are obtained at the same point in time to ensure data consistency. Applicable to scenarios where two time units need to be displayed at the same time.<br />
        /// 两个返回值基于同一时间点获取，保证数据一致性。适用于需要同时显示两种时间单位的场景。
        /// </remarks>
        public (decimal Milliseconds, decimal Seconds) GetBothElapsedTimes()
        {
            var ms = (decimal)_stopwatch.Elapsed.TotalMilliseconds;
            var sec = (decimal)_stopwatch.Elapsed.TotalSeconds;
            return (Math.Round(ms, 2), Math.Round(sec, 2));
        }
    }
}
