using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace JonesovaGui
{
    public partial class MainWindow
    {
        class Hugo
        {
            private readonly MainWindow window;
            private readonly ProcessStartInfo hugoStart;
            private Process process;
            private string address;

            public Hugo(MainWindow window)
            {
                this.window = window;
                var hugoPath = Path.GetFullPath("Assets/hugo.exe");
                hugoStart = new ProcessStartInfo(hugoPath, "--gc server")
                {
                    WorkingDirectory = window.repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                window.previewButton.Click += PreviewButton_Click;
                window.Closing += Window_Closing;
            }

            private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!e.Cancel)
                {
                    process?.Kill();
                }
            }

            public void Start()
            {
                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    window.previewStatus.Content = "Načítání...";
                    window.previewStatus.Foreground = Brushes.DarkOrange;
                });

                // Kill existing hugo processes (can be there from previous
                // debugging sessions even).
                foreach (var old in Process.GetProcessesByName("hugo"))
                    old.Kill();

                process = new Process();
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.Exited += Process_Exited;
                process.StartInfo = hugoStart;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _ = process.WaitForExitAsync();
            }

            private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                Log.Info("Hugo", $"Stdout: {e.Data}");

                var match = Regex.Match(e.Data ?? string.Empty, @"Web Server is available at (\S+)");
                if (match.Success)
                {
                    var oldAddress = address;
                    address = match.Groups[1].Value;
                    if (!string.Equals(address, oldAddress))
                    { 
                        OpenPreview();
                        _ = window.Dispatcher.InvokeAsync(() =>
                        {
                            window.previewStatus.Content = $"Načteno ({address})";
                            window.previewStatus.Foreground = Brushes.Black;
                        });
                    }
                }
            }

            private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                Log.Error("Hugo", $"Stderr: {e.Data}");

                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    window.previewStatus.Content = $"Chyba: {e.Data}";
                    window.previewStatus.Foreground = Brushes.DarkRed;
                });
            }

            private void Process_Exited(object sender, EventArgs e)
            {
                Log.Info("Hugo", $"Exited: {process.ExitCode}");

                address = null;
                process = null;

                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    window.previewStatus.Content = $"Neaktivní";
                    window.previewStatus.Foreground = Brushes.DarkRed;
                });
            }

            private void PreviewButton_Click(object sender, System.Windows.RoutedEventArgs e)
            {
                if (address != null)
                {
                    OpenPreview();
                }
                else
                {
                    Start();
                }
            }

            private void OpenPreview()
            {
                Log.Debug("Hugo", $"Opening address {address}");
                Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            }
        }
    }
}
