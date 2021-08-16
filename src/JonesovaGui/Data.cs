using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            private readonly MainWindow window;
            private List<Album> albums;
            private List<string> categories;

            public Data(MainWindow window)
            {
                this.window = window;
                window.categories.SelectionChanged += Categories_SelectionChanged;
            }

            public void Load()
            {
                var pipeline = new MarkdownPipelineBuilder()
                    .UseYamlFrontMatter()
                    .Build();
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var contentFolder = Path.Combine(window.repoPath, "content");
                albums = Directory.EnumerateDirectories(contentFolder)
                    .Select(p =>
                    {
                        var indexPath = Path.Combine(p, "_index.md");
                        Log.Debug("Data", $"Deserializing {indexPath}");
                        var indexContent = File.ReadAllText(indexPath);
                        var markdown = Markdown.Parse(indexContent, pipeline);
                        var yaml = markdown.Descendants<YamlFrontMatterBlock>()
                            .Single().Lines.ToString();
                        return new Album
                        {
                            Info = deserializer.Deserialize<AlbumInfo>(yaml),
                            Content = indexContent
                        };
                    })
                    .ToList();
                categories = albums.SelectMany(a => a.Info.Categories).Distinct().ToList();

                window.categories.ItemsSource = categories;
                window.categoriesStatus.Visibility = Visibility.Collapsed;
                window.categories.Visibility = Visibility.Visible;

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
                    window.albums.ItemsSource = albums
                        .Where(a => a.Info.Categories.Contains(category)).ToList();
                    window.albums.Visibility = Visibility.Visible;
                }
            }

            private void NoCategorySelected()
            {
                window.albumsStatus.Content = "Nejprve vyberte kategorii";
                window.albumsStatus.Foreground = Brushes.Black;
                window.albumsStatus.Visibility = Visibility.Visible;
                window.albums.Visibility = Visibility.Collapsed;
            }
        }
    }

    class Album
    {
        public AlbumInfo Info { get; set; }
        public string Content { get; set; }

        public override string ToString()
        {
            return Info?.Title;
        }
    }

    class AlbumInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Albumthumb { get; set; }
        public DateTime Date { get; set; }
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
