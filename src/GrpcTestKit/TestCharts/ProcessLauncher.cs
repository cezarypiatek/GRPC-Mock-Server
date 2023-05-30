using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CliWrap;

namespace TestHelmCharts
{
    public class ProcessLauncher : IProcessLauncher
    {
        private readonly IProcessOutputWriter _processOutputWriter;

        public ProcessLauncher(IProcessOutputWriter? processOutputWriter = null)
        {
            _processOutputWriter = processOutputWriter ?? new ConsoleProcessOutputWriter();
        }

        public async IAsyncEnumerable<string> Execute(string command, string parameters, [EnumeratorCancellation] CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<string>();

            var actualJob = Task.Run(async () =>
            {
                var writer = channel.Writer;
                var errorOutput = new StringBuilder();
                try
                {
                    await Cli.Wrap(command)
                        .WithArguments(parameters)
                        .WithValidation(CommandResultValidation.ZeroExitCode)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(async s =>
                        {
                            await writer.WriteAsync(s, default);
                            _processOutputWriter.Write(s);
                        }))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(async s =>
                        {
                            await writer.WriteAsync(s, default);
                            errorOutput.Append(s);
                            _processOutputWriter.WriteError(s);
                        }))
                        .ExecuteAsync(token);
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException)
                    {
                        throw new InvalidOperationException($"Error while executing script '{command} {parameters}': {errorOutput}", e);
                    }
                }
                finally
                {
                    writer.Complete();
                }
            }, default);

            var reader = channel.Reader;

            while (await reader.WaitToReadAsync(default))
            {
                if (reader.TryRead(out var item))
                {
                    yield return item;
                }
            }

            await actualJob;
        }
    }
}
