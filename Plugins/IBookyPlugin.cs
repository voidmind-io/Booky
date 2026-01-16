namespace Booky.Plugins;

/// <summary>
/// Base interface for all Booky plugins.
/// Plugins are loaded from DLLs in the Plugins folder.
/// </summary>
public interface IBookyPlugin
{
    /// <summary>
    /// Unique identifier for the plugin
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name shown in UI
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the plugin does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Plugin version
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Called when the plugin is loaded
    /// </summary>
    void Initialize();

    /// <summary>
    /// Called when the plugin is unloaded
    /// </summary>
    void Shutdown();
}

/// <summary>
/// Plugin that processes files before conversion.
/// Use case: DRM removal, format normalization, etc.
/// </summary>
public interface IPreConversionPlugin : IBookyPlugin
{
    /// <summary>
    /// File extensions this plugin can process (e.g., ".mobi", ".azw")
    /// </summary>
    string[] SupportedExtensions { get; }

    /// <summary>
    /// Process a file before conversion.
    /// </summary>
    /// <param name="inputPath">Path to the input file</param>
    /// <param name="outputPath">Path where processed file should be written</param>
    /// <returns>True if processing succeeded, false to skip this plugin</returns>
    Task<PluginResult> ProcessAsync(string inputPath, string outputPath);
}

/// <summary>
/// Plugin that processes files after conversion.
/// Use case: Metadata enhancement, cover generation, etc.
/// </summary>
public interface IPostConversionPlugin : IBookyPlugin
{
    /// <summary>
    /// Process a file after conversion to EPUB.
    /// </summary>
    /// <param name="epubPath">Path to the converted EPUB file</param>
    /// <returns>Result of the processing</returns>
    Task<PluginResult> ProcessAsync(string epubPath);
}

/// <summary>
/// Plugin that adds support for additional input formats.
/// </summary>
public interface IFormatPlugin : IBookyPlugin
{
    /// <summary>
    /// File extensions this plugin handles (e.g., ".azw3", ".kfx")
    /// </summary>
    string[] SupportedExtensions { get; }

    /// <summary>
    /// Description for the file type (e.g., "Kindle AZW3 File")
    /// </summary>
    string GetFileTypeDescription(string extension);

    /// <summary>
    /// Convert the input file to EPUB.
    /// </summary>
    Task<PluginResult> ConvertToEpubAsync(string inputPath, string outputPath, string title, string author);
}

public class PluginResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputPath { get; set; }

    public static PluginResult Ok(string? outputPath = null) => new() { Success = true, OutputPath = outputPath };
    public static PluginResult Fail(string error) => new() { Success = false, ErrorMessage = error };
    public static PluginResult Skip() => new() { Success = true }; // Plugin chose not to process
}
