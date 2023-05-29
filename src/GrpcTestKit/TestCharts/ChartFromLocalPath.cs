using System.Collections.Generic;

namespace TestCharts;

public class ChartFromLocalPath : IChart
{
    private readonly string _path;

    public ChartFromLocalPath(string path)
    {
        _path = path;
    }

    public void ApplyInstallParameters(IList<string> parameters)
    {
        parameters.Add(_path);
    }
}
