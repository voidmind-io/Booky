namespace Booky;

public partial class App : System.Windows.Application
{
    public static string? StartupFilePath { get; private set; }

    private void Application_Startup(object sender, System.Windows.StartupEventArgs e)
    {
        // Check if a file was passed as argument (e.g., from Explorer context menu)
        if (e.Args.Length > 0 && !string.IsNullOrEmpty(e.Args[0]))
        {
            var filePath = e.Args[0];
            if (System.IO.File.Exists(filePath))
            {
                StartupFilePath = filePath;
            }
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
