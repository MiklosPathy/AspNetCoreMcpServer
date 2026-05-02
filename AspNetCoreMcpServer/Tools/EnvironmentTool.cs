using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class EnvironmentTool
{
    [McpServerTool(Name = "get_environment_info"), Description("Get runtime and OS environment information.")]
    public static string GetEnvironmentInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"OS Description: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"OS Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Machine Name: {Environment.MachineName}");
        sb.AppendLine($"User Name: {Environment.UserName}");
        sb.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
        sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
        sb.AppendLine($"Tick Count: {Environment.TickCount64} ms");
        sb.AppendLine($".NET Version: {Environment.Version}");
        return sb.ToString();
    }

    [McpServerTool(Name = "get_environment_variable"), Description("Get a specific environment variable by name.")]
    public static string? GetEnvironmentVariable(
        [Description("Variable name")] string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }

    [McpServerTool(Name = "list_environment_variables"), Description("List all environment variables.")]
    public static string[] ListEnvironmentVariables()
    {
        return Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Select(e => $"{e.Key}={e.Value}")
            .OrderBy(s => s)
            .ToArray();
    }
}
