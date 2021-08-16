using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace JonesovaGui
{
    public partial class MainWindow
    {
        class Git
        {
            private readonly MainWindow window;
            private Repository repo;
            private bool pushing;

            public Git(MainWindow window)
            {
                this.window = window;

                GlobalSettings.LogConfiguration = new LogConfiguration(LogLevel.Trace,
                    (level, message) => Log.Write(level, "Git", message));

                window.backupButton.Click += BackupButton_Click;
                window.restoreButton.Click += RestoreButton_Click;
                window.publishButton.Click += PublishButton_Click;
            }

            public async Task<bool> UpdateAsync()
            {
                Directory.CreateDirectory(window.repoPath);
                try
                {
                    if (!Repository.IsValid(window.repoPath))
                    {
                        // Clone repository.
                        Log.Warn("Git", $"Invalid repo at {window.repoPath}; will clone");
                        Directory.Delete(window.repoPath, recursive: true);
                        await Task.Run(() => Repository.Clone("https://github.com/jjonescz/jjonesova", window.repoPath, new CloneOptions
                        {
                            CredentialsProvider = Credentials,
                            OnProgress = p => Status("Přihlašování", p),
                            OnTransferProgress = p => Status("Přihlašování", p),
                            OnCheckoutProgress = (p, c, t) => Status("Přihlašování", $"Rozbalování: {(double)c / t:p}"),
                            RecurseSubmodules = true
                        }));
                        repo = new Repository(window.repoPath);
                        Log.Info("Git", $"Cloned at {window.repoPath}");
                    }
                    else
                    {
                        // Pull repository.
                        Log.Info("Git", $"Repo at {window.repoPath} valid; will pull");
                        repo = new Repository(window.repoPath);
                        if (!await PullAsync()) return false;
                    }
                }
                catch (LibGit2SharpException e)
                {
                    Log.Error("Git", $"Update failed: {e}");
                    window.loginStatus.Content = "Chyba; zadejte kód:";
                    window.loginStatus.Foreground = Brushes.DarkRed;
                    window.tokenBox.Visibility = Visibility.Visible;
                    return false;
                }

                window.loginStatus.Content = "Přihlášení úspěšné";
                window.loginStatus.Foreground = Brushes.Black;
                window.tokenBox.Visibility = Visibility.Collapsed;
                return true;
            }

            private async Task<bool> PullAsync()
            {
                Log.Debug("Git", "Starting pulling");
                var result = await Task.Run(() => Commands.Pull(repo, GetSignature(), new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = Credentials,
                        OnProgress = p => Status("Přihlašování", p),
                        OnTransferProgress = p => Status("Přihlašování", p)
                    }
                }));
                if (result.Status == MergeStatus.Conflicts)
                {
                    Log.Error("Git", $"Pull conflicts with merge commit {result.Commit?.Sha}");
                    window.loginStatus.Content = "Konflikt v příchozích a aktuálních změnách";
                    window.loginStatus.Foreground = Brushes.DarkRed;
                    window.tokenBox.Visibility = Visibility.Collapsed;
                    return false;
                }
                Log.Debug("Git", $"Pulled with status {result.Status} with merge commit {result.Commit?.Sha}");
                return true;
            }

            private async Task<bool> PushAsync()
            {
                Log.Debug("Git", "Starting pushing");
                PushStatusError error = null;
                await Task.Run(() => repo.Network.Push(repo.Head, new PushOptions
                {
                    CredentialsProvider = Credentials,
                    OnPackBuilderProgress = (s, c, t) => Status("Nahrávání", $"Zabalování {s}: {(double)c / t:p}"),
                    OnPushTransferProgress = (c, t, b) => Status("Nahrávání", $"Přenos: {(double)c / t:p}"),
                    OnPushStatusError = e => error = e
                }));
                if (error != null)
                {
                    Log.Error("Git", $"Publish failed (reference {error.Reference}): {error.Message}");
                    window.loginStatus.Content = $"Nahrávání selhalo: {error.Message}";
                    window.loginStatus.Foreground = Brushes.DarkRed;
                    window.tokenBox.Visibility = Visibility.Collapsed;
                    return false;
                }
                Log.Debug("Git", $"Pushed successfully");
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

            private bool Status(string title, TransferProgress p)
            {
                return Status(title, $"Přenos: {(double)p.ReceivedObjects / p.TotalObjects:p}");
            }

            private Credentials Credentials(string url, string usernameFromUrl, SupportedCredentialTypes types)
            {
                return new UsernamePasswordCredentials
                {
                    Username = "jjonescz",
                    Password = window.Dispatcher.Invoke(() => window.tokenBox.Text)
                };
            }

            private Signature GetSignature()
            {
                return new Signature("Admin GUI", "admin@jjonesova.cz", DateTimeOffset.Now);
            }

            // WARNING: This is called also before publishing, so it must not be `async`!
            private void BackupButton_Click(object sender, RoutedEventArgs e)
            {
                Log.Info("Git", "Committing changes");
                Commands.Stage(repo, "*");
                var commit = repo.Commit("Apply changes from admin GUI", GetSignature(), GetSignature());
                Log.Debug("Git", $"Committed as {commit.Sha}");
                RefreshStatus();
                window.backupButton.Content = "✔ Zálohováno";
            }

            private void RestoreButton_Click(object sender, RoutedEventArgs e)
            {
                var status = repo.RetrieveStatus();
                var changes = status.Except(status.Ignored).Count();
                var result = MessageBox.Show(window,
                    $"Změny v {changes} souborech od předchozí zálohy (nebo předchozího publikování) budou zahozeny.",
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
                    RefreshStatus();
                    window.restoreButton.Content = "✔ Obnoveno";
                    window.data.Load();
                }
            }

            private async void PublishButton_Click(object sender, RoutedEventArgs e)
            {
                // Backup (i.e., commit) first.
                if (window.backupButton.IsEnabled)
                    BackupButton_Click(this, null);

                window.publishButton.IsEnabled = false;
                window.publishButton.Content = "⌛ Zveřejňování...";
                pushing = true;

                var result = await PushAsync();
                pushing = false;
                RefreshStatus();
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

            public void RefreshStatus()
            {
                // Note that all these buttons are enabled only if changes are saved.
                // IMPORTANT: Keep consistent with `Data.Changed`.

                var status = repo.RetrieveStatus();
                var dirty = status.IsDirty && !window.saveButton.IsEnabled;
                if (dirty && !window.restoreButton.IsEnabled)
                {
                    window.restoreButton.Content = "Obnovit předchozí zálohu...";
                }
                window.restoreButton.IsEnabled = dirty;
                if (dirty && !window.backupButton.IsEnabled)
                {
                    window.backupButton.Content = "Zálohovat";
                }
                window.backupButton.IsEnabled = dirty;

                if (!pushing)
                {
                    var pushDirty = repo.Head.TrackingDetails.AheadBy > 0 && !window.saveButton.IsEnabled;
                    if (pushDirty && !window.publishButton.IsEnabled)
                    {
                        window.publishButton.Content = "Zveřejnit";
                    }
                    window.publishButton.IsEnabled = pushDirty;
                }

                // HACK: Get string representation of repository status.
                var statusString = status.GetType()
                    .GetProperty("DebuggerDisplay",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(status);

                Log.Debug("Git", $"Status: {statusString}; " +
                    $"dirty: {status.IsDirty}; " +
                    $"ahead by {repo.Head.TrackingDetails.AheadBy}; " +
                    $"behind by {repo.Head.TrackingDetails.BehindBy}");

                // Indicate changed files in UI.
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
            }
        }
    }
}
