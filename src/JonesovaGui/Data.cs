using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JonesovaGui
{
    public partial class MainWindow
    {
        class Data
        {
            private static readonly Regex frontMatterSeparator =
                new Regex("^---\r?$", RegexOptions.Multiline);
            private readonly IDeserializer deserializer;
            private readonly ISerializer serializer;
            private readonly MainWindow window;
            private List<Album> albums;
            private List<string> categories;

            public Data(MainWindow window)
            {
                deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitDefaults)
                    .Build();

                this.window = window;
                window.categories.SelectionChanged += Categories_SelectionChanged;
                window.albums.SelectionChanged += Albums_SelectionChanged;
                window.albumUpButton.Click += AlbumUpButton_Click;
                window.albumDownButton.Click += AlbumDownButton_Click;
                window.saveButton.Click += SaveButton_Click;
            }

            public void Load()
            {
                var contentFolder = Path.Combine(window.repoPath, "content");
                albums = Directory.EnumerateDirectories(contentFolder)
                    .Select(p =>
                    {
                        var indexPath = Path.Combine(p, "_index.md");
                        Log.Debug("Data", $"Deserializing {indexPath}");
                        var indexContent = File.ReadAllText(indexPath);
                        var parts = frontMatterSeparator.Split(indexContent, 3);
                        Debug.Assert(string.IsNullOrEmpty(parts[0]));
                        var yaml = parts[1];
                        var text = parts[2].Trim('\r', '\n');
                        return new Album
                        {
                            Id = Path.GetFileName(p),
                            DirectoryPath = p,
                            IndexPath = indexPath,
                            Info = deserializer.Deserialize<AlbumInfo>(yaml),
                            Text = text
                        };
                    })
                    .ToList();
                categories = albums.SelectMany(a => a.Info.Categories).Distinct().ToList();

                window.categories.ItemsSource = categories;
                window.categoriesStatus.Visibility = Visibility.Collapsed;
                window.categories.Visibility = Visibility.Visible;

                window.categories.SelectedItem = null;
                NoCategorySelected();
            }

            private void Categories_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            {
                var category = window.categories.SelectedItem as string;
                if (category == null)
                {
                    NoCategorySelected();
                }
                else
                {
                    window.albumsStatus.Visibility = Visibility.Collapsed;
                    RefreshAlbums();
                    window.albums.Visibility = Visibility.Visible;
                }
            }

            private void Albums_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            {
                var album = window.albums.SelectedItem as Album;
                if (album == null)
                {
                    window.albumOrder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    window.albumOrder.Visibility = Visibility.Visible;
                }
            }

            private void AlbumUpButton_Click(object sender, RoutedEventArgs e)
            {
                if (window.albums.SelectedIndex <= 0) return;
                var minus1 = (window.albums.Items[window.albums.SelectedIndex - 1] as Album).Info.Date;
                DateTime newDate;
                if (window.albums.SelectedIndex == 1)
                {
                    newDate = minus1.AddDays(-1);
                }
                else
                {
                    var minus2 = (window.albums.Items[window.albums.SelectedIndex - 2] as Album).Info.Date;
                    // Select midpoint between date of `minus2` and `minus1`.
                    newDate = minus2 + (minus1 - minus2) / 2;
                }
                Log.Debug("Data", $"Moving album #{window.albums.SelectedIndex} up, setting its date to {newDate}");
                (window.albums.SelectedItem as Album).Info.Date = newDate;
                Changed();
                RefreshAlbums();
            }

            private void AlbumDownButton_Click(object sender, RoutedEventArgs e)
            {
                if (window.albums.SelectedIndex >= window.albums.Items.Count - 1) return;
                var plus1 = (window.albums.Items[window.albums.SelectedIndex + 1] as Album).Info.Date;
                DateTime newDate;
                if (window.albums.SelectedIndex == window.albums.Items.Count - 2)
                {
                    newDate = plus1.AddDays(1);
                }
                else
                {
                    var plus2 = (window.albums.Items[window.albums.SelectedIndex + 2] as Album).Info.Date;
                    // Select midpoint between date of `plus1` and `plus2`.
                    newDate = plus1 + (plus2 - plus1) / 2;
                }
                Log.Debug("Data", $"Moving album #{window.albums.SelectedIndex} down, setting its date to {newDate}");
                (window.albums.SelectedItem as Album).Info.Date = newDate;
                Changed();
                RefreshAlbums();
            }

            private void SaveButton_Click(object sender, RoutedEventArgs e)
            {
                Log.Info("Data", "Saving changes");

                foreach (var album in albums)
                {
                    var yaml = serializer.Serialize(album.Info);
                    File.WriteAllLines(album.IndexPath, new[]
                    {
                        "---",
                        yaml,
                        "---",
                        album.Text
                    });
                }

                window.saveButton.IsEnabled = false;
                window.saveButton.Content = "✔ Uloženo";
                window.git.RefreshStatus();
            }

            private void RefreshAlbums()
            {
                var category = window.categories.SelectedItem as string;
                window.albums.ItemsSource = albums
                    .Where(a => a.Info.Categories.Contains(category))
                    .OrderBy(a => a.Info.Date)
                    .ToList();
            }

            private void NoCategorySelected()
            {
                window.albumsStatus.Content = "Nejprve vyberte kategorii";
                window.albumsStatus.Foreground = Brushes.Black;
                window.albumsStatus.Visibility = Visibility.Visible;
                window.albums.Visibility = Visibility.Collapsed;
            }

            private void Changed()
            {
                window.saveButton.IsEnabled = true;
                window.saveButton.Content = "Uložit";
            }
        }
    }

    class Album
    {
        public string Id { get; set; }
        public string DirectoryPath { get; set; }
        public string IndexPath { get; set; }
        public AlbumInfo Info { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return Info?.Title;
        }
    }

    class AlbumInfo
    {
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Albumthumb { get; set; }
        public IList<string> Categories { get; set; }
        public IList<Image> Resources { get; set; }
    }

    class Image
    {
        public string Src { get; set; }
        public string Phototitle { get; set; }
        public string Description { get; set; }
        public bool Exif { get; set; }

        public override string ToString()
        {
            return Src;
        }
    }

    static class DictExtensions
    {
        public static IEnumerable<T> GetItems<T>(this Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var value))
                return (value as IEnumerable<object>).Cast<T>();
            return Enumerable.Empty<T>();
        }
    }
}
