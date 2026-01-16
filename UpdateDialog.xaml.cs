using System.Windows;
using Booky.Services;

namespace Booky;

public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _updateInfo;

    public UpdateDialog(UpdateInfo updateInfo)
    {
        InitializeComponent();
        _updateInfo = updateInfo;

        CurrentVersionText.Text = updateInfo.CurrentVersion;
        NewVersionText.Text = updateInfo.LatestVersion;

        if (!string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes))
        {
            ReleaseNotesText.Text = updateInfo.ReleaseNotes;
            ReleaseNotesSection.Visibility = Visibility.Visible;
        }
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        // Open the release page or direct download
        var url = _updateInfo.DownloadUrl ?? _updateInfo.ReleaseUrl;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        Close();
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public static void ShowIfAvailable(Window owner, UpdateInfo? updateInfo)
    {
        if (updateInfo == null) return;

        var dialog = new UpdateDialog(updateInfo)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }
}
