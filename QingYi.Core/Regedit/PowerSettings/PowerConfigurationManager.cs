#if NET6_0_OR_GREATER && !BROWSER
#pragma warning disable CA1416
using System;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;

namespace QingYi.Core.Regedit.PowerSettings
{
    /// <summary>
    /// Power planning related class.<br />
    /// 电源计划相关的类。
    /// </summary>
    class PowerConfigurationManager
    {
        /// <summary>
        /// Starts a process with administrator privileges.<br />
        /// 以管理员权限启动进程。
        /// </summary>
        /// <param name="fileName">The name of the file to execute.</param>
        /// <param name="arguments">Arguments to pass to the executable.</param>
        /// <exception cref="InvalidOperationException">Thrown if the command execution fails.</exception>
        private static void StartProcessAsAdmin(string fileName, string arguments)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                Arguments = arguments,
                Verb = "runas", // 请求管理员权限
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using Process process = Process.Start(startInfo);
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"执行命令失败: {fileName} {arguments}", ex);
            }
        }

        /// <summary>
        /// Restores the default power schemes using PowerShell.<br />
        /// 使用PowerShell恢复默认电源方案。
        /// </summary>
        public static void RestoreDefaultSchemes() => StartProcessAsAdmin("powercfg.exe", "-restoredefaultschemes");

        /// <summary>
        /// Restores the High Performance power plan.<br />
        /// 恢复高性能电源计划。
        /// </summary>
        public static void RestoreHighPerformancePlan() => StartProcessAsAdmin("powercfg.exe", "-duplicatescheme 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

        /// <summary>
        /// Restores the Balanced power plan.<br />
        /// 恢复平衡电源计划。
        /// </summary>
        public static void RestoreBalancedPlan() => StartProcessAsAdmin("powercfg.exe", "-duplicatescheme 381b4222-f694-41f0-9685-ff5bb260df2e");

        /// <summary>
        /// Restores the Power Saver power plan.<br />
        /// 恢复省电电源计划。
        /// </summary>
        public static void RestorePowerSaverPlan() => StartProcessAsAdmin("powercfg.exe", "-duplicatescheme a1841308-3541-4fab-bc81-f71556f20b4a");

        /// <summary>
        /// Restores the Ultimate Performance power plan.<br />
        /// 恢复卓越性能电源计划。
        /// </summary>
        public static void RestoreUltimatePerformancePlan() => StartProcessAsAdmin("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");

        /// <summary>
        /// Sets the system to High Performance mode.<br />
        /// 设置系统为高性能模式。
        /// </summary>
        public static void SetHighPerformanceMode() => StartProcessAsAdmin("powercfg.exe", "/s SCHEME_MIN");

        /// <summary>
        /// Modifies the registry to disable Connected Standby.<br />
        /// 修改注册表以禁用Connected Standby。
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if access to the registry is denied or administrator privileges are required.</exception>
        public static void DisableConnectedStandby()
        {
            try
            {
                // 尝试修改CsEnabled
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power", true))
                {
                    if (key != null)
                    {
                        object csEnabledValue = key.GetValue("CsEnabled");
                        if (csEnabledValue != null)
                        {
                            key.SetValue("CsEnabled", 0, RegistryValueKind.DWord);
                            return;
                        }
                    }
                }

                // 如果CsEnabled不存在，添加PlatformAoAcOverride
                StartProcessAsAdmin("cmd.exe", "/c reg add HKLM\\System\\CurrentControlSet\\Control\\Power /v PlatformAoAcOverride /t REG_DWORD /d 0");
            }
            catch (SecurityException ex)
            {
                throw new InvalidOperationException("需要管理员权限修改注册表。", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException("访问注册表被拒绝。", ex);
            }
        }
    }
}
#endif
