using System.Diagnostics;
using System.Windows;

namespace JonesovaGui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("App", e.Exception.ToString());

            // Open log file.
            MessageBox.Show(
                "Nastala chyba. Po kliknutí na tlačítko OK se otevře soubor, který pošleš Janu Jonešovi on to spraví.",
                "jjonesova.cz",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Process.Start(new ProcessStartInfo(Log.LogPath) { UseShellExecute = true });
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Log.Info("App", $"Exitting ({e.ApplicationExitCode})");
        }
    }
}
