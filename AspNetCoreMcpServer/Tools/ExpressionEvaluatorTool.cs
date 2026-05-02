using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class ExpressionEvaluatorTool
{
    private static readonly ScriptOptions ScriptOptions = ScriptOptions.Default
        .AddImports("System", "System.Linq", "System.Collections.Generic")
        .AddReferences(typeof(System.Linq.Enumerable).Assembly);

    [McpServerTool(Name = "evaluate_csharp_expression"), Description("""
        Evaluate a C# expression using Roslyn scripting.
        Useful for calculations, algorithmic tasks, and data transformations.
        Examples: "1+1", "Math.Sqrt(16)", "new int[]{1,2,3}.Sum()", "\"hello\".ToUpper()"
        """)]
    public async Task<string> Evaluate(
        [Description("C# expression to evaluate")] string expression,
        [Description("Timeout in seconds (default: 5)")] int timeoutSeconds = 5)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var result = await CSharpScript.EvaluateAsync(expression, ScriptOptions, cancellationToken: cts.Token);
            return result?.ToString() ?? "null";
        }
        catch (CompilationErrorException ex)
        {
            return $"Compilation error: {string.Join("; ", ex.Diagnostics.Select(d => d.GetMessage()))}";
        }
        catch (OperationCanceledException)
        {
            return $"Evaluation timed out after {timeoutSeconds} seconds";
        }
        catch (Exception ex)
        {
            return $"Runtime error: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
