using System.Windows;

namespace Booky;

public partial class ConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmDialog(string title, string message, string confirmText = "Yes", string cancelText = "Cancel")
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }

    public static bool Show(Window owner, string title, string message, string confirmText = "Yes", string cancelText = "Cancel")
    {
        var dialog = new ConfirmDialog(title, message, confirmText, cancelText)
        {
            Owner = owner
        };
        dialog.ShowDialog();
        return dialog.Confirmed;
    }
}
