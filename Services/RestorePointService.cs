using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// Creates Windows system restore points. Uses PowerShell's
    /// Enable-ComputerRestore + Checkpoint-Computer (the app already runs
    /// elevated via its manifest, so no extra UAC prompt appears).
    /// </summary>
    public static class RestorePointService
    {
        private const string Script =
            "try { Enable-ComputerRestore -Drive ($env:systemdrive + '\\') -ErrorAction SilentlyContinue } catch { };" +
            "Checkpoint-Computer -Description 'OptiPulse tweak backup' -RestorePointType MODIFY_SETTINGS";

        /// <summary>Creates a restore point. Returns (success, error text).</summary>
        public static async Task<(bool Ok, string Error)> CreateRestorePointAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{Script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                    return (false, "Could not start PowerShell.");

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return (false, "Timed out while creating the restore point.");
                }

                if (process.ExitCode == 0)
                    return (true, "");

                string stderr = (await process.StandardError.ReadToEndAsync()).Trim();
                string stdout = (await process.StandardOutput.ReadToEndAsync()).Trim();
                string error = stderr.Length > 0 ? stderr : stdout;
                if (error.Length == 0)
                    error = LocalizationService.L("RestoreGenericFail");
                return (false, error);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
