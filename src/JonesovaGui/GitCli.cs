using CliWrap;
using System;
using System.IO;
using System.Threading.Tasks;

namespace JonesovaGui
{
    class GitCli
    {
        private readonly CommandRunner runner;

        public GitCli(DirectoryInfo localDirectory, Action<LogLevel, string> handler)
        {
            LocalDirectory = localDirectory;
            this.runner = new SynchronizedCommandRunner(handler);

            var gitPath = Path.GetFullPath("Assets/git/cmd/git.exe");
            GitCommand = Cli.Wrap(gitPath)
                .WithEnvironmentVariables(e => e.Set("GIT_TERMINAL_PROMPT", "0"))
                .WithWorkingDirectory(LocalDirectory.FullName);
        }
        
        public DirectoryInfo LocalDirectory { get; }
        public Command GitCommand { get; set; }

        public void AddConfig(string key, string value)
        {
            GitCommand = GitCommand.AddArguments(new[] { "-c", $"{key}={value}" });
        }

        public void AddUserName(string userName)
        {
            AddConfig("user.name", userName);
        }

        public void AddUserEmail(string emailAddress)
        {
            AddConfig("user.email", emailAddress);
        }

        public void AddGpgSign(bool sign)
        {
            AddConfig("commit.gpgsign", sign ? "true" : "false");
        }

        public async Task CloneAsync(string remoteUrl)
        {
            await GitCommand
                .AddArguments(
                    "clone",
                    "--progress",
                    "--recurse-submodules",
                    "--depth=1",
                    "--shallow-submodules",
                    remoteUrl,
                    LocalDirectory.Name)
                .WithWorkingDirectory(LocalDirectory.Parent.FullName)
                .RunAsync(runner);
        }

        public async Task SetRemoteUrlAsync(string remoteUrl)
        {
            await GitCommand.AddArguments("remote", "set-url", "origin", remoteUrl).RunAsync(runner);
        }

        public async Task PullAsync()
        {
            await GitCommand.AddArguments("pull", "--ff-only", "--progress").RunAsync(runner);
        }

        public bool IsValidRepository()
        {
            return Directory.Exists(Path.Join(LocalDirectory.FullName, ".git"));
        }

        public async Task CloneOrPullAsync(string remoteUrl, bool replace = false)
        {
            if (IsValidRepository())
            {
                await SetRemoteUrlAsync(remoteUrl);
                await PullAsync();
            }
            else
            {
                if (replace)
                {
                    LocalDirectory.Refresh();
                    if (LocalDirectory.Exists)
                    {
                        try
                        {
                            LocalDirectory.Delete(recursive: true);
                        }
                        catch (DirectoryNotFoundException) { }
                        LocalDirectory.Refresh();
                    }
                }

                await CloneAsync(remoteUrl);
            }
        }

        public async Task AddAllAsync()
        {
            await GitCommand.AddArguments("add", "-A").RunAsync(runner);
        }

        public async Task<bool> HasChangesAsync()
        {
            var result = await GitCommand
                .AddArguments("diff-index", "--quiet", "HEAD")
                .WithValidation(CommandResultValidation.None)
                .RunAsync(runner);
            return result.ExitCode switch
            {
                1 => true,
                0 => false,
                _ => throw new InvalidOperationException($"Unrecognized exit code {result.ExitCode}")
            };
        }

        public async Task<int> AheadByAsync()
        {
            var numLines = 0;
            await GitCommand
                .AddArguments("log", "--oneline", "@{u}..")
                .WithStandardOutputPipe(PipeTarget.ToDelegate(_ => numLines++))
                .RunAsync(runner, noInterceptPipes: true);
            return numLines;
        }

        public async Task CommitAsync(string message)
        {
            await GitCommand
                .AddArguments("commit", "-m", message)
                .RunAsync(runner);
        }

        public async Task PushAsync()
        {
            await GitCommand.AddArguments("push", "--progress").RunAsync(runner);
        }

        public async Task ResetAsync()
        {
            await GitCommand.AddArguments("reset", "--hard").RunAsync(runner);
        }

        public async Task CleanAsync()
        {
            await GitCommand.AddArguments("clean", "-fd").RunAsync(runner);
        }
    }
}
