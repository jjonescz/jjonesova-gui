using Microsoft.Win32;
using Slugify;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
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
            private readonly SlugHelper slugifier = new SlugHelper();
            private readonly IDeserializer deserializer;
            private readonly ISerializer serializer;
            private readonly MainWindow window;
            private readonly string contentFolder, assetsFolder, lastDirPath;
            private string lastDir;
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
                lastDirPath = Path.Combine(Log.RootPath, "last-dir.txt");

                if (File.Exists(lastDirPath))
                    lastDir = File.ReadAllText(lastDirPath);

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
                window.imageDeleteButton.Click += ImageDeleteButton_Click;
                window.albumTitleBox.TextChanged += AlbumTitleBox_TextChanged;
                window.albumCategoriesBox.TextChanged += AlbumCategoriesBox_TextChanged;
                window.albumTextBox.TextChanged += AlbumTextBox_TextChanged;
                window.imageSrcBox.TextChanged += ImageSrcBox_TextChanged;
                window.imageLabelBox.TextChanged += ImageLabelBox_TextChanged;
                window.imageExifBox.Click += ImageExifBox_Click;
                window.imageSrcButton.Click += ImageSrcButton_Click;
                window.imageOpenButton.Click += ImageOpenButton_Click;
                window.saveButton.Click += SaveButton_Click;
            }

            private Album SelectedAlbum => window.albums.SelectedItem as Album;
            private Image SelectedImage => window.images.SelectedItem as Image;
            public IReadOnlyList<Album> Albums => albums;

            public void Load()
            {
                var selectedAlbum = window.albums.SelectedIndex;
                var selectedImage = window.images.SelectedIndex;

                albums = Directory.EnumerateDirectories(contentFolder)
                    .Select(p =>
                    {
                        var indexPath = Path.Combine(p, indexFileName);

                        // Ignore empty folders.
                        if (!File.Exists(indexPath)) return null;

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
                    .Where(a => a != null)
                    .ToList();
                categories = albums.SelectMany(a => a.Info.Categories).Distinct().ToList();

                // Normalize some properties for editing (they are then again
                // modified when saving).
                foreach (var album in albums)
                {
                    foreach (var image in album.Info.Resources)
                    {
                        if (!string.IsNullOrEmpty(image.Src))
                        {
                            image.FullPath = Path.GetFullPath($"./{image.Src}", basePath: assetsFolder);
                            image.Src = Path.GetFileName(image.Src);
                        }
                    }
                }

                window.categories.ItemsSource = categories;
                window.categories.IsEnabled = true;

                // Refresh data all the way down.
                Categories_SelectionChanged(this, null);

                // Restore selections.
                window.albums.SelectedIndex = selectedAlbum;
                window.images.SelectedIndex = selectedImage;
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
                window.imageDetails.IsEnabled = hasImage;
                window.imageLabelBox.Text = SelectedImage?.Description;
                window.imageExifBox.IsChecked = SelectedImage?.Exif ?? false;
                RefreshImage();
            }

            private void AddAlbumButton_Click(object sender, RoutedEventArgs e)
            {
                // Find unused title.
                var number = 1;
                string title;
                while (true)
                {
                    title = GetName("Album", number++, " ");
                    if (!albums.Any(a => title.Equals(a.Info.Title))) break;
                }

                // Add new album.
                var id = Slugify(title);
                var dir = Path.Combine(contentFolder, id);
                var album = new Album
                {
                    Id = id,
                    DirectoryPath = dir,
                    IndexPath = Path.Combine(dir, indexFileName),
                    Info = new AlbumInfo
                    {
                        Title = title,
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

            private void ImageDeleteButton_Click(object sender, RoutedEventArgs e)
            {
                SelectedAlbum.Info.Resources.Remove(SelectedImage);
                Changed();
                RefreshImages();
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

            private void ImageSrcBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            {
                var newValue = window.imageSrcBox.Text.Trim();
                if (SelectedImage != null && !string.Equals(SelectedImage.Src, newValue, StringComparison.Ordinal))
                {
                    SelectedImage.Src = newValue;
                    Changed();
                }
            }

            private void ImageLabelBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            {
                if (SelectedImage != null && !string.Equals(SelectedImage.Description, window.imageLabelBox.Text))
                {
                    SelectedImage.Description = window.imageLabelBox.Text;
                    Changed();
                }
            }

            private void ImageExifBox_Click(object sender, RoutedEventArgs e)
            {
                var newValue = window.imageExifBox.IsChecked ?? false;
                if (SelectedImage != null && SelectedImage.Exif != newValue)
                {
                    SelectedImage.Exif = newValue;
                    Changed();
                    RefreshImage();
                }
            }

            private void ImageSrcButton_Click(object sender, RoutedEventArgs e)
            {
                var dialog = new OpenFileDialog
                {
                    InitialDirectory = lastDir ?? string.Empty
                };
                if (dialog.ShowDialog(window) == true)
                {
                    // Remember selected directory for next time.
                    lastDir = Path.GetDirectoryName(dialog.FileName);
                    File.WriteAllText(lastDirPath, lastDir);

                    // Update file path.
                    SelectedImage.FullPath = dialog.FileName;
                    var name = Path.GetFileName(dialog.FileName);
                    SelectedImage.Src = $"/{SelectedAlbum.Id}/{name.ToLower()}";
                    Changed();
                    RefreshImage();
                }
            }

            private void ImageOpenButton_Click(object sender, RoutedEventArgs e)
            {
                Log.Debug("Data", $"Opening image {SelectedImage.FullPath}");
                Process.Start(new ProcessStartInfo(SelectedImage.FullPath) { UseShellExecute = true });
            }

            private async void SaveButton_Click(object sender, RoutedEventArgs e)
            {
                Log.Info("Data", "Saving changes");

                window.saveButton.IsEnabled = false;
                window.saveButton.Content = "⏳ Ukládání...";

                await Task.Run(async () =>
                {
                    // Add/update albums.
                    foreach (var album in albums)
                    {
                        // Normalize properties.
                        album.Id = Slugify(album.Info.Title);
                        album.DirectoryPath = Path.Combine(contentFolder, album.Id);
                        album.IndexPath = Path.Combine(album.DirectoryPath, indexFileName);
                        foreach (var image in album.Info.Resources)
                        {
                            image.Src = $"/{album.Id}/{image.Src}";
                        }

                        // Save Markdown.
                        var yaml = serializer.Serialize(album.Info);
                        Directory.CreateDirectory(album.DirectoryPath);
                        await File.WriteAllLinesAsync(album.IndexPath, new[]
                        {
                            "---",
                            yaml,
                            "---",
                            album.Text
                        });
                    }

                    // Copy images. Note that this must happen before deleting
                    // old albums, as we might need to copy images of albums
                    // that will be deleted in the next step (this happens when
                    // renaming an album).
                    var anyCopied = false;
                    foreach (var album in albums)
                    {
                        var existingNames = new HashSet<string>();
                        foreach (var image in album.Info.Resources)
                        {
                            if (string.IsNullOrEmpty(image.Src)) continue;

                            // Find unused file name.
                            var number = 1;
                            string name;
                            while (true)
                            {
                                var bareName = Path.GetFileNameWithoutExtension(image.Src);
                                var extension = Path.GetExtension(image.Src);
                                name = GetName(bareName, number++, "_") + extension;
                                if (existingNames.Add(name)) break;
                            }

                            // Copy image if not the same as previously.
                            var fullPath = Path.Combine(assetsFolder, album.Id, name);
                            if (!fullPath.Equals(image.FullPath))
                            {
                                // Report status (if the first time copying).
                                if (!anyCopied)
                                {
                                    anyCopied = true;
                                    _ = window.Dispatcher.InvokeAsync(() =>
                                    {
                                        window.saveButton.Content = "⌛ Kopírování obrázků...";
                                    });
                                }

                                Log.Debug("Data", $"Copying image from {image.FullPath} to {fullPath}");
                                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                                File.Copy(image.FullPath, fullPath, overwrite: true);

                                // Update full path (needed below when deleting
                                // unused images; would not be needed otherwise
                                // since we will reload all data anyway).
                                image.FullPath = fullPath;
                            }
                        }
                    }

                    // Delete albums.
                    foreach (var p in Directory.EnumerateDirectories(contentFolder))
                    {
                        if (!albums.Any(a => p.Equals(a.DirectoryPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            var albumId = Path.GetFileName(p);
                            var albumAssetsPath = Path.Combine(assetsFolder, albumId);
                            if (Directory.Exists(p))
                            {
                                Log.Debug("Data", $"Deleting album {albumId} at {p}");
                                Directory.Delete(p, recursive: true);
                            }
                            if (Directory.Exists(albumAssetsPath))
                            {
                                Log.Debug("Data", $"Deleting album assets at {albumAssetsPath}");
                                Directory.Delete(albumAssetsPath, recursive: true);
                            }
                        }
                    }

                    // Delete unused images.
                    foreach (var dir in Directory.EnumerateDirectories(assetsFolder))
                    {
                        foreach (var p in Directory.EnumerateFiles(dir))
                        {
                            if (!albums.SelectMany(a => a.Info.Resources).Any(i => p.Equals(i.FullPath, StringComparison.OrdinalIgnoreCase)))
                            {
                                Log.Debug("Data", $"Deleting image {p}");
                                File.Delete(p);
                            }
                        }
                    }
                });

                _ = window.Dispatcher.InvokeAsync(() =>
                {
                    window.saveButton.IsEnabled = false;
                    window.saveButton.Content = "✔ Uloženo";
                    window.git.RefreshStatus();

                    // Refresh content.
                    Load();
                });
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

            private async void RefreshImage()
            {
                // Show source file name.
                window.imageSrcBox.IsEnabled = SelectedImage != null;
                window.imageSrcBox.Text = SelectedImage?.Src;

                // Load image in background.
                window.image.Source = null;
                var fullPath = SelectedImage?.FullPath;
                window.imageOpenButton.IsEnabled = fullPath != null;
                if (fullPath != null)
                {
                    window.imageStatus.Visibility = Visibility.Visible;
                    var uri = new Uri(fullPath, UriKind.Absolute);
                    var exif = SelectedImage.Exif;
                    var bitmap = await Task.Run(() =>
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = uri;
                        if (exif) image.Rotation = DetectRotation(fullPath);
                        image.EndInit();
                        image.Freeze();
                        return image;
                    });
                    _ = window.Dispatcher.InvokeAsync(() =>
                    {
                        window.imageStatus.Visibility = Visibility.Collapsed;
                        window.image.Source = bitmap;
                    });
                }
            }

            // From https://stackoverflow.com/a/63627972.
            private static Rotation DetectRotation(string path)
            {
                const string orientationQuery = "System.Photo.Orientation";
                using (var stream = File.OpenRead(path))
                {
                    var bitmapFrame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    var bitmapMetadata = bitmapFrame.Metadata as BitmapMetadata;

                    if (bitmapMetadata != null && bitmapMetadata.ContainsQuery(orientationQuery))
                    {
                        var o = bitmapMetadata.GetQuery(orientationQuery);
                        if (o != null)
                        {
                            return (ushort)o switch
                            {
                                6 => Rotation.Rotate90,
                                3 => Rotation.Rotate180,
                                8 => Rotation.Rotate270,
                                _ => Rotation.Rotate0,
                            };
                        }
                    }
                }
                return Rotation.Rotate0;
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

            private static string GetName(string prefix, int number, string separator)
            {
                if (number == 1) return prefix;
                return $"{prefix}{separator}{number}";
            }

            private string Slugify(string name)
            {
                return slugifier.GenerateSlug(name).Trim('-');
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
        public bool Changed { get; set; }

        public override string ToString()
        {
            return Info?.Title + (Changed ? " *" : null);
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
        [YamlIgnore]
        public string FullPath { get; set; }
        public string Src { get; set; }
        public string Phototitle { get; set; }
        public string Description { get; set; }
        public bool Exif { get; set; }
        [YamlIgnore]
        public bool Changed { get; set; }

        public override string ToString()
        {
            return (Src == null ? Description : Path.GetFileName(Src)) + (Changed ? " *" : null);
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
