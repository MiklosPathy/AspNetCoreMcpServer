using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class GitTool
{
    [McpServerTool(Name = "git_status"), Description("Get git status (modified, untracked files).")]
    public static string Status(
        [Description("Repository path (default: current directory)")] string? repositoryPath = null)
        => RunGit("status --short", repositoryPath);

    [McpServerTool(Name = "git_log"), Description("Get recent commit history.")]
    public static string Log(
        [Description("Repository path")] string? repositoryPath = null,
        [Description("Number of commits")] int count = 5)
        => RunGit($"log -n {count} --oneline", repositoryPath);

    [McpServerTool(Name = "git_diff"), Description("Get diff for working tree or specific file.")]
    public static string Diff(
        [Description("Repository path")] string? repositoryPath = null,
        [Description("File path or empty for all changes")] string? filePath = null)
        => RunGit(string.IsNullOrEmpty(filePath) ? "diff" : $"diff -- {filePath}", repositoryPath);

    [McpServerTool(Name = "git_branch"), Description("List branches.")]
    public static string Branch(
        [Description("Repository path")] string? repositoryPath = null)
        => RunGit("branch -a", repositoryPath);

    private static string RunGit(string args, string? workingDir)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory()
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        return proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
    }
}
