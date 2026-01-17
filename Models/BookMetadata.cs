using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace Booky.Models;

public class BookMetadata : INotifyPropertyChanged
{
    private string? _title;
    private string? _author;
    private string? _filePath;
    private string? _fileName;
    private string? _status;
    private bool _isEpub;
    private BitmapImage? _coverImage;

    public string? Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(nameof(Title)); }
    }

    public string? Author
    {
        get => _author;
        set { _author = value; OnPropertyChanged(nameof(Author)); }
    }

    public string? FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
    }

    public string? FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(nameof(FileName)); }
    }

    public string? Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public bool IsEpub
    {
        get => _isEpub;
        set { _isEpub = value; OnPropertyChanged(nameof(IsEpub)); }
    }

    public BitmapImage? CoverImage
    {
        get => _coverImage;
        set { _coverImage = value; OnPropertyChanged(nameof(CoverImage)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
