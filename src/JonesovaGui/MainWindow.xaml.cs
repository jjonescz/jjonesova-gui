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
        private readonly Git git;
        private readonly Data data;

        public MainWindow()
        {
            tokenPath = Path.GetFullPath("jjonesova.cz/token.txt");
            repoPath = Path.GetFullPath("jjonesova.cz/repo");

            InitializeComponent();

            git = new Git(this);
            data = new Data(this);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load Git token.
            if (File.Exists(tokenPath))
                tokenBox.Text = File.ReadAllText(tokenPath);

            // Update Git repo.
            await git.PullAsync();

            // Execute Hugo.
            new Hugo(this).Start();

            // Load content.
            data.Load();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
    }
}
