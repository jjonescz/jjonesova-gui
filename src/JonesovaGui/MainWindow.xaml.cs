using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace JonesovaGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string tokenPath, repoPath;

        public MainWindow()
        {
            tokenPath = Path.GetFullPath("jjonesova.cz/token.txt");
            repoPath = Path.GetFullPath("jjonesova.cz/repo");

            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load Git token.
            if (File.Exists(tokenPath))
                tokenBox.Text = File.ReadAllText(tokenPath);

            // Update Git repo.
            await new Git(this).RefreshAsync();

            // Execute Hugo.
            new Hugo(this).Start();
        }

        private void tokenBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Save Git token.
            File.WriteAllText(tokenPath, tokenBox.Text);
        }
    }
}
