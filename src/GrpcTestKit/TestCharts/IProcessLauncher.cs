using System.Collections.Generic;
using System.Threading;

namespace TestHelmCharts;

public interface IProcessLauncher
{
    IAsyncEnumerable<string> Execute(string command, string parameters, CancellationToken token);
}
