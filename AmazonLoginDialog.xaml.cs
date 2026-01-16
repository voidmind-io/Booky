using System.Windows;
using Microsoft.Web.WebView2.Core;
using Booky.Services;

namespace Booky;

public partial class AmazonLoginDialog : Window
{
    private readonly KindleWebService _kindleService;
    private bool _loginComplete = false;

    public bool LoginSuccessful { get; private set; }

    public AmazonLoginDialog(KindleWebService kindleService)
    {
        InitializeComponent();
        _kindleService = kindleService;
        Loaded += AmazonLoginDialog_Loaded;
    }

    private async void AmazonLoginDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Initialize WebView2
            var env = await CoreWebView2Environment.CreateAsync();
            await WebView.EnsureCoreWebView2Async(env);

            // Navigate to Amazon sign-in page for Send to Kindle
            WebView.CoreWebView2.Navigate("https://www.amazon.com/sendtokindle");

            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_loginComplete)
            return;

        var url = WebView.Source?.ToString() ?? "";

        // Check if we've successfully logged in and landed on the Send to Kindle page
        if (url.Contains("/sendtokindle") && !url.Contains("/ap/signin"))
        {
            // Try to detect if we're actually logged in by checking the page content
            try
            {
                var html = await WebView.CoreWebView2.ExecuteScriptAsync("document.body.innerHTML");

                // If we can see the send to kindle interface (not the sign-in prompt), we're logged in
                if (html.Contains("csrfToken") || html.Contains("send-to-kindle"))
                {
                    await ExtractAndSaveCookies();
                }
            }
            catch
            {
                // Ignore script errors
            }
        }

        // Update status based on URL
        if (url.Contains("/ap/signin"))
        {
            StatusText.Text = "Please enter your Amazon credentials...";
        }
        else if (url.Contains("/sendtokindle"))
        {
            StatusText.Text = "Verifying login...";
        }
    }

    private async Task ExtractAndSaveCookies()
    {
        try
        {
            StatusText.Text = "Saving login session...";

            // Get all cookies from WebView2
            var cookies = await WebView.CoreWebView2.CookieManager.GetCookiesAsync("https://www.amazon.com");

            var cookieList = cookies.Select(c => (c.Name, c.Value, c.Domain, c.Path)).ToList();

            _kindleService.ImportCookies(cookieList);

            // Verify the session works
            var isValid = await _kindleService.VerifySessionAsync();

            if (isValid)
            {
                _loginComplete = true;
                LoginSuccessful = true;
                StatusText.Text = "Login successful!";

                await Task.Delay(500);
                DialogResult = true;
                Close();
            }
            else
            {
                StatusText.Text = "Please complete the sign-in process...";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
