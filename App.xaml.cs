using DurdomClient.Helpers;

namespace DurdomClient;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        LanguageManager.Initialize("en");
        var window = new MainWindow();
        window.Show();
    }
}

