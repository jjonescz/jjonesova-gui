using System;
using System.Net.Http;
using System.Timers;
using System.Windows.Media;

namespace JonesovaGui
{
    public partial class MainWindow
    {
        class Deploy
        {
            private const string statusBadgeUrl = "https://api.netlify.com/api/v1/badges/525cc64c-f176-4033-a85d-e727c17b29cd/deploy-status";
            private readonly MainWindow window;
            private readonly Timer timer;
            private readonly HttpClient client;
            private DeployStatus previousStatus;

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
            }

            public void Detect()
            {
                if (!timer.Enabled)
                {
                    Log.Debug("Deploy", $"Starting checking status (previously {previousStatus})");
                    Timer_Elapsed(this, null); // Initial check.
                    timer.Start();
                }
                else
                {
                    Log.Warn("Deploy", $"Wanting to start checking that is already started (currenty {previousStatus})");
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

                // Detect state from color used inside SVG of the badge.
                var svg = await response.Content.ReadAsStringAsync();

                // Colors can be obtained from
                // https://github.com/badges/shields/blob/23c0406bedfc6930735e8f5ea75dfe34faf1f290/services/netlify/netlify.service.js#L55-L57
                // but we used more thorough manual inspection of image in
                // https://docs.netlify.com/monitor-sites/status-badges/.
                bool HasColor(string color)
                {
                    return svg.Contains(color, StringComparison.OrdinalIgnoreCase);
                }
                var status = svg switch
                {
                    _ when HasColor("#c9eeeb") => DeployStatus.Success,
                    _ when HasColor("#fed7e2") => DeployStatus.Failed,
                    _ when HasColor("#ffe4c2") => DeployStatus.Building,
                    _ when HasColor("#e9eaeb") => DeployStatus.Canceled,
                    _ => DeployStatus.Unknown
                };

                Log.Debug("Deploy", $"Got status badge {status}");

                switch (status)
                {
                    case DeployStatus.Success:
                        _ = window.Dispatcher.InvokeAsync(() =>
                        {
                            window.deployStatus.Content = "Zveřejněno";
                            window.deployStatus.Foreground = Brushes.Green;
                        });
                        break;
                    case DeployStatus.Building:
                        _ = window.Dispatcher.InvokeAsync(() =>
                        {
                            window.deployStatus.Content = "Zveřejňování...";
                            window.deployStatus.Foreground = Brushes.DarkOrange;
                        });
                        break;
                    case DeployStatus.Canceled:
                    case DeployStatus.Failed:
                        Log.Warn("Deploy", "Deployment failed/canceled");
                        _ = window.Dispatcher.InvokeAsync(() =>
                        {
                            window.deployStatus.Content = "Zveřejnění selhalo";
                            window.deployStatus.Foreground = Brushes.DarkRed;
                        });
                        break;
                    default:
                        Log.Error("Deploy", $"Unrecognized status badge {svg}");
                        _ = window.Dispatcher.InvokeAsync(() =>
                        {
                            window.deployStatus.Content = "Neznámý stav zveřejnění";
                            window.deployStatus.Foreground = Brushes.DarkRed;
                        });
                        break;
                }
                Changed(status);
            }

            private void Changed(DeployStatus status)
            {
                try
                {
                    if (previousStatus == status)
                    {
                        // Continue checking, until status changes for the first time.
                        Log.Debug("Deploy", $"Same state ({status}), checking continues");
                        return;
                    }

                    if (status == DeployStatus.Building)
                    {
                        // Continue checking while build is in progress.
                        Log.Debug("Deploy", $"Build in progress (previously {previousStatus}), checking continues");
                        return;
                    }

                    // Otherwise, stop timer; we got some status change.
                    timer.Stop();
                    Log.Debug("Deploy", $"Got state {status} (previously {previousStatus}), checking stopped");
                }
                finally
                {
                    previousStatus = status;
                }
            }
        }
    }

    public enum DeployStatus
    {
        Unknown,
        Success,
        Building,
        Canceled,
        Failed
    }
}
