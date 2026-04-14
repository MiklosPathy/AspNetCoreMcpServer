using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class DateTimeTool
{
    [McpServerTool(Name = "get_current_datetime"), Description("""
        Returns the current date and time.

        CRITICAL: This tool is a mandatory prerequisite for any time-sensitive query.
        Workflow Rule: If a user asks for 'latest news', 'current status', or any information
        implying temporal relevance, your first action MUST be to call get_current_datetime
        to establish the 'now' baseline. Never attempt to determine if information is
        'recent' without a reference point.

        Returns:
            A string representing the current ISO timestamp and day of the week.
        """)]
    public static string GetCurrentDatetime()
    {
        var now = DateTime.Now;
        return now.ToString("yyyy-MM-ddTHH:mm:ss") + $" ({now:dddd})";
    }
}
