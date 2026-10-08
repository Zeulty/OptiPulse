using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChlorideTweaks.Services
{
    public static class GameLauncherService
    {
        private static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiPulse");
        private static readonly string GamesFilePath = Path.Combine(DataDirectory, "games.json");

        private static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }
        }

        public static List<string> GetGames()
        {
            try
            {
                if (File.Exists(GamesFilePath))
                {
                    string json = File.ReadAllText(GamesFilePath);
                    return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading games: {ex.Message}");
            }
            return new List<string>();
        }

        public static void AddGame(string path)
        {
            try
            {
                EnsureDirectoryExists();
                var games = GetGames();
                if (!games.Contains(path))
                {
                    games.Add(path);
                    File.WriteAllText(GamesFilePath, JsonSerializer.Serialize(games));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding game: {ex.Message}");
            }
        }

        public static void RemoveGame(string path)
        {
            try
            {
                EnsureDirectoryExists();
                var games = GetGames();
                if (games.Contains(path))
                {
                    games.Remove(path);
                    File.WriteAllText(GamesFilePath, JsonSerializer.Serialize(games));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing game: {ex.Message}");
            }
        }

        public static async Task LaunchGameAsync(string path)
        {
            try
            {
                Process? gameProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });

                if (gameProcess == null)
                {
                    return;
                }

                // Kill explorer.exe
                Process[] explorers = Process.GetProcessesByName("explorer");
                foreach (var explorer in explorers)
                {
                    try
                    {
                        explorer.Kill();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to kill explorer: {ex.Message}");
                    }
                }

                // Loop every 5 minutes while the game is running
                while (!gameProcess.HasExited)
                {
                    try
                    {
                        GhostToolsService.ReduceMemory();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error running ReduceMemory: {ex.Message}");
                    }

                    // Wait for 5 minutes, checking periodically if the game has exited
                    for (int i = 0; i < 300; i++)
                    {
                        if (gameProcess.HasExited)
                            break;
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching game: {ex.Message}");
            }
            finally
            {
                // Restart explorer.exe
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to restart explorer: {ex.Message}");
                }
            }
        }
    }
}
