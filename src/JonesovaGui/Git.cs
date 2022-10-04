using CliWrap.Exceptions;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace JonesovaGui
{
    public enum LogLevel
    {
        Error,
        Warning,
        Info,
        Debug
    }

    public partial class MainWindow
    {
        class Git
        {
            private readonly MainWindow window;
            private readonly GitCli git;
            private bool pushing;
            private Action<LogLevel, string> gitCliHandler;

            public Git(MainWindow window)
            {
                this.window = window;

                git = new GitCli(new DirectoryInfo(window.repoPath),
                    (logLevel, line) => gitCliHandler?.Invoke(logLevel, line));
                ConfigCli(git);

                window.loginButton.Click += LoginButton_Click;
                window.backupButton.Click += BackupButton_Click;
                window.restoreButton.Click += RestoreButton_Click;
                window.publishButton.Click += PublishButton_Click;
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

            private async Task<bool> TryUpdateAsync()
            {
                var token = window.tokenBox.Text;
                if (string.IsNullOrWhiteSpace(token)) return false;

                Directory.CreateDirectory(window.repoPath);
                try
                {
                    if (!git.IsValidRepository())
                    {
                        // Clone repository.
                        Log.Warn("Git", $"Invalid repo at {window.repoPath}; will clone");
                        Directory.Delete(window.repoPath, recursive: true);
                        if (await CloneAsync(token))
                        {
                            Log.Info("Git", $"Cloned at {window.repoPath}");
                        }
                    }
                    else
                    {
                        // Pull repository.
                        Log.Info("Git", $"Repo at {window.repoPath} valid; will pull");
                        if (!await PullAsync(token)) return false;
                    }
                }
                catch (CommandExecutionException e)
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

            private static string GetRemoteUrl(string token)
            {
                return $"https://jjonescz:{token}@github.com/jjonescz/jjonesova";
            }

            private async Task<bool> CloneAsync(string token)
            {
                return await GitCommandWithProgressAsync("Clone", "Přihlašování",
                    async () => await git.CloneAsync(GetRemoteUrl(token)));
            }

            private async Task<bool> PullAsync(string token)
            {
                return await GitCommandWithProgressAsync("Clone", "Přihlašování",
                    async () =>
                    {
                        await git.SetRemoteUrlAsync(GetRemoteUrl(token));
                        await git.PullAsync();
                    });
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
                var result = MessageBox.Show(window,
                    "Změny od předchozí zálohy (nebo předchozího zveřejnění) budou zahozeny.",
                    "Obnovit předchozí zálohu",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel);
                if (result == MessageBoxResult.OK)
                {
                    Log.Info("Git", "Resetting repository");
                    await git.ResetAsync();
                    await git.CleanAsync();
                    Log.Debug("Git", "Repository reset");

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
                await git.AddAllAsync();
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
