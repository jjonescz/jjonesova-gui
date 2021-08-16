using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Timers;
using System.Windows.Media;

namespace JonesovaGui
{
    public partial class MainWindow
    {
        class Deploy
        {
            private const string statusBadgeUrl = "https://api.netlify.com/api/v1/badges/525cc64c-f176-4033-a85d-e727c17b29cd/deploy-status";
            private static readonly byte[] successHash = Convert.FromBase64String("/2QIxioKQdYi5UgeY+yvQJlHY/0BPPYoauDCAxZwIF4=");
            private static readonly byte[] buildingHash = Convert.FromBase64String("");
            private static readonly byte[] canceledHash = Convert.FromBase64String("");
            private static readonly byte[] failedHash = Convert.FromBase64String("");
            private readonly MainWindow window;
            private readonly Timer timer;
            private readonly HttpClient client;
            private readonly HashAlgorithm hasher;

            public Deploy(MainWindow window)
            {
                this.window = window;
                timer = new Timer(TimeSpan.FromSeconds(10).TotalMilliseconds)
                {
                    Enabled = false,
                    AutoReset = true
                };
                timer.Elapsed += Timer_Elapsed;
                client = new HttpClient();
                hasher = SHA256.Create();
            }

            public void Detect()
            {
                if (!timer.Enabled)
                {
                    Timer_Elapsed(this, null); // Initial check.
                    timer.Start();
                }
            }

            private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
            {
                // Download Netlify status badge.
                Log.Info("Deploy", "Getting status badge");
                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    window.deployStatus.Content = "Načítání...";
                    window.deployStatus.Foreground = Brushes.Black;
                });

                var response = await client.GetAsync(statusBadgeUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Deploy", $"Couldn't get status badge: {response}");
                    return;
                }

                // Detect state from hash of the badge.
                var stream = await response.Content.ReadAsStreamAsync();
                var hash = await hasher.ComputeHashAsync(stream);
                var hashString = Convert.ToBase64String(hash);
                Log.Debug("Deploy", $"Got status badge with hash {hashString}");

                if (hash.SequenceEqual(successHash))
                {
                    _ = window.Dispatcher.InvokeAsync(() =>
                    {
                        window.deployStatus.Content = "Zveřejněno";
                        window.deployStatus.Foreground = Brushes.Green;
                    });
                    timer.Stop();
                }
                else if (hash.SequenceEqual(buildingHash))
                {
                    _ = window.Dispatcher.InvokeAsync(() =>
                    {
                        window.deployStatus.Content = "Zveřejněvání...";
                        window.deployStatus.Foreground = Brushes.DarkOrange;
                    });
                    // Continue timer => continue checking until some definitive change.
                }
                else if (hash.SequenceEqual(canceledHash) || hash.SequenceEqual(failedHash))
                {
                    Log.Warn("Deploy", "Deployment failed/canceled");
                    _ = window.Dispatcher.InvokeAsync(() =>
                    {
                        window.deployStatus.Content = "Zveřejnění selhalo";
                        window.deployStatus.Foreground = Brushes.DarkRed;
                    });
                    timer.Stop();
                }
                else
                {
                    Log.Error("Deploy", $"Unrecognized status badge hash {hashString}");
                    _ = window.Dispatcher.InvokeAsync(() =>
                    {
                        window.deployStatus.Content = "Neznámý stav zveřejnění";
                        window.deployStatus.Foreground = Brushes.DarkRed;
                    });
                    timer.Stop();
                }
            }
        }
    }
}
