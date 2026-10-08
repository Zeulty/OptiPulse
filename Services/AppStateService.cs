using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// Persisted app state: whether OptiPulse already created a system restore
    /// point on this machine, and which tweaks are currently applied.
    /// Stored in %AppData%\OptiPulse\state.json.
    /// Excluded from obfuscation so the persisted JSON keys stay stable.
    /// </summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class AppState
    {
        public bool RestorePointCreated { get; set; }
        public List<string> EnabledTweaks { get; set; } = new();
        public string Language { get; set; } = "en";

        /// <summary>Saved UI accent color ("#RRGGBB"); null/empty = default theme.</summary>
        public string? AccentColor { get; set; }

        /// <summary>Whether the background video is paused (static frame mode for low-end PCs).</summary>
        public bool VideoPaused { get; set; }

        /// <summary>Drives (e.g. "C:\") where the CompactOS disk compression tweak is currently applied.</summary>
        public List<string> CompressedDrives { get; set; } = new();
    }

    public static class AppStateService
    {
        private static readonly object _lock = new();
        private static AppState? _state;

        public static string StoreDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OptiPulse");

        private static string StorePath => Path.Combine(StoreDirectory, "state.json");

        public static AppState State
        {
            get { lock (_lock) { return _state ??= Load(); } }
        }

        public static bool IsTweakEnabled(string id)
        {
            lock (_lock) { return State.EnabledTweaks.Contains(id); }
        }

        public static void SetTweakEnabled(string id, bool enabled)
        {
            lock (_lock)
            {
                if (enabled)
                {
                    if (!State.EnabledTweaks.Contains(id))
                        State.EnabledTweaks.Add(id);
                }
                else
                {
                    State.EnabledTweaks.Remove(id);
                }
                Save();
            }
        }

        public static void MarkRestorePointCreated()
        {
            lock (_lock)
            {
                State.RestorePointCreated = true;
                Save();
            }
        }

        public static void SetLanguage(string code)
        {
            lock (_lock)
            {
                State.Language = code;
                Save();
            }
        }

        public static void SetAccentColor(string hex)
        {
            lock (_lock)
            {
                State.AccentColor = hex;
                Save();
            }
        }

        public static void SetVideoPaused(bool paused)
        {
            lock (_lock)
            {
                State.VideoPaused = paused;
                Save();
            }
        }

        public static List<string> GetCompressedDrives()
        {
            lock (_lock)
            {
                return new List<string>(State.CompressedDrives ?? new List<string>());
            }
        }

        public static void SetCompressedDrives(IEnumerable<string> drives)
        {
            lock (_lock)
            {
                State.CompressedDrives = new List<string>(drives);
                Save();
            }
        }

        private static AppState Load()
        {
            try
            {
                string path = StorePath;
                if (File.Exists(path))
                {
                    var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path));
                    if (state != null) return state;
                }
            }
            catch
            {
                // Corrupt/unreadable state -> start fresh rather than crash the app
            }
            return new AppState();
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(StoreDirectory);
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(StorePath, JsonSerializer.Serialize(State, options));
            }
            catch
            {
                // Best effort: losing the state file only forgets toggle states
            }
        }
    }
}
