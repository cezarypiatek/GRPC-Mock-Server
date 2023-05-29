using System;

namespace TestHelmCharts;

public interface IProcessOutputWriter
{
    void Write(string text);
    void WriteError(string text);
}

public class ConsoleProcessOutputWriter : IProcessOutputWriter
{
    public void Write(string text) => Console.WriteLine(text);

    public void WriteError(string text) => Console.Error.WriteLine(text);
}
