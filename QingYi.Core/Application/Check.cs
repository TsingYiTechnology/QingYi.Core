#if !BROWSER
using System;
using System.Diagnostics;

namespace QingYi.Core.Application
{
    /// <summary>
    /// Provides methods to check the status of running applications on the system.
    /// </summary>
    public static class Check
    {
        /// <summary>
        /// Checks if the specified application is running on the system.
        /// </summary>
        /// <param name="app">The name of the application to check (without the file extension).</param>
        /// <returns>Returns true if the application is running, otherwise false.</returns>
        public static bool CheckRunning(string app)
        {
            // Get all running processes and check if the specified process is found
            var processes = Process.GetProcessesByName(app);

            // Use relational pattern in C# 9.0 or higher
            if (processes.Length > 0)
            {
                Console.WriteLine($"{app}.exe is running.");
                return true;
            }
            else
            {
                Console.WriteLine($"{app}.exe is not running.");
                return false;
            }
        }
    }
}
#endif
