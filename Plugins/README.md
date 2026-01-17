# Booky Plugins

Booky supports plugins to extend its functionality. Plugins are .NET DLLs placed in this folder.

## Plugin Types

| Interface | Purpose | Example Use Case |
|-----------|---------|------------------|
| `IPreConversionPlugin` | Process files before conversion | DRM removal, format normalization |
| `IPostConversionPlugin` | Process files after conversion | Metadata enhancement, cover generation |
| `IFormatPlugin` | Add support for new input formats | AZW3, KFX support |

## Creating a Plugin

1. Create a .NET Class Library project targeting `net8.0-windows`
2. Reference `Booky.exe` or copy `IBookyPlugin.cs` interfaces
3. Implement one or more plugin interfaces
4. Build and copy the DLL to the `Plugins` folder

### Example: Pre-Conversion Plugin

```csharp
using Booky.Plugins;

public class MyDrmPlugin : IPreConversionPlugin
{
    public string Id => "my-drm-plugin";
    public string Name => "My DRM Plugin";
    public string Description => "Removes DRM from ebooks";
    public string Version => "1.0.0";
    public string[] SupportedExtensions => new[] { ".mobi", ".azw" };

    public void Initialize() { }
    public void Shutdown() { }

    public async Task<PluginResult> ProcessAsync(string inputPath, string outputPath)
    {
        // Your DRM removal logic here
        // Write the processed file to outputPath

        return PluginResult.Ok(outputPath);
    }
}
```

### Example: Format Plugin

```csharp
using Booky.Plugins;

public class Azw3Plugin : IFormatPlugin
{
    public string Id => "azw3-plugin";
    public string Name => "AZW3 Support";
    public string Description => "Adds AZW3 format support";
    public string Version => "1.0.0";
    public string[] SupportedExtensions => new[] { ".azw3" };

    public void Initialize() { }
    public void Shutdown() { }

    public string GetFileTypeDescription(string extension) => "Kindle AZW3 File";

    public async Task<PluginResult> ConvertToEpubAsync(string inputPath, string outputPath, string title, string author)
    {
        // Your conversion logic here

        return PluginResult.Ok(outputPath);
    }
}
```

## Plugin Guidelines

- Plugins should fail gracefully and not crash Booky
- Return `PluginResult.Skip()` if the plugin can't process a file
- Return `PluginResult.Fail("error message")` on errors
- Plugins are loaded on startup from all `.dll` files in this folder
- Use async/await for long-running operations

## Legal Notice

Booky does not include or endorse any DRM removal functionality. Any plugins that remove DRM are the responsibility of the plugin author and user. Users should only use such plugins for content they legally own and have the right to convert.
