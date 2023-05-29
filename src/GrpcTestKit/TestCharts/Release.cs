using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TestHelmCharts;

public class Release : IAsyncDisposable
{
    public string DeploymentName { get; }
    private readonly IProcessLauncher _processExecutor;
    private readonly List<(Task, CancellationTokenSource)> _portForwards = new();

    public Release(string deploymentName, IProcessLauncher processExecutor)
    {
        DeploymentName = deploymentName;
        _processExecutor = processExecutor;
    }

    public async Task<int> StartPortForward(string serviceName, int servicePort, int? localPort = null)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var asyncEnumerable = _processExecutor.Execute("kubectl", $"port-forward service/{serviceName} {localPort}:{servicePort}", cancellationTokenSource.Token);

        var enumerator = asyncEnumerable.GetAsyncEnumerator(default);
        await enumerator.MoveNextAsync();
        if (enumerator.Current.StartsWith("Forwarding from"))
        {
            _portForwards.Add((ReadToEnd(enumerator), cancellationTokenSource));
            return ExtractPortNumber(enumerator.Current);
        }
        
        await ReadToEnd(enumerator);
        return 0;
    }

    private static int ExtractPortNumber(string input)
    {
        var pattern = @":(\d+) ->";
        var match = Regex.Match(input, pattern);

        if (match.Success)
        {
            var portNumber = match.Groups[1].Value;
            return int.Parse(portNumber);
        }

        throw new ArgumentException("Invalid input. No port number found.");
    }

    private async Task ReadToEnd(IAsyncEnumerator<string> enumerator)
    {
        while (await enumerator.MoveNextAsync()){}
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var (_, cts) in _portForwards)
            {
                cts.Cancel();
            }

            await Task.WhenAll(_portForwards.Select(x => x.Item1).ToArray());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        await _processExecutor.ExecuteToEnd("helm", $"uninstall {DeploymentName}", default);
    }
}
