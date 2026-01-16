using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Booky.Services;
using Booky.Models;
using WinForms = System.Windows.Forms;

namespace Booky;

public partial class MainWindow : Window
{
    private string? _currentFilePath;
    private string? _lastOutputPath;
    private string? _lastTitle;
    private string? _lastAuthor;
    private readonly List<string> _lastBatchOutputPaths = new();
    private readonly ConversionService _conversionService;
    private readonly KindleWebService _kindleService;
    private int _logoClickCount;
    private readonly DispatcherTimer _easterEggResetTimer;
    private readonly ObservableCollection<BookMetadata> _books = new();
    private const double NormalWidth = 500;
    private const double MultiFileWidth = 700;

    // Dark title bar support
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();
        _conversionService = new ConversionService();
        _kindleService = new KindleWebService();
        FileListView.ItemsSource = _books;

        // Easter egg timer - resets click count after 2 seconds of no clicks
        _easterEggResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _easterEggResetTimer.Tick += (s, e) => { _logoClickCount = 0; _easterEggResetTimer.Stop(); };

        // Enable dark title bar and handle startup file
        Loaded += (s, e) =>
        {
            EnableDarkTitleBar();
            UpdateKindleUI();

            // Check if app was launched with a file (e.g., from Explorer context menu)
            if (!string.IsNullOrEmpty(App.StartupFilePath))
            {
                LoadFile(App.StartupFilePath);
            }
        };
    }

    private void UpdateKindleUI()
    {
        if (_kindleService.IsConfigured)
        {
            KindleStatusText.Text = "Kindle Connected";
            SendToKindleButton.Visibility = Visibility.Visible;
            SendAllToKindleButton.Visibility = Visibility.Visible;

            SendToKindleButtonText.Text = "Send to Kindle";
            SendAllToKindleButtonText.Text = "Send All to Kindle";
        }
        else
        {
            KindleStatusText.Text = "Setup Kindle";
            SendToKindleButton.Visibility = Visibility.Collapsed;
            SendAllToKindleButton.Visibility = Visibility.Collapsed;
        }
    }

    private void EnableDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int value = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private void Logo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _easterEggResetTimer.Stop();
        _easterEggResetTimer.Start();
        _logoClickCount++;

        if (_logoClickCount >= 7)
        {
            _logoClickCount = 0;
            _easterEggResetTimer.Stop();
            SetStatus("Made with \u2764 for noodlecore", isSuccess: true);
        }
    }

    private void VoidMindLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://voidmind.io/",
            UseShellExecute = true
        });
    }

    private void CoffeeLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://buymeacoffee.com/voidmind",
            UseShellExecute = true
        });
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var supportedFiles = files.Where(IsSupportedFile).ToArray();
            if (supportedFiles.Length > 0)
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                DropZone.Background = (SolidColorBrush)FindResource("DropZoneActiveBrush");
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        DropZone.Background = (SolidColorBrush)FindResource("DropZoneBrush");

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var supportedFiles = files.Where(IsSupportedFile).ToArray();

            if (supportedFiles.Length > 1)
            {
                LoadMultipleFiles(supportedFiles);
            }
            else if (supportedFiles.Length == 1)
            {
                LoadFile(supportedFiles[0]);
            }
        }
    }

    private void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "MOBI files (*.mobi)|*.mobi|All files (*.*)|*.*",
            Title = "Select ebook file(s)",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            var supportedFiles = dialog.FileNames.Where(IsSupportedFile).ToArray();

            if (supportedFiles.Length > 1)
            {
                LoadMultipleFiles(supportedFiles);
            }
            else if (supportedFiles.Length == 1)
            {
                LoadFile(supportedFiles[0]);
            }
        }
    }

    private async void LoadFile(string filePath)
    {
        if (!IsSupportedFile(filePath))
        {
            SetStatus("Unsupported file format", isError: true);
            return;
        }

        // Reset to normal width for single file
        Width = NormalWidth;

        _currentFilePath = filePath;
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        FileNameText.Text = fileName;
        FileTypeText.Text = GetFileTypeDescription(extension);

        // Pre-fill title from filename (without extension) as fallback
        var titleGuess = Path.GetFileNameWithoutExtension(filePath);
        titleGuess = CleanupTitle(titleGuess);
        TitleTextBox.Text = titleGuess;
        AuthorTextBox.Text = "";

        DropZone.Visibility = Visibility.Collapsed;
        MultiFilePanel.Visibility = Visibility.Collapsed;
        FilePanel.Visibility = Visibility.Visible;

        SetStatus($"Loading: {fileName}...");

        if (extension == ".epub")
        {
            // EPUB files don't need conversion - go straight to send mode
            try
            {
                var metadata = await _conversionService.ExtractEpubMetadataAsync(filePath);
                if (!string.IsNullOrEmpty(metadata.Title))
                    TitleTextBox.Text = metadata.Title;
                if (!string.IsNullOrEmpty(metadata.Author))
                    AuthorTextBox.Text = metadata.Author;
            }
            catch
            {
                // Keep filename-based title
            }

            // Store for Kindle sending (the EPUB itself is the output)
            _lastOutputPath = filePath;
            _lastTitle = TitleTextBox.Text;
            _lastAuthor = AuthorTextBox.Text;

            // Skip convert button, show send panel directly
            ConvertButton.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            UpdateKindleUI();

            SetStatus("Ready to send to Kindle", isSuccess: true);
        }
        else if (extension == ".mobi")
        {
            // MOBI files need conversion
            ConvertButton.Visibility = Visibility.Visible;
            SuccessPanel.Visibility = Visibility.Collapsed;

            try
            {
                var metadata = await _conversionService.ExtractMobiMetadataAsync(filePath);
                if (!string.IsNullOrEmpty(metadata.Title))
                    TitleTextBox.Text = metadata.Title;
                if (!string.IsNullOrEmpty(metadata.Author))
                    AuthorTextBox.Text = metadata.Author;
                SetStatus("Metadata extracted from MOBI file");
            }
            catch
            {
                SetStatus($"Loaded: {fileName}");
            }
        }
        else
        {
            ConvertButton.Visibility = Visibility.Visible;
            SuccessPanel.Visibility = Visibility.Collapsed;
            SetStatus($"Loaded: {fileName}");
        }
    }

    private async void LoadMultipleFiles(string[] filePaths)
    {
        // Expand window for multi-file view
        Width = MultiFileWidth;

        _books.Clear();
        _lastBatchOutputPaths.Clear();

        DropZone.Visibility = Visibility.Collapsed;
        FilePanel.Visibility = Visibility.Collapsed;
        MultiFilePanel.Visibility = Visibility.Visible;

        SetStatus("Loading metadata...");

        int epubCount = 0;
        int mobiCount = 0;

        foreach (var filePath in filePaths)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var isEpub = ext == ".epub";

            if (isEpub) epubCount++;
            else mobiCount++;

            var book = new BookMetadata
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Title = CleanupTitle(Path.GetFileNameWithoutExtension(filePath)),
                Author = "",
                IsEpub = isEpub,
                Status = isEpub ? "Ready" : "Pending"
            };
            _books.Add(book);

            // For EPUBs, add to batch output paths immediately
            if (isEpub)
            {
                _lastBatchOutputPaths.Add(filePath);
            }
        }

        // Extract metadata for each file
        foreach (var book in _books)
        {
            try
            {
                if (book.IsEpub)
                {
                    var metadata = await _conversionService.ExtractEpubMetadataAsync(book.FilePath!);
                    if (!string.IsNullOrEmpty(metadata.Title))
                        book.Title = metadata.Title;
                    if (!string.IsNullOrEmpty(metadata.Author))
                        book.Author = metadata.Author;
                }
                else
                {
                    var metadata = await _conversionService.ExtractMobiMetadataAsync(book.FilePath!);
                    if (!string.IsNullOrEmpty(metadata.Title))
                        book.Title = metadata.Title;
                    if (!string.IsNullOrEmpty(metadata.Author))
                        book.Author = metadata.Author;
                }
            }
            catch
            {
                // Keep filename-based title
            }
        }

        // Determine UI state based on file types
        if (mobiCount > 0)
        {
            // Some files need conversion
            MultiFileHeader.Text = epubCount > 0
                ? $"{mobiCount} to convert, {epubCount} ready to send"
                : $"{filePaths.Length} books to convert";
            ConvertAllButton.Visibility = Visibility.Visible;
            BatchSuccessPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            // All files are EPUBs - skip to send mode
            MultiFileHeader.Text = $"{epubCount} books ready to send";
            ConvertAllButton.Visibility = Visibility.Collapsed;
            BatchSuccessPanel.Visibility = Visibility.Visible;
            UpdateKindleUI();
        }

        SetStatus($"Loaded {filePaths.Length} books");
    }

    private string CleanupTitle(string title)
    {
        // Remove common suffixes/patterns from ebook filenames
        title = System.Text.RegularExpressions.Regex.Replace(title, @"[-_]\d+$", ""); // Remove trailing numbers
        title = System.Text.RegularExpressions.Regex.Replace(title, @"[-_](epub|mobi|kindle|ebook)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        title = title.Replace("_", " ").Replace("-", " ");
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ").Trim();
        return title;
    }

    private void ClearFile_Click(object sender, RoutedEventArgs e)
    {
        _currentFilePath = null;
        _books.Clear();
        TitleTextBox.Text = "";
        AuthorTextBox.Text = "";

        // Reset to normal width
        Width = NormalWidth;

        FilePanel.Visibility = Visibility.Collapsed;
        MultiFilePanel.Visibility = Visibility.Collapsed;
        DropZone.Visibility = Visibility.Visible;

        SetStatus("Ready");
    }

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
            return;

        var title = TitleTextBox.Text.Trim();
        var author = AuthorTextBox.Text.Trim();

        if (string.IsNullOrEmpty(title))
        {
            SetStatus("Please enter a title", isError: true);
            TitleTextBox.Focus();
            return;
        }

        // Create safe filename
        var safeTitle = MakeSafeFilename(title);
        var safeAuthor = string.IsNullOrEmpty(author) ? "" : MakeSafeFilename(author);
        var outputFileName = string.IsNullOrEmpty(safeAuthor)
            ? $"{safeTitle}.epub"
            : $"{safeAuthor} - {safeTitle}.epub";

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "EPUB files (*.epub)|*.epub",
            FileName = outputFileName,
            Title = "Save EPUB file"
        };

        if (dialog.ShowDialog() != true)
            return;

        ConvertButton.IsEnabled = false;
        SetStatus("Converting...");

        try
        {
            var options = new ConversionOptions
            {
                Title = title,
                Author = author,
                InputPath = _currentFilePath,
                OutputPath = dialog.FileName
            };

            await _conversionService.ConvertAsync(options);

            SetStatus($"Saved: {Path.GetFileName(dialog.FileName)}", isSuccess: true);

            // Store for Kindle sending
            _lastOutputPath = dialog.FileName;
            _lastTitle = title;
            _lastAuthor = author;

            // Show success buttons
            ConvertButton.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            UpdateKindleUI();
        }
        catch (Exception ex)
        {
            SetStatus($"Conversion failed: {ex.Message}", isError: true);
            System.Windows.MessageBox.Show($"Conversion failed:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ConvertButton.IsEnabled = true;
        }
    }

    private async void ConvertAll_Click(object sender, RoutedEventArgs e)
    {
        // Ask for output folder
        var folderDialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select folder to save EPUB files",
            UseDescriptionForTitle = true
        };

        if (folderDialog.ShowDialog() != WinForms.DialogResult.OK)
            return;

        var outputFolder = folderDialog.SelectedPath;
        ConvertAllButton.IsEnabled = false;

        // Keep existing EPUB paths, clear only converted ones
        var existingEpubPaths = _lastBatchOutputPaths.Where(p =>
            _books.Any(b => b.IsEpub && b.FilePath == p)).ToList();
        _lastBatchOutputPaths.Clear();
        _lastBatchOutputPaths.AddRange(existingEpubPaths);

        int success = 0;
        int failed = 0;
        int skipped = 0;

        foreach (var book in _books)
        {
            // Skip EPUBs - they're already ready
            if (book.IsEpub)
            {
                skipped++;
                continue;
            }

            if (string.IsNullOrEmpty(book.Title))
            {
                book.Status = "No title";
                failed++;
                continue;
            }

            book.Status = "Converting...";
            SetStatus($"Converting: {book.Title}");

            try
            {
                var safeTitle = MakeSafeFilename(book.Title);
                var safeAuthor = string.IsNullOrEmpty(book.Author) ? "" : MakeSafeFilename(book.Author);
                var outputFileName = string.IsNullOrEmpty(safeAuthor)
                    ? $"{safeTitle}.epub"
                    : $"{safeAuthor} - {safeTitle}.epub";

                var outputPath = Path.Combine(outputFolder, outputFileName);

                var options = new ConversionOptions
                {
                    Title = book.Title,
                    Author = book.Author ?? "",
                    InputPath = book.FilePath!,
                    OutputPath = outputPath
                };

                await _conversionService.ConvertAsync(options);
                book.Status = "Done";
                _lastOutputPath = outputPath;
                _lastBatchOutputPaths.Add(outputPath);
                success++;
            }
            catch (Exception ex)
            {
                book.Status = "Failed";
                failed++;
                System.Diagnostics.Debug.WriteLine($"Failed to convert {book.FileName}: {ex.Message}");
            }
        }

        ConvertAllButton.IsEnabled = true;

        if (failed == 0)
        {
            var msg = skipped > 0
                ? $"Converted {success} books! ({skipped} EPUBs already ready)"
                : $"Converted {success} books successfully!";
            SetStatus(msg, isSuccess: true);
        }
        else
        {
            SetStatus($"Converted {success} books, {failed} failed", isError: failed > success);
        }

        // Show success buttons
        ConvertAllButton.Visibility = Visibility.Collapsed;
        BatchSuccessPanel.Visibility = Visibility.Visible;
        UpdateKindleUI();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastOutputPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_lastOutputPath}\"");
        }
    }

    private void ConvertAnother_Click(object sender, RoutedEventArgs e)
    {
        _currentFilePath = null;
        _lastOutputPath = null;
        _books.Clear();
        TitleTextBox.Text = "";
        AuthorTextBox.Text = "";
        ConvertButton.Visibility = Visibility.Visible;
        SuccessPanel.Visibility = Visibility.Collapsed;
        ConvertAllButton.Visibility = Visibility.Visible;
        BatchSuccessPanel.Visibility = Visibility.Collapsed;
        FilePanel.Visibility = Visibility.Collapsed;
        MultiFilePanel.Visibility = Visibility.Collapsed;
        DropZone.Visibility = Visibility.Visible;

        // Reset to normal width
        Width = NormalWidth;

        SetStatus("Ready");
    }


    private void SetStatus(string message, bool isError = false, bool isSuccess = false)
    {
        StatusText.Text = message;
        if (isError)
            StatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
        else if (isSuccess)
            StatusText.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
        else
            StatusText.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
    }

    private static bool IsSupportedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".mobi" || ext == ".epub";
    }

    private static string GetFileTypeDescription(string extension) => extension switch
    {
        ".mobi" => "Kindle MOBI File",
        ".epub" => "EPUB File",
        _ => "Unknown File"
    };

    private static string MakeSafeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("", name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    #region Kindle Integration


    private void KindleSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_kindleService.IsConfigured)
        {
            // Show logout option
            var confirmed = ConfirmDialog.Show(
                this,
                "Kindle Settings",
                "You are logged in to Amazon.\n\nWould you like to log out?",
                "Log out",
                "Cancel");

            if (confirmed)
            {
                _kindleService.ClearCookies();
                UpdateKindleUI();
                SetStatus("Logged out from Amazon");
            }
        }
        else
        {
            // Show login dialog
            var dialog = new AmazonLoginDialog(_kindleService)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                UpdateKindleUI();
                SetStatus("Logged in to Amazon!", isSuccess: true);
            }
        }
    }

    private async void SendToKindle_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastOutputPath) || !File.Exists(_lastOutputPath))
        {
            SetStatus("No file to send", isError: true);
            return;
        }

        await SendFileToKindleAsync(_lastOutputPath, _lastTitle ?? "Untitled", _lastAuthor ?? "");
    }

    private async void SendAllToKindle_Click(object sender, RoutedEventArgs e)
    {
        // Check if we have any books ready to send (Done = converted, Ready = EPUB)
        var booksToSend = _books.Where(b => b.Status == "Done" || b.Status == "Ready").ToList();

        if (booksToSend.Count == 0)
        {
            SetStatus("No files to send", isError: true);
            return;
        }

        SendAllToKindleButton.IsEnabled = false;
        int sent = 0;
        int failed = 0;

        foreach (var book in booksToSend)
        {
            string? outputPath;

            if (book.IsEpub)
            {
                // For EPUBs, use the original file path
                outputPath = book.FilePath;
            }
            else
            {
                // For converted files, find in batch output paths
                outputPath = _lastBatchOutputPaths.FirstOrDefault(p =>
                    Path.GetFileNameWithoutExtension(p).Contains(MakeSafeFilename(book.Title ?? "")));
            }

            if (outputPath == null || !File.Exists(outputPath))
            {
                failed++;
                continue;
            }

            book.Status = "Sending...";
            SetStatus($"Sending: {book.Title}");

            try
            {
                var result = await _kindleService.SendFileAsync(
                    outputPath,
                    book.Title ?? "Untitled",
                    book.Author ?? ""
                );

                if (result.Success)
                {
                    book.Status = "Sent!";
                    sent++;
                }
                else
                {
                    book.Status = "Send failed";
                    failed++;
                }
            }
            catch
            {
                book.Status = "Send failed";
                failed++;
            }
        }

        SendAllToKindleButton.IsEnabled = true;

        if (failed == 0)
        {
            SetStatus($"Sent {sent} books to Kindle!", isSuccess: true);
        }
        else
        {
            SetStatus($"Sent {sent} books, {failed} failed", isError: failed > sent);
        }
    }

    private async Task SendFileToKindleAsync(string filePath, string title, string author)
    {
        SendToKindleButton.IsEnabled = false;
        SetStatus("Sending to Kindle...");

        try
        {
            var result = await _kindleService.SendFileAsync(filePath, title, author);

            if (result.Success)
            {
                SetStatus("Sent to Kindle!", isSuccess: true);
            }
            else
            {
                SetStatus($"Failed: {result.Error}", isError: true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to send: {ex.Message}", isError: true);
        }
        finally
        {
            SendToKindleButton.IsEnabled = true;
        }
    }

    #endregion
}
