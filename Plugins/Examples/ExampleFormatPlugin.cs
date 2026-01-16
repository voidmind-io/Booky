using Booky.Plugins;

namespace Booky.Plugins.Examples;

/// <summary>
/// Example format plugin template.
/// Shows how to add support for additional ebook formats.
///
/// To create a real plugin:
/// 1. Create a new .NET Class Library project
/// 2. Copy this file as a starting point
/// 3. Implement your own conversion logic
/// 4. Build and copy the DLL to Booky's Plugins folder
/// </summary>
public class ExampleFormatPlugin : IFormatPlugin
{
    public string Id => "example-format";
    public string Name => "Example Format Plugin";
    public string Description => "Template for format plugins (adds new input format support)";
    public string Version => "1.0.0";

    public string[] SupportedExtensions => new[] { ".example" };

    public void Initialize()
    {
        // Called when plugin is loaded
    }

    public void Shutdown()
    {
        // Called when plugin is unloaded
    }

    public string GetFileTypeDescription(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".example" => "Example Ebook File",
            _ => "Unknown File"
        };
    }

    public async Task<PluginResult> ConvertToEpubAsync(string inputPath, string outputPath, string title, string author)
    {
        // This is where you would implement your conversion logic
        //
        // For example:
        // 1. Read and parse the input file
        // 2. Extract content, images, metadata
        // 3. Create an EPUB file at outputPath
        // 4. Return PluginResult.Ok(outputPath) on success
        //
        // You can use the same EPUB creation approach as Booky's built-in converter,
        // or use a library like EpubSharp, VersOne.Epub, etc.

        await Task.CompletedTask; // Placeholder for async operations

        // Default: return failure (not implemented)
        return PluginResult.Fail("This is an example plugin - conversion not implemented");
    }
}
