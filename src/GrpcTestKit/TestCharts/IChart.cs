using System.Collections.Generic;

namespace TestHelmCharts;

public interface IChart
{
    void ApplyInstallParameters(IList<string> parameters);
}
