using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class ShellTool
{
    [McpServerTool(Name = "execute_shell_command"), Description("""
        Execute a shell command via cmd /c.
        Returns exit code, stdout and stderr.
        WARNING: Use only for safe, read-only or build commands.
        """)]
    public async Task<string> Execute(
        [Description("Command to run (e.g. 'dotnet build', 'dir')")] string command,
        [Description("Working directory (default: current directory)")] string? workingDirectory = null,
        [Description("Timeout in seconds (default: 60)")] int timeoutSeconds = 60)
    {
        var psi = new ProcessStartInfo("cmd", $"/c {command}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var proc = Process.Start(psi)!;

        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(); } catch { /* ignore */ }
            throw new McpException($"Command timed out after {timeoutSeconds} seconds");
        }

        var stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token);
        var stderr = await proc.StandardError.ReadToEndAsync(cts.Token);

        return $"Exit code: {proc.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}";
    }
}
