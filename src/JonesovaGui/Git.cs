using LibGit2Sharp;
using System;
using System.IO;
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

            public Git(MainWindow window)
            {
                this.window = window;

                GlobalSettings.LogConfiguration = new LogConfiguration(LogLevel.Trace,
                    (level, message) => Log.Write(level, "Git", message));
            }

            public async Task RefreshAsync()
            {
                Directory.CreateDirectory(window.repoPath);
                try
                {
                    if (!Repository.IsValid(window.repoPath))
                    {
                        // Clone repository.
                        Directory.Delete(window.repoPath, recursive: true);
                        await Task.Run(() => Repository.Clone("https://github.com/jjonescz/jjonesova", window.repoPath, new CloneOptions
                        {
                            CredentialsProvider = Credentials,
                            OnProgress = p => Status("Přihlašování", p),
                            OnTransferProgress = p => Status("Přihlašování", p),
                            OnCheckoutProgress = (p, c, t) => Status("Přihlašování", $"Checkout: {(double)c / t:p}"),
                            RecurseSubmodules = true
                        }));
                    }
                    else
                    {
                        // Pull repository.
                        using var repo = new Repository(window.repoPath);
                        var signature = new Signature("Jan Joneš", "jjones@outlook.cz", DateTimeOffset.Now);
                        var result = await Task.Run(() => Commands.Pull(repo, signature, new PullOptions
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
                            window.loginStatus.Content = "Konflikt";
                            window.loginStatus.Foreground = Brushes.DarkRed;
                            window.tokenBox.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }
                }
                catch (LibGit2SharpException)
                {
                    window.loginStatus.Content = "Chyba; zadejte kód:";
                    window.loginStatus.Foreground = Brushes.DarkRed;
                    window.tokenBox.Visibility = Visibility.Visible;
                    return;
                }

                window.loginStatus.Content = "Přihlášení úspěšné";
                window.loginStatus.Foreground = Brushes.Black;
                window.tokenBox.Visibility = Visibility.Collapsed;
            }

            private bool Status(string title, string progress)
            {
                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    window.loginStatus.Content = $"{title}: {progress}";
                    window.loginStatus.Foreground = Brushes.DarkOrange;
                    window.tokenBox.Visibility = Visibility.Collapsed;
                });
                return true;
            }

            private bool Status(string title, TransferProgress p)
            {
                return Status(title, $"Transfer: {(double)p.ReceivedObjects / p.TotalObjects:p}");
            }

            private Credentials Credentials(string url, string usernameFromUrl, SupportedCredentialTypes types)
            {
                return new UsernamePasswordCredentials
                {
                    Username = "jjonescz",
                    Password = window.Dispatcher.Invoke(() => window.tokenBox.Text)
                };
            }
        }
    }
}
