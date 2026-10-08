using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChlorideTweaks.Models;
using Microsoft.Win32;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// Executes tweaks entirely in memory: registry modifications are applied
    /// directly with Microsoft.Win32.RegistryKey, and raw command strings run
    /// through cmd.exe. No .bat or .reg file is ever written to disk, so the
    /// tweak payloads cannot be intercepted or leaked from the file system.
    /// </summary>
    public static class TweakRunner
    {
        public sealed record RunResult(bool Ok, string Output);

        /// <summary>Applies (apply = true) or reverts a tweak.</summary>
        public static async Task<RunResult> RunAsync(Tweak tweak, bool apply)
        {
            var registry = (apply ? tweak.ApplyRegistry : tweak.RevertRegistry) ?? Array.Empty<RegistryChange>();
            try
            {
                foreach (RegistryChange change in registry)
                    ApplyRegistryChange(change);
            }
            catch (Exception ex)
            {
                return new RunResult(false, ex.Message);
            }

            IReadOnlyList<string> commands =
                (apply ? tweak.ApplyCommands : tweak.RevertCommands) ?? Array.Empty<string>();
            if (commands.Count == 0)
                return new RunResult(true, "");

            return await RunCommandsAsync(commands, tweak.Interactive);
        }

        /// <summary>
        /// Applies registry modifications directly with RegistryKey. Public
        /// entry for callers without a Tweak instance, e.g. the USB buffer
        /// sliders.
        /// </summary>
        public static Task<RunResult> RunRegistryAsync(IReadOnlyList<RegistryChange> changes)
        {
            try
            {
                foreach (RegistryChange change in changes)
                    ApplyRegistryChange(change);
                return Task.FromResult(new RunResult(true, ""));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new RunResult(false, ex.Message));
            }
        }

        /// <summary>Writes one registry modification with RegistryKey.</summary>
        private static void ApplyRegistryChange(RegistryChange change)
        {
            using RegistryKey hive = change.Hive switch
            {
                RegistryHive.ClassesRoot => Registry.ClassesRoot,
                RegistryHive.CurrentUser => Registry.CurrentUser,
                RegistryHive.LocalMachine => Registry.LocalMachine,
                RegistryHive.Users => Registry.Users,
                RegistryHive.CurrentConfig => Registry.CurrentConfig,
                _ => throw new NotSupportedException($"Unsupported registry hive: {change.Hive}")
            };

            if (change.DeleteKey)
            {
                hive.DeleteSubKeyTree(change.KeyPath, throwOnMissingSubKey: false);
                return;
            }

            using RegistryKey key = hive.CreateSubKey(change.KeyPath, writable: true)
                ?? throw new InvalidOperationException($"Cannot open registry key {change.KeyPath}.");

            if (change.DeleteValue)
            {
                key.DeleteValue(change.ValueName ?? "", throwOnMissingValue: false);
                return;
            }

            object value = change.Value is string text
                ? text.Replace("{APPEXE}", Environment.ProcessPath ?? "")
                : change.Value!;

            key.SetValue(change.ValueName ?? "", value, change.Kind);
        }

        /// <summary>
        /// Runs raw command strings in memory through cmd.exe. Regular commands
        /// run hidden with their output captured; interactive commands open a
        /// visible console window (delayed expansion enabled for the picker
        /// script) and are not waited on. Public entry for callers without a
        /// Tweak instance, e.g. the RAM profile slider.
        /// </summary>
        public static async Task<RunResult> RunCommandsAsync(IReadOnlyList<string> commands, bool interactive)
        {
            if (interactive)
            {
                foreach (string command in commands)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/v:on /c \"" + command + "\"",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        return new RunResult(false, ex.Message);
                    }
                }
                return new RunResult(true, "");
            }

            var output = new StringBuilder();
            for (int i = 0; i < commands.Count; i++)
            {
                bool last = i == commands.Count - 1;
                try
                {
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c \"" + commands[i] + "\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };
                    process.Start();

                    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderrTask = process.StandardError.ReadToEndAsync();

                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { process.Kill(entireProcessTree: true); } catch { }
                        return new RunResult(false, "Timed out after 10 minutes.");
                    }

                    string stdout = await stdoutTask;
                    string stderr = await stderrTask;

                    // Failures before the final command are tolerated, matching
                    // the original script behavior (mid-script errors never
                    // failed the whole script); only the last command decides.
                    if (process.ExitCode != 0)
                    {
                        string detail = (stderr.Trim() + " " + stdout.Trim()).Trim();
                        if (detail.Length > 0) output.AppendLine(detail);
                        if (last)
                            return new RunResult(false, output.Length > 0
                                ? output.ToString()
                                : $"The command exited with code {process.ExitCode}.");
                    }
                }
                catch (Exception ex)
                {
                    return new RunResult(false, ex.Message);
                }
            }

            return new RunResult(true, output.ToString());
        }
    }
}
