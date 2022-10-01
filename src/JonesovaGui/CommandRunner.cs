using CliWrap;
using LibGit2Sharp;
using System.Threading;
using System.Threading.Tasks;

namespace JonesovaGui
{
    /// <summary>
    /// Wrapper around <see cref="Cli"/> that logs <see cref="Command"/> execution.
    /// </summary>
    class CommandRunner
    {
        private readonly PipeTarget standardOutput, standardError;

        public CommandRunner()
        {
            standardOutput = CreatePipeTarget(LogLevel.Info);
            standardError = CreatePipeTarget(LogLevel.Warning);

            PipeTarget CreatePipeTarget(LogLevel logLevel)
            {
                return PipeTarget.ToDelegate(line =>
                    Log.Write(logLevel, "Command", $"> {line}"));
            }
        }

        public virtual async Task<CommandResult> RunAsync(Command command)
        {
            Log.Info("Command", $"{command.WorkingDirPath}$ {command}");
            using var result = command
                .WithStandardOutputPipe(standardOutput)
                .WithStandardErrorPipe(standardError)
                .ExecuteAsync();
            return await result.Task;
        }
    }

    class SynchronizedCommandRunner : CommandRunner
    {
        private readonly SemaphoreSlim semaphore = new(1, 1);

        public override async Task<CommandResult> RunAsync(Command command)
        {
            try
            {
                await semaphore.WaitAsync();
                return await base.RunAsync(command);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
