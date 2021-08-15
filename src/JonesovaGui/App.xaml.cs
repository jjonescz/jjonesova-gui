using LibGit2Sharp;
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
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Log.Info("App", $"Exitting ({e.ApplicationExitCode})");
        }
    }
}
