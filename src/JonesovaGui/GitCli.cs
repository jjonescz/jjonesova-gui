using CliWrap;
using System;
using System.IO;
using System.Threading.Tasks;

namespace JonesovaGui
{
    class GitCli
    {
        private readonly CommandRunner runner = new SynchronizedCommandRunner();

        public GitCli(Uri remoteUrl, DirectoryInfo localDirectory)
        {
            RemoteUrl = remoteUrl;
            LocalDirectory = localDirectory;

            GitCommand = Cli.Wrap("git")
                .WithWorkingDirectory(LocalDirectory.FullName);
        }

        public Uri RemoteUrl { get; }
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

        public async Task CloneAsync()
        {
            await GitCommand
                .AddArguments("clone", RemoteUrl.ToString(), LocalDirectory.Name)
                .WithWorkingDirectory(LocalDirectory.Parent.FullName)
                .RunAsync(runner);
        }

        public async Task PullAsync()
        {
            await GitCommand.AddArguments("pull", "--ff-only").RunAsync(runner);
        }

        public async Task CloneOrPullAsync(bool replace = false)
        {
            if (Directory.Exists(Path.Join(LocalDirectory.FullName, ".git")))
            {
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

                await CloneAsync();
            }
        }

        public async Task AddAllAsync()
        {
            await GitCommand.AddArguments("add", "-A").RunAsync(runner);
        }

        public async Task<bool> HasStagedChanges()
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

        public async Task CommitAsync(string message)
        {
            await GitCommand
                .AddArguments("commit", "-m", message)
                .RunAsync(runner);
        }

        public async Task PushAsync()
        {
            await GitCommand.AddArguments("push").RunAsync(runner);
        }
    }
}
