using CliWrap.Exceptions;
using LibGit2Sharp;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace JonesovaGui
{
    public partial class MainWindow
    {
        class Git
        {
            private const string repoUrl = "https://github.com/jjonescz/jjonesova";
            private readonly string configPath;
            private readonly MainWindow window;
            private readonly GitCli git;
            private Repository repo;
            private bool pushing;
            private Action<LogLevel, string> gitCliHandler;

            public Git(MainWindow window)
            {
                this.window = window;

                git = new GitCli(new Uri(repoUrl), new DirectoryInfo(window.repoPath),
                    (logLevel, line) => gitCliHandler?.Invoke(logLevel, line));
                ConfigCli(git);

                GlobalSettings.LogConfiguration = new LogConfiguration(LogLevel.Trace,
                    (level, message) => Log.Write(level, "Git", message));

                // Set configuration. We create separate file instead of storing
                // it in `.git/config` so that it takes effect even when pulling
                // the repository for the first time.
                configPath = Path.Combine(Log.RootPath, ".gitconfig");
                File.WriteAllText(configPath, null); // Create the file.
                using (var config = Configuration.BuildFrom(configPath))
                {
                    ConfigLib(config);
                }
                GlobalSettings.SetConfigSearchPaths(ConfigurationLevel.Global, Log.RootPath);

                window.loginButton.Click += LoginButton_Click;
                window.backupButton.Click += BackupButton_Click;
                window.restoreButton.Click += RestoreButton_Click;
                window.publishButton.Click += PublishButton_Click;
            }

            // IMPORTANT: Keep these two consistent.
            private static void ConfigLib(Configuration config)
            {
                config.Set("core.symlinks", false);
                config.Set("core.autocrlf", true);
                config.Set("core.longpaths", true);
            }
            private static void ConfigCli(GitCli git)
            {
                git.AddConfig("core.symlinks", "false");
                git.AddConfig("core.autocrlf", "true");
                git.AddConfig("core.longpaths", "true");
                git.AddUserName("Admin GUI");
                git.AddUserEmail("admin@jjonesova.cz");
            }

            public async Task UpdateAsync()
            {
                if (DesignerProperties.GetIsInDesignMode(window))
                {
                    window.loginStatus.Content = "Režim vývoje, přihlášení neproběhlo";
                    window.loginStatus.Foreground = Brushes.Purple;
                    window.tokenBox.Visibility = Visibility.Collapsed;
                    window.loginButton.Visibility = Visibility.Collapsed;
                }

                window.loginStatus.Content = "Přihlašování...";
                window.loginStatus.Foreground = Brushes.DarkOrange;
                window.tokenBox.Visibility = Visibility.Collapsed;
                window.loginButton.Visibility = Visibility.Collapsed;
                if (await TryUpdateAsync())
                {
                    window.loginStatus.Content = "Přihlášení úspěšné";
                    window.loginStatus.Foreground = Brushes.Black;
                    await window.InitAsync();
                }
                else
                {
                    window.loginStatus.Content = "Chyba; zadejte klíč:";
                    window.loginStatus.Foreground = Brushes.DarkRed;
                    window.tokenBox.Visibility = Visibility.Visible;
                    window.loginButton.Visibility = Visibility.Visible;
                }
            }

            public void Init()
            {
                repo = new Repository(window.repoPath);
            }

            private async Task<bool> TryUpdateAsync()
            {
                if (string.IsNullOrWhiteSpace(window.tokenBox.Text)) return false;

                Directory.CreateDirectory(window.repoPath);
                try
                {
                    if (!Repository.IsValid(window.repoPath))
                    {
                        // Clone repository.
                        Log.Warn("Git", $"Invalid repo at {window.repoPath}; will clone");
                        Directory.Delete(window.repoPath, recursive: true);
                        if (await CloneAsync())
                        {
                            Init();
                            Log.Info("Git", $"Cloned at {window.repoPath}");
                        }
                    }
                    else
                    {
                        // Pull repository.
                        Log.Info("Git", $"Repo at {window.repoPath} valid; will pull");
                        Init();
                        if (!await PullAsync()) return false;
                    }
                }
                catch (LibGit2SharpException e)
                {
                    Log.Error("Git", $"Update failed: {e}");
                    return false;
                }

                return true;
            }

            private async Task<bool> GitCommandWithProgressAsync(string name, string title, Func<Task> action)
            {
                Log.Debug("Git", $"{name} started");

                gitCliHandler = (level, line) =>
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Status(title, line);
                    }
                };
                try
                {
                    await Task.Run(action);
                }
                catch (CommandExecutionException e)
                {
                    Log.Error("Git", $"{name} failed: {e}");
                    return false;
                }
                finally
                {
                    gitCliHandler = null;
                }

                Log.Debug("Git", $"{name} finished");
                return true;
            }

            private async Task<bool> CloneAsync()
            {
                return await GitCommandWithProgressAsync("Clone", "Přihlašování",
                    async () => await git.CloneAsync());
            }

            private async Task<bool> PullAsync()
            {
                return await GitCommandWithProgressAsync("Clone", "Přihlašování",
                    async () => await git.PullAsync());
            }

            private async Task<bool> PushAsync()
            {
                if (!await GitCommandWithProgressAsync("Clone", "Nahrávání",
                    async () => await git.PushAsync()))
                {
                    window.loginStatus.Foreground = Brushes.DarkRed;
                    window.tokenBox.Visibility = Visibility.Collapsed;
                    return false;
                }
                return true;
            }

            private bool Status(string title, string progress)
            {
                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    var line = $"{title}: {progress}";
                    Log.Debug("Git", $"Progress: {line}");
                    window.loginStatus.Content = line;
                    window.loginStatus.Foreground = Brushes.DarkOrange;
                    window.tokenBox.Visibility = Visibility.Collapsed;
                });
                return true;
            }

            public async Task ResetAsync(bool bare = false)
            {
                var status = repo.RetrieveStatus();
                var changes = status.Except(status.Ignored).Count();
                var result = MessageBox.Show(window,
                    $"Změny v {changes} souborech od předchozí zálohy (nebo předchozího zveřejnění) budou zahozeny.",
                    "Obnovit předchozí zálohu",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel);
                if (result == MessageBoxResult.OK)
                {
                    Log.Info("Git", $"Resetting repository ({changes} changes)");
                    repo.Reset(ResetMode.Hard);
                    repo.RemoveUntrackedFiles();
                    Log.Debug("Git", $"Repository at commit {repo.Head.Tip.Sha}");

                    // Avoid updating UI when resetting the repository before
                    // app is completely loaded (i.e., after error occurred
                    // previously).
                    if (!bare)
                    {
                        await RefreshStatusAsync();
                        window.restoreButton.Header = "✔ Obnoveno";
                        window.data.Load();
                    }
                }
            }

            private void LoginButton_Click(object sender, RoutedEventArgs e)
            {
                _ = UpdateAsync();
            }

            private void BackupButton_Click(object sender, RoutedEventArgs e)
            {
                _ = CommitAsync();
            }

            private async Task CommitAsync()
            {
                Log.Info("Git", "Committing changes");
                Commands.Stage(repo, "*");
                await git.CommitAsync("Apply changes from admin GUI");
                Log.Debug("Git", "Committed");
                await RefreshStatusAsync();
                window.backupButton.Header = "✔ Zálohováno";
            }

            private void RestoreButton_Click(object sender, RoutedEventArgs e)
            {
                _ = ResetAsync();
            }

            private async void PublishButton_Click(object sender, RoutedEventArgs e)
            {
                // Backup (i.e., commit) first.
                if (window.backupButton.IsEnabled)
                {
                    await CommitAsync();
                }

                window.publishButton.IsEnabled = false;
                window.publishButton.Content = "⌛ Zveřejňování...";
                pushing = true;

                var result = await PushAsync();
                pushing = false;
                await RefreshStatusAsync();
                if (result)
                {
                    window.loginStatus.Content = "Nahrání úspěšné";
                    window.loginStatus.Foreground = Brushes.Black;
                    window.tokenBox.Visibility = Visibility.Collapsed;

                    window.publishButton.Content = "✔ Zveřejněno";

                    // Start checking deploy status.
                    window.deploy.Detect();
                }
            }

            public async Task RefreshStatusAsync()
            {
                // Note that all these buttons are enabled only if changes are saved.
                // IMPORTANT: Keep consistent with `Data.Changed`.

                var hasChanges = await git.HasChangesAsync();
                var dirty = hasChanges && !window.saveButton.IsEnabled;
                if (dirty && !window.restoreButton.IsEnabled)
                {
                    window.restoreButton.Header = "Obnovit předchozí zálohu...";
                }
                window.restoreButton.IsEnabled = dirty;
                if (dirty && !window.backupButton.IsEnabled)
                {
                    window.backupButton.Header = "Zálohovat";
                }
                window.backupButton.IsEnabled = dirty;

                if (!pushing)
                {
                    var pushDirty = dirty ||
                        (await git.AheadByAsync() > 0 && !window.saveButton.IsEnabled);
                    if (pushDirty && !window.publishButton.IsEnabled)
                    {
                        window.publishButton.Content = "Zveřejnit";
                    }
                    window.publishButton.IsEnabled = pushDirty;
                }

                Log.Debug("Git", $"Has changes: {hasChanges}");

                // TODO: Indicate changed files in UI.
#if false
                foreach (var album in window.data.Albums)
                {
                    album.Changed = false;
                    foreach (var image in album.Info.Resources)
                        image.Changed = false;
                }
                foreach (var entry in status)
                {
                    var fullPath = Path.GetFullPath(entry.FilePath, basePath: window.repoPath);
                    foreach (var album in window.data.Albums)
                    {
                        album.Changed |= fullPath.Equals(album.IndexPath, StringComparison.OrdinalIgnoreCase);

                        foreach (var image in album.Info.Resources)
                        {
                            image.Changed |= fullPath.Equals(image.FullPath, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }          
                window.albums.Items.Refresh();
                window.images.Items.Refresh();
#endif
            }
        }
    }
}
