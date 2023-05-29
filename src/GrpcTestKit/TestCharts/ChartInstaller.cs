using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestCharts;

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
    /// <returns></returns>
    public async Task<Release> Install(IChart chart, string releaseName, object overrides, TimeSpan? timeout = null)
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

        var serializedOverrides = JsonConvert.SerializeObject(overrides);
        var overridesPath = Path.Combine(Path.GetTempPath(), $"{releaseName}.json");
        File.WriteAllText(overridesPath, serializedOverrides);
        parameters.Add($"-f \"{overridesPath}\"");

        await _processLauncher.ExecuteToEnd("helm", $"upgrade {releaseName} {string.Join(" ", parameters)}", default);
        File.Delete(overridesPath);
        return new Release(releaseName, _processLauncher);
    }
}
