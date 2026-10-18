using System.ComponentModel;
using System.Diagnostics;

namespace ArginTaxPayer.IdentityOperator;

public static class TerminalQrCode
{
    public static async Task<bool> TryPrintAsync(string provisioningUri)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "qrencode",
                Arguments = "-t ANSIUTF8 -o -",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null) return false;

            await process.StandardInput.WriteAsync(provisioningUri);
            process.StandardInput.Close();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                _ = error;
                return false;
            }

            Console.WriteLine(output);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
