using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestCharts;

public static class ProcessLauncherExtensions
{
    public static async Task<string> ExecuteToEnd(this IProcessLauncher @this, string command, string parameters, CancellationToken token)
    {
        var outputBuilder = new StringBuilder();
        await foreach (var line in @this.Execute(command, parameters, token))
        {
            outputBuilder.Append(line);
        }
        return outputBuilder.ToString();
    }
}
