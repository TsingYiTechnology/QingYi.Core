using System.ComponentModel;
using System.Runtime.InteropServices;
using System;

namespace QingYi.Core.Battery
{
    /// <summary>
    /// Get power plan info.
    /// </summary>
    public class PowerPlanInfo
    {
        /// <summary>
        /// The type of the power plan
        /// </summary>
        public enum PowerPlanType
        {
            /// <summary>
            /// High performance mode
            /// </summary>
            HighPerformance,

            /// <summary>
            /// Balanced mode
            /// </summary>
            Balanced,

            /// <summary>
            /// Power saver mode
            /// </summary>
            PowerSaver,

            /// <summary>
            /// Unknown mode
            /// </summary>
            Unknown
        }

        #region Power API
#pragma warning disable SYSLIB1054
        [DllImport("powrprof.dll", CharSet = CharSet.Auto)]
        private static extern uint PowerGetActiveScheme(IntPtr userRoot, out IntPtr activePolicyGuid);

        [DllImport("powrprof.dll", CharSet = CharSet.Auto)]
        private static extern uint PowerReadFriendlyName(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subgroupOfPowerSettingsGuid,
            IntPtr powerSettingGuid,
            IntPtr buffer,
            ref uint bufferSize);

        // SystemParametersInfo for Battery Saver
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(
            uint uiAction,
            uint uiParam,
            ref bool pvParam,
            uint fWinIni);
#pragma warning restore SYSLIB1054
        #endregion

        #region GUIDs for default power plans
#pragma warning disable IDE0090
        private static readonly Guid HighPerformanceGuid = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static readonly Guid BalancedGuid = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        private static readonly Guid PowerSaverGuid = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
#pragma warning restore IDE0090
        #endregion

        #region Properties
        /// <summary>
        /// The current power plan type.
        /// </summary>
        public PowerPlanType PlanType { get; }

        /// <summary>
        /// The name of the power plan.
        /// </summary>
        public string PlanName { get; }

        /// <summary>
        /// The status of the battery save mode. <b>True</b> is enable, <b>False</b> is disable.
        /// </summary>
        public bool IsBatterySaverOn { get; }
        #endregion

        /// <summary>
        /// Get power plan info.
        /// </summary>
        public PowerPlanInfo()
        {
            PlanType = GetCurrentPowerPlanType();
            PlanName = GetCurrentPowerPlanName() ?? "Unknown";
            IsBatterySaverOn = GetBatterySaverStatus();
        }

        private static PowerPlanType GetCurrentPowerPlanType()
        {
            IntPtr activeGuidPtr = IntPtr.Zero;
            try
            {
                uint result = PowerGetActiveScheme(IntPtr.Zero, out activeGuidPtr);
                if (result != 0) return PowerPlanType.Unknown;

#if NET9_0_OR_GREATER
                Guid activeGuid = Marshal.PtrToStructure<Guid>(activeGuidPtr);
#else
                Guid activeGuid = (Guid)Marshal.PtrToStructure(activeGuidPtr, typeof(Guid));
#endif

                if (activeGuid == HighPerformanceGuid) return PowerPlanType.HighPerformance;
                if (activeGuid == BalancedGuid) return PowerPlanType.Balanced;
                return activeGuid == PowerSaverGuid ? PowerPlanType.PowerSaver : PowerPlanType.Unknown;
            }
            finally
            {
                if (activeGuidPtr != IntPtr.Zero) Marshal.FreeHGlobal(activeGuidPtr);
            }
        }

        private static string GetCurrentPowerPlanName()
        {
            IntPtr activeGuidPtr = IntPtr.Zero;
            try
            {
                uint result = PowerGetActiveScheme(IntPtr.Zero, out activeGuidPtr);
                if (result != 0) return null;

#if NET9_0_OR_GREATER
                Guid activeGuid = Marshal.PtrToStructure<Guid>(activeGuidPtr);
#else
                Guid activeGuid = (Guid)Marshal.PtrToStructure(activeGuidPtr, typeof(Guid));
#endif

                uint bufferSize = 0;
                result = PowerReadFriendlyName(
                    IntPtr.Zero,
                    ref activeGuid,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    ref bufferSize);

                if (result != 0 || bufferSize == 0) return null;

                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    result = PowerReadFriendlyName(
                        IntPtr.Zero,
                        ref activeGuid,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        buffer,
                        ref bufferSize);

                    return result == 0 ? Marshal.PtrToStringUni(buffer) : null;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                if (activeGuidPtr != IntPtr.Zero) Marshal.FreeHGlobal(activeGuidPtr);
            }
        }

        private static bool GetBatterySaverStatus()
        {
            try
            {
                const uint SPI_GETBATTERYSAVERSTATUS = 0x1045;
                bool isBatterySaverOn = false;

                if (!SystemParametersInfo(
                    SPI_GETBATTERYSAVERSTATUS,
                    0,
                    ref isBatterySaverOn,
                    0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return isBatterySaverOn;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the power plan info string.
        /// </summary>
        /// <returns>The power plan info string.</returns>
        public override string ToString()
        {
            return $"Power Plan: {PlanName} ({PlanType})\nBattery Saver: {IsBatterySaverOn}";
        }
    }
}
