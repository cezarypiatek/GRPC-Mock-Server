using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestHelmCharts;

public class ChartInstaller
{
    private readonly IProcessLauncher _processLauncher;

    public ChartInstaller(IProcessLauncher processLauncher)
    {
        _processLauncher = processLauncher;
    }

    /// <summary>
    ///     Perform helm chart installation
    /// </summary>
    /// <remarks>
    ///     Requires Helm and kubectl to be installed and added to PATH environment variable
    /// </remarks>
    /// <param name="chart"></param>
    /// <param name="releaseName"></param>
    /// <param name="overrides"></param>
    /// <param name="timeout"></param>
    public async Task<Release> Install(IChart chart, string releaseName, object? overrides = null, TimeSpan? timeout = null)
    {
        var executeToEnd = await _processLauncher.ExecuteToEnd("helm", $"list --filter {releaseName} -o json", default);
        if (executeToEnd != "[]")
        {
            await _processLauncher.ExecuteToEnd("helm", $"uninstall  {releaseName} --wait", default);
        }

        var parameters = new List<string>
        {
            "--install",
            "--force",
            "--atomic",
            "--wait"
        };

        if (timeout.HasValue)
        {
            parameters.Add($"--timeout {timeout.Value.TotalSeconds}s");
        }

        chart.ApplyInstallParameters(parameters);

        if (overrides != null)
        {
            var serializedOverrides = JsonConvert.SerializeObject(overrides);
            var overridesPath = Path.Combine(Path.GetTempPath(), $"{releaseName}.json");
            File.WriteAllText(overridesPath, serializedOverrides, Encoding.UTF8);
            parameters.Add($"-f \"{overridesPath}\"");    
        }

        await _processLauncher.ExecuteToEnd("helm", $"upgrade {releaseName} {string.Join(" ", parameters)}", default);
        return new Release(releaseName, _processLauncher);
    }
}
