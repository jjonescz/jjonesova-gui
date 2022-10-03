using CliWrap;
using LibGit2Sharp;
using System;
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

        public CommandRunner(Action<LogLevel, string> handler)
        {
            standardOutput = CreatePipeTarget(LogLevel.Info, handler);
            standardError = CreatePipeTarget(LogLevel.Warning, handler);

            static PipeTarget CreatePipeTarget(LogLevel logLevel, Action<LogLevel, string> handler)
            {
                return PipeTarget.ToDelegate(line =>
                {
                    Log.Write(logLevel, "Command", $"> {line}");
                    handler(logLevel, line);
                });
            }
        }

        public virtual async Task<CommandResult> RunAsync(Command command,
            bool noInterceptPipes = false)
        {
            Log.Info("Command", $"{command.WorkingDirPath}$ {command}");
            command = noInterceptPipes ? command : command
                .WithStandardOutputPipe(standardOutput)
                .WithStandardErrorPipe(standardError);
            using var result = command.ExecuteAsync();
            return await result.Task;
        }
    }

    class SynchronizedCommandRunner : CommandRunner
    {
        private readonly SemaphoreSlim semaphore = new(1, 1);

        public SynchronizedCommandRunner(Action<LogLevel, string> handler) : base(handler)
        {
        }

        public override async Task<CommandResult> RunAsync(Command command,
            bool noInterceptPipes = false)
        {
            try
            {
                await semaphore.WaitAsync();
                return await base.RunAsync(command, noInterceptPipes: noInterceptPipes);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
