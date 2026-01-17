using Booky.Plugins;

namespace Booky.Plugins.Examples;

/// <summary>
/// Example pre-conversion plugin template.
/// This is a SKELETON - it does not contain any actual DRM removal code.
///
/// To create a real plugin:
/// 1. Create a new .NET Class Library project
/// 2. Copy this file as a starting point
/// 3. Implement your own processing logic
/// 4. Build and copy the DLL to Booky's Plugins folder
/// </summary>
public class ExamplePreConversionPlugin : IPreConversionPlugin
{
    public string Id => "example-pre-conversion";
    public string Name => "Example Pre-Conversion Plugin";
    public string Description => "Template for pre-conversion plugins (e.g., file preprocessing)";
    public string Version => "1.0.0";

    public string[] SupportedExtensions => new[] { ".mobi", ".azw", ".azw3" };

    public void Initialize()
    {
        // Called when plugin is loaded
        // Initialize any resources here
    }

    public void Shutdown()
    {
        // Called when plugin is unloaded
        // Clean up resources here
    }

    public async Task<PluginResult> ProcessAsync(string inputPath, string outputPath)
    {
        // This is where you would implement your processing logic
        //
        // For example:
        // 1. Read the input file
        // 2. Process it (decrypt, normalize, etc.)
        // 3. Write the result to outputPath
        // 4. Return PluginResult.Ok(outputPath) on success
        //
        // If you can't process this file, return PluginResult.Skip()
        // If there's an error, return PluginResult.Fail("error message")

        await Task.CompletedTask; // Placeholder for async operations

        // Default: skip processing (pass through unchanged)
        return PluginResult.Skip();
    }
}
