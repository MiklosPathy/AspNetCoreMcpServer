using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class ProcessListTool
{
    [McpServerTool(Name = "list_processes"), Description("List running OS processes.")]
    public static string ListProcesses(
        [Description("Optional filter by process name")] string? nameFilter = null)
    {
        IEnumerable<Process> processes = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.ProcessName))
            .OrderBy(p => p.ProcessName);

        if (!string.IsNullOrEmpty(nameFilter))
            processes = processes.Where(p => p.ProcessName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));

        var lines = processes.Select(p =>
        {
            var memMb = p.WorkingSet64 / 1024 / 1024;
            return $"{p.Id,-8} {p.ProcessName,-35} {memMb,8} MB";
        });

        return string.Join("\n", lines);
    }
}
