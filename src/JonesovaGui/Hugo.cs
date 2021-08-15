using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            public Hugo(MainWindow window)
            {
                this.window = window;
                var hugoPath = Path.GetFullPath("Assets/hugo.exe");
                hugoStart = new ProcessStartInfo(hugoPath, "server")
                {
                    WorkingDirectory = window.repoPath
                };

                window.previewButton.Click += PreviewButton_Click;
            }

            public void Start()
            {
                process = Process.Start(hugoStart);
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.Exited += Process_Exited;
            }

            private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
            {
            }

            private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    window.previewStatus.Content = $"Chyba: ${e.Data}";
                    window.previewStatus.Foreground = Brushes.DarkRed;
                });
            }

            private void Process_Exited(object sender, EventArgs e)
            {
                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    window.previewStatus.Content = $"Neaktivní";
                    window.previewStatus.Foreground = Brushes.DarkRed;
                });
            }

            private void PreviewButton_Click(object sender, System.Windows.RoutedEventArgs e)
            {
            }
        }
    }
}
