using LibGit2Sharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JonesovaGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string tokenPath, repoPath;

        public MainWindow()
        {
            tokenPath = Path.GetFullPath("jjonesova.cz/token.txt");
            repoPath = Path.GetFullPath("jjonesova.cz/repo");

            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GlobalSettings.LogConfiguration = new LogConfiguration(LogLevel.Trace,
                (level, message) => Log.Write(level, $"Git: {message}"));

            // Load token if saved.
            if (File.Exists(tokenPath))
                tokenBox.Text = File.ReadAllText(tokenPath);

            // Check Git repo.
            Directory.CreateDirectory(repoPath);
            try
            {
                if (!Repository.IsValid(repoPath))
                {
                    // Clone repository.
                    Directory.Delete(repoPath, recursive: true);
                    await Task.Run(() => Repository.Clone("https://github.com/jjonescz/jjonesova", repoPath, new CloneOptions
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
                    using var repo = new Repository(repoPath);
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
                        loginStatus.Content = "Konflikt";
                        loginStatus.Foreground = Brushes.DarkRed;
                        tokenBox.Visibility = Visibility.Collapsed;
                        return;
                    }
                }
            }
            catch (LibGit2SharpException)
            {
                loginStatus.Content = "Chyba; zadejte kód:";
                loginStatus.Foreground = Brushes.DarkRed;
                tokenBox.Visibility = Visibility.Visible;
                return;
            }

            loginStatus.Content = "Přihlášení úspěšné";
            loginStatus.Foreground = Brushes.Black;
            tokenBox.Visibility = Visibility.Collapsed;

            // Execute Hugo.
            new Hugo(this).Start();
        }

        private void tokenBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Save token.
            File.WriteAllText(tokenPath, tokenBox.Text);
        }

        private bool Status(string title, string progress)
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                loginStatus.Content = $"{title}: {progress}";
                loginStatus.Foreground = Brushes.DarkOrange;
                tokenBox.Visibility = Visibility.Collapsed;
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
                Password = Dispatcher.Invoke(() => tokenBox.Text)
            };
        }
    }
}
