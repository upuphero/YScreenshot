using Microsoft.Win32;

namespace YScreenshot.App
{
    /// <summary>
    /// Reads/writes the per-user "run at startup" registry entry backing the Settings
    /// dialog's startup toggle. HKCU only -- never touches machine-wide (HKLM) startup
    /// entries, and never requires elevation.
    /// </summary>
    public static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "YScreenshot";

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false))
            {
                return key?.GetValue(ValueName) != null;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
            {
                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue(ValueName, "\"" + exePath + "\"");
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
        }
    }
}
