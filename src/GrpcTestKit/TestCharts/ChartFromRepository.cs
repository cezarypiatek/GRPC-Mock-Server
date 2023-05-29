using System.Collections.Generic;

namespace TestHelmCharts;

public class ChartFromRepository : IChart
{
    private readonly HelmRepository _repository;
    private readonly string _chartName;
    private readonly string? _version;

    public ChartFromRepository(HelmRepository repository, string chartName, string? version)
    {
        _repository = repository;
        _chartName = chartName;
        _version = version;
    }

    public void ApplyInstallParameters(IList<string> parameters)
    {
        parameters.Add($"--repo {_repository.Url}");
        parameters.Add($"--username \"{_repository.Login}\"");
        parameters.Add($"--password \"{_repository.Password}\"");
        if (_version != null)
        {
            parameters.Add($"--version {_version}");
        }
        parameters.Add(_chartName);
    }
}
