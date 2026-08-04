using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace YScreenshot.App
{
    /// <summary>
    /// Hotkey bindings and strip position, persisted to
    /// %AppData%\YScreenshot\settings.json. Persistence is best-effort: a failed
    /// read/write falls back to defaults rather than crashing the app, since settings
    /// are a convenience, not something the app depends on to function.
    /// </summary>
    [DataContract]
    public sealed class AppSettings
    {
        private const int UnsetCoordinate = int.MinValue;

        [DataMember] public int StripX { get; set; } = UnsetCoordinate;
        [DataMember] public int StripY { get; set; } = UnsetCoordinate;

        [DataMember] public string FullScreenHotkey { get; set; } = "PrintScreen";
        [DataMember] public string RegionHotkey { get; set; } = "Ctrl+Shift+A";
        [DataMember] public string ScrollingHotkey { get; set; } = "Ctrl+Shift+S";

        [DataMember] public bool StartWithWindows { get; set; }

        /// <summary>
        /// One of "Toast", "TrayBalloon", "None". Stored as a plain string (like the
        /// hotkey fields) rather than an enum type, so the settings file format doesn't
        /// depend on how DataContractJsonSerializer happens to represent enums.
        /// </summary>
        [DataMember] public string FeedbackStyle { get; set; } = "Toast";

        public bool HasStoredPosition => StripX != UnsetCoordinate && StripY != UnsetCoordinate;

        private static string SettingsDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YScreenshot");

        private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new AppSettings();
                }

                using (var stream = File.OpenRead(SettingsPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    return (AppSettings)serializer.ReadObject(stream) ?? new AppSettings();
                }
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);

                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    serializer.WriteObject(stream, this);
                    File.WriteAllBytes(SettingsPath, stream.ToArray());
                }
            }
            catch
            {
                // Best-effort; losing the strip's saved position is not worth crashing over.
            }
        }
    }
}
