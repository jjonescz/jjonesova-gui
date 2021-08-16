using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace JonesovaGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string tokenPath, repoPath, windowPath;
        private readonly Git git;
        private readonly Data data;
        private readonly Deploy deploy;

        public MainWindow()
        {
            tokenPath = Path.Combine(Log.RootPath, "token.txt");
            repoPath = Path.Combine(Log.RootPath, "repo");
            windowPath = Path.Combine(Log.RootPath, "window.txt");

            InitializeComponent();

            git = new Git(this);
            data = new Data(this);
            deploy = new Deploy(this);
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // Restore window coordinates. Inspired by
            // https://stackoverflow.com/a/847761.
            if (File.Exists(windowPath))
            {
                Log.Debug("MainWindow", $"Deserializing window coordinates from {windowPath}");
                try
                {
                    var windowText = File.ReadAllText(windowPath);
                    var coords = JsonSerializer.Deserialize<WindowCoordinates>(windowText);
                    Top = coords.Top;
                    Left = coords.Left;
                    Height = coords.Height;
                    Width = coords.Width;
                    if (coords.Maximized) WindowState = WindowState.Maximized;
                }
                catch (Exception ex)
                {
                    Log.Error("MainWindow", $"Cannot deserialize window coordinates: {ex}");
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load Git token.
            if (File.Exists(tokenPath))
                tokenBox.Text = File.ReadAllText(tokenPath);

            // Update Git repo.
            await git.UpdateAsync();

            // Execute Hugo.
            new Hugo(this).Start();

            // Load content.
            data.Load();

            // Detect deployment status.
            deploy.Detect();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save window coordinates. Inspired by
            // https://stackoverflow.com/a/847761.
            var coords = new WindowCoordinates();
            if (WindowState == WindowState.Maximized)
            {
                coords.Maximized = true;
                coords.Height = RestoreBounds.Height;
                coords.Width = RestoreBounds.Width;
                coords.Top = RestoreBounds.Top;
                coords.Left = RestoreBounds.Left;
            }
            else
            {
                coords.Top = Top;
                coords.Left = Left;
                coords.Height = Height;
                coords.Width = Width;
                coords.Maximized = false;
            }
            Log.Debug("MainWindow", $"Saving coordinates: {coords}");
            using (var stream = File.OpenWrite(windowPath))
            {
                stream.SetLength(0);
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                JsonSerializer.Serialize(writer, coords);
            }

            if (saveButton.IsEnabled)
            {
                var result = MessageBox.Show(this,
                    "Neuložené změny budou zahozeny!",
                    "Zavírání programu jjonesova.cz",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel);
                if (result != MessageBoxResult.OK)
                {
                    e.Cancel = true;
                }
            }
        }

        private void tokenBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Save Git token.
            File.WriteAllText(tokenPath, tokenBox.Text);
        }

        private void webButton_Click(object sender, RoutedEventArgs e)
        {
            Log.Debug("MainWindow", "Opening web");
            Process.Start(new ProcessStartInfo("https://jjonesova.cz") { UseShellExecute = true });
        }
    }

    record WindowCoordinates
    {
        public bool Maximized { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Top { get; set; }
        public double Left { get; set; }
        public double AlbumsWidth { get; set; }
        public double ImagesWidth { get; set; }
    }
}
