using System.IO;
using System.Reflection;
using Booky.Plugins;

namespace Booky.Services;

public class PluginService
{
    private readonly List<IBookyPlugin> _plugins = new();
    private readonly string _pluginsPath;

    public IReadOnlyList<IBookyPlugin> Plugins => _plugins.AsReadOnly();
    public IEnumerable<IPreConversionPlugin> PreConversionPlugins => _plugins.OfType<IPreConversionPlugin>();
    public IEnumerable<IPostConversionPlugin> PostConversionPlugins => _plugins.OfType<IPostConversionPlugin>();
    public IEnumerable<IFormatPlugin> FormatPlugins => _plugins.OfType<IFormatPlugin>();

    public PluginService()
    {
        // Plugins folder next to the executable
        var exeDir = AppContext.BaseDirectory;
        _pluginsPath = Path.Combine(exeDir, "Plugins");
    }

    public void LoadPlugins()
    {
        if (!Directory.Exists(_pluginsPath))
        {
            Directory.CreateDirectory(_pluginsPath);
            return;
        }

        var dllFiles = Directory.GetFiles(_pluginsPath, "*.dll");

        foreach (var dllPath in dllFiles)
        {
            try
            {
                LoadPluginFromDll(dllPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load plugin {dllPath}: {ex.Message}");
            }
        }
    }

    private void LoadPluginFromDll(string dllPath)
    {
        var assembly = Assembly.LoadFrom(dllPath);

        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IBookyPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in pluginTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is IBookyPlugin plugin)
                {
                    plugin.Initialize();
                    _plugins.Add(plugin);
                    System.Diagnostics.Debug.WriteLine($"Loaded plugin: {plugin.Name} v{plugin.Version}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize plugin {type.Name}: {ex.Message}");
            }
        }
    }

    public void UnloadPlugins()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Shutdown();
            }
            catch
            {
                // Ignore shutdown errors
            }
        }
        _plugins.Clear();
    }

    /// <summary>
    /// Check if any plugin supports the given file extension
    /// </summary>
    public bool IsPluginSupportedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return PreConversionPlugins.Any(p => p.SupportedExtensions.Contains(ext))
            || FormatPlugins.Any(p => p.SupportedExtensions.Contains(ext));
    }

    /// <summary>
    /// Get file type description from plugins
    /// </summary>
    public string? GetPluginFileTypeDescription(string extension)
    {
        var formatPlugin = FormatPlugins.FirstOrDefault(p => p.SupportedExtensions.Contains(extension.ToLowerInvariant()));
        return formatPlugin?.GetFileTypeDescription(extension);
    }

    /// <summary>
    /// Run pre-conversion plugins on a file
    /// </summary>
    public async Task<string> RunPreConversionPluginsAsync(string inputPath)
    {
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var currentPath = inputPath;

        foreach (var plugin in PreConversionPlugins.Where(p => p.SupportedExtensions.Contains(ext)))
        {
            var tempOutput = Path.Combine(Path.GetTempPath(), $"booky_pre_{Guid.NewGuid()}{ext}");

            try
            {
                var result = await plugin.ProcessAsync(currentPath, tempOutput);

                if (result.Success && !string.IsNullOrEmpty(result.OutputPath))
                {
                    // Clean up previous temp file if it's not the original
                    if (currentPath != inputPath && File.Exists(currentPath))
                        File.Delete(currentPath);

                    currentPath = result.OutputPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Plugin {plugin.Name} failed: {ex.Message}");
            }
        }

        return currentPath;
    }

    /// <summary>
    /// Run post-conversion plugins on an EPUB file
    /// </summary>
    public async Task RunPostConversionPluginsAsync(string epubPath)
    {
        foreach (var plugin in PostConversionPlugins)
        {
            try
            {
                await plugin.ProcessAsync(epubPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Plugin {plugin.Name} failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Convert using a format plugin
    /// </summary>
    public async Task<PluginResult?> ConvertWithPluginAsync(string inputPath, string outputPath, string title, string author)
    {
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var plugin = FormatPlugins.FirstOrDefault(p => p.SupportedExtensions.Contains(ext));

        if (plugin == null)
            return null;

        return await plugin.ConvertToEpubAsync(inputPath, outputPath, title, author);
    }
}
