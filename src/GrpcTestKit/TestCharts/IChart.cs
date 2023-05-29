using System.Collections.Generic;

namespace TestCharts;

public interface IChart
{
    void ApplyInstallParameters(IList<string> parameters);
}
