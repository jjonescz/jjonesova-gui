using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
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
                        var indexContent = File.ReadAllText(indexPath);
                        var markdown = Markdown.Parse(indexContent, pipeline);
                        var yaml = markdown.Descendants<YamlFrontMatterBlock>()
                            .Single().Lines.ToString();
                        var dict = deserializer.Deserialize<Dictionary<string, object>>(yaml);
                        return new Album
                        {
                            Id = Path.GetFileName(p),
                            Title = dict.GetValueOrDefault("title") as string,
                            Thumb = dict.GetValueOrDefault("albumthumb") as string,
                            Categories = dict.GetList<string>("categories"),
                            Images = dict.GetItems<Dictionary<string, object>>("resources")
                                .Select(d => new Image
                                {
                                    Src = d.GetValueOrDefault("src") as string,
                                    Description = d.GetValueOrDefault("description") as string,
                                    Exif = true.Equals(d.GetValueOrDefault("exif"))
                                })
                                .ToList(),
                            Content = indexContent
                        };
                    })
                    .ToList();
                categories = albums.SelectMany(a => a.Categories).Distinct().ToList();

                window.categories.ItemsSource = categories;
                window.categoriesStatus.Visibility = Visibility.Collapsed;
                window.categories.Visibility = Visibility.Visible;
            }
        }
    }

    class Album
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Thumb { get; set; }
        public IList<string> Categories { get; set; }
        public IList<Image> Images { get; set; }
        public string Content { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }

    class Image
    {
        public string Src { get; set; }
        public string Description { get; set; }
        public bool Exif { get; set; }

        public override string ToString()
        {
            return Src;
        }
    }

    static class DictExtensions
    {
        public static IList<T> GetList<T>(this Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value is IList list)
                return list.Cast<T>().ToList();
            return Array.Empty<T>();
        }

        public static IEnumerable<T> GetItems<T>(this Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value is IEnumerable<T> items)
                return items;
            return Enumerable.Empty<T>();
        }
    }
}
