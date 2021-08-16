﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JonesovaGui
{
    public partial class MainWindow
    {
        class Data
        {
            private const string indexFileName = "_index.md";
            private static readonly Regex frontMatterSeparator =
                new Regex("^---\r?$", RegexOptions.Multiline);
            private readonly IDeserializer deserializer;
            private readonly ISerializer serializer;
            private readonly MainWindow window;
            private readonly string contentFolder, assetsFolder;
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
                contentFolder = Path.Combine(window.repoPath, "content");
                assetsFolder = Path.Combine(window.repoPath, "assets");

                window.categories.SelectionChanged += Categories_SelectionChanged;
                window.albums.SelectionChanged += Albums_SelectionChanged;
                window.images.SelectionChanged += Images_SelectionChanged;
                window.addAlbumButton.Click += AddAlbumButton_Click;
                window.addImageButton.Click += AddImageButton_Click;
                window.albumUpButton.Click += AlbumUpButton_Click;
                window.albumDownButton.Click += AlbumDownButton_Click;
                window.imageUpButton.Click += ImageUpButton_Click;
                window.imageDownButton.Click += ImageDownButton_Click;
                window.albumDeleteButton.Click += AlbumDeleteButton_Click;
                window.albumTitleBox.TextChanged += AlbumTitleBox_TextChanged;
                window.albumCategoriesBox.TextChanged += AlbumCategoriesBox_TextChanged;
                window.albumTextBox.TextChanged += AlbumTextBox_TextChanged;
                window.saveButton.Click += SaveButton_Click;
            }

            private Album SelectedAlbum => window.albums.SelectedItem as Album;
            private Image SelectedImage => window.images.SelectedItem as Image;

            public void Load()
            {
                albums = Directory.EnumerateDirectories(contentFolder)
                    .Select(p =>
                    {
                        var indexPath = Path.Combine(p, indexFileName);
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
                window.categories.IsEnabled = true;

                // Refresh data all the way down.
                Categories_SelectionChanged(this, null);
            }

            private void Categories_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            {
                var category = window.categories.SelectedItem as string;
                var hasCategory = category != null;
                RefreshAlbums();
                window.albums.IsEnabled = hasCategory;
                window.addAlbumButton.IsEnabled = hasCategory;
            }

            private void Albums_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            {
                var hasAlbum = SelectedAlbum != null;
                window.albumOrder.IsEnabled = hasAlbum;
                window.albumDetails.IsEnabled = hasAlbum;
                window.albumTitleBox.Text = SelectedAlbum?.Info.Title;
                window.albumCategoriesBox.Text = SelectedAlbum == null ? null : string.Join(", ", SelectedAlbum.Info.Categories);
                window.albumTextBox.Text = SelectedAlbum?.Text;
                RefreshImages();
                window.images.IsEnabled = hasAlbum;
                window.addImageButton.IsEnabled = hasAlbum;
            }

            private void Images_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            {
                var hasImage = SelectedImage != null;
                window.imageOrder.IsEnabled = hasImage;
            }

            private void AddAlbumButton_Click(object sender, RoutedEventArgs e)
            {
                // Find unused title.
                var number = 1;
                string Title() => GetName("Album", number);
                while (albums.Any(a => Title().Equals(a.Info.Title))) number++;

                // Add new album.
                var title = Title();
                var id = Slugify(title);
                var dir = Path.Combine(contentFolder, id);
                var album = new Album
                {
                    Id = id,
                    DirectoryPath = dir,
                    IndexPath = Path.Combine(dir, indexFileName),
                    Info = new AlbumInfo
                    {
                        Title = Title(),
                        Date = DateTime.UtcNow,
                        Categories = new[] { window.categories.SelectedItem as string }
                    }
                };
                albums.Add(album);
                Changed();

                // Select it.
                RefreshAlbums();
                window.albums.SelectedItem = album;
            }

            private void AddImageButton_Click(object sender, RoutedEventArgs e)
            {
                // Add new image.
                var image = new Image
                {
                    Description = "Obrázek"
                };
                SelectedAlbum.Info.Resources.Add(image);
                Changed();

                // Select it.
                RefreshImages();
                window.images.SelectedItem = image;
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
                SelectedAlbum.Info.Date = newDate;
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
                SelectedAlbum.Info.Date = newDate;
                Changed();
                RefreshAlbums();
            }


            private void ImageUpButton_Click(object sender, RoutedEventArgs e)
            {
                var index = window.images.SelectedIndex;
                if (index <= 0) return;
                var image = SelectedImage;
                SelectedAlbum.Info.Resources.RemoveAt(index);
                SelectedAlbum.Info.Resources.Insert(index - 1, image);
                Changed();
                RefreshImages();
                window.images.SelectedIndex = index - 1;
            }

            private void ImageDownButton_Click(object sender, RoutedEventArgs e)
            {
                var index = window.images.SelectedIndex;
                if (index >= window.images.Items.Count - 1) return;
                var image = SelectedImage;
                SelectedAlbum.Info.Resources.RemoveAt(index);
                SelectedAlbum.Info.Resources.Insert(index + 1, image);
                Changed();
                RefreshImages();
                window.images.SelectedIndex = index + 1;
            }

            private void AlbumDeleteButton_Click(object sender, RoutedEventArgs e)
            {
                albums.Remove(SelectedAlbum);
                Changed();
                RefreshAlbums();
            }

            private void AlbumTitleBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            {
                if (SelectedAlbum != null && !string.Equals(SelectedAlbum.Info.Title, window.albumTitleBox.Text))
                {
                    SelectedAlbum.Info.Title = window.albumTitleBox.Text;
                    Changed();
                }
            }

            private void AlbumCategoriesBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            {
                var categories = (window.albumCategoriesBox.Text ?? string.Empty)
                    .Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToHashSet();
                if (SelectedAlbum != null && !categories.SetEquals(SelectedAlbum.Info.Categories))
                {
                    SelectedAlbum.Info.Categories = categories.ToList();
                    Changed();
                }
            }

            private void AlbumTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            {
                if (SelectedAlbum != null && !string.Equals(SelectedAlbum.Text, window.albumTextBox.Text))
                {
                    SelectedAlbum.Text = window.albumTextBox.Text;
                    Changed();
                }
            }

            private void SaveButton_Click(object sender, RoutedEventArgs e)
            {
                Log.Info("Data", "Saving changes");

                // Add/update albums.
                foreach (var album in albums)
                {
                    var yaml = serializer.Serialize(album.Info);
                    Directory.CreateDirectory(album.DirectoryPath);
                    File.WriteAllLines(album.IndexPath, new[]
                    {
                        "---",
                        yaml,
                        "---",
                        album.Text
                    });
                }

                // Delete albums.
                foreach (var p in Directory.EnumerateDirectories(contentFolder))
                {
                    if (!albums.Any(a => p.Equals(a.DirectoryPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        Directory.Delete(p, recursive: true);
                    }
                }

                window.saveButton.IsEnabled = false;
                window.saveButton.Content = "✔ Uloženo";
                window.git.RefreshStatus();

                // Refresh content.
                Load();
            }

            private void RefreshAlbums()
            {
                var category = window.categories.SelectedItem as string;
                window.albums.ItemsSource = albums
                    .Where(a => a.Info.Categories.Contains(category))
                    .OrderBy(a => a.Info.Date)
                    .ToList();
            }

            private void RefreshImages()
            {
                window.images.ItemsSource = SelectedAlbum?.Info.Resources.ToList();
            }

            private void Changed()
            {
                if (!window.saveButton.IsEnabled)
                {
                    window.saveButton.IsEnabled = true;
                    window.saveButton.Content = "Uložit";
                    window.git.RefreshStatus();
                }
            }

            private static string GetName(string prefix, int number)
            {
                if (number == 1) return prefix;
                return $"{prefix} {number}";
            }

            private static string Slugify(string name)
            {
                return name;
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
            return Src == null ? Description : Path.GetFileName(Src);
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
