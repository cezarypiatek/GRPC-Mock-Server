using System.Collections.Generic;
using System.Threading;

namespace TestCharts;

public interface IProcessLauncher
{
    IAsyncEnumerable<string> Execute(string command, string parameters, CancellationToken token);
}
