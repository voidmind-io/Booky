using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;
using Booky.Models;
using HtmlAgilityPack;

namespace Booky.Services;

public class ConversionService
{
    /// <summary>
    /// Extract metadata (title, author) from an EPUB file by reading the OPF
    /// </summary>
    public async Task<BookMetadata> ExtractEpubMetadataAsync(string inputPath)
    {
        var metadata = new BookMetadata();

        try
        {
            using var archive = ZipFile.OpenRead(inputPath);

            // Find the OPF file (usually OEBPS/content.opf or similar)
            var opfEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));

            if (opfEntry == null)
                return metadata;

            using var stream = opfEntry.Open();
            using var reader = new StreamReader(stream);
            var opfContent = await reader.ReadToEndAsync();

            // Parse title
            var titleMatch = System.Text.RegularExpressions.Regex.Match(
                opfContent, @"<dc:title[^>]*>([^<]+)</dc:title>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (titleMatch.Success)
                metadata.Title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());

            // Parse author/creator
            var authorMatch = System.Text.RegularExpressions.Regex.Match(
                opfContent, @"<dc:creator[^>]*>([^<]+)</dc:creator>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (authorMatch.Success)
                metadata.Author = System.Net.WebUtility.HtmlDecode(authorMatch.Groups[1].Value.Trim());
        }
        catch
        {
            // Silently fail - metadata extraction is best-effort
        }

        return metadata;
    }

    /// <summary>
    /// Extract cover image from an EPUB file
    /// </summary>
    public BitmapImage? ExtractEpubCover(string inputPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(inputPath);

            // Find the OPF file to locate the cover
            var opfEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));

            if (opfEntry == null)
                return null;

            string? coverPath = null;
            var opfDir = Path.GetDirectoryName(opfEntry.FullName)?.Replace('\\', '/') ?? "";

            using (var stream = opfEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                var opfContent = reader.ReadToEnd();

                // Try multiple patterns to find the cover

                // Pattern 1: properties="cover-image" (EPUB3 standard)
                var coverMatch = System.Text.RegularExpressions.Regex.Match(
                    opfContent, @"<item[^>]*properties\s*=\s*[""'][^""']*cover-image[^""']*[""'][^>]*href\s*=\s*[""']([^""']+)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                if (!coverMatch.Success)
                {
                    // Pattern 1b: href before properties
                    coverMatch = System.Text.RegularExpressions.Regex.Match(
                        opfContent, @"<item[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*properties\s*=\s*[""'][^""']*cover-image[^""']*[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                }

                if (!coverMatch.Success)
                {
                    // Pattern 2: id="cover" or id="cover-image"
                    coverMatch = System.Text.RegularExpressions.Regex.Match(
                        opfContent, @"<item[^>]*id\s*=\s*[""']cover[^""']*[""'][^>]*href\s*=\s*[""']([^""']+)[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                }

                if (!coverMatch.Success)
                {
                    // Pattern 2b: href before id
                    coverMatch = System.Text.RegularExpressions.Regex.Match(
                        opfContent, @"<item[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*id\s*=\s*[""']cover[^""']*[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                }

                if (!coverMatch.Success)
                {
                    // Pattern 3: meta name="cover" content="image-id", then find that image
                    var metaCoverMatch = System.Text.RegularExpressions.Regex.Match(
                        opfContent, @"<meta[^>]*name\s*=\s*[""']cover[""'][^>]*content\s*=\s*[""']([^""']+)[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (!metaCoverMatch.Success)
                    {
                        // Try reversed attribute order
                        metaCoverMatch = System.Text.RegularExpressions.Regex.Match(
                            opfContent, @"<meta[^>]*content\s*=\s*[""']([^""']+)[""'][^>]*name\s*=\s*[""']cover[""']",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }

                    if (metaCoverMatch.Success)
                    {
                        var coverId = metaCoverMatch.Groups[1].Value;
                        // Find item with this id
                        coverMatch = System.Text.RegularExpressions.Regex.Match(
                            opfContent, $@"<item[^>]*id\s*=\s*[""']{System.Text.RegularExpressions.Regex.Escape(coverId)}[""'][^>]*href\s*=\s*[""']([^""']+)[""']",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                        if (!coverMatch.Success)
                        {
                            coverMatch = System.Text.RegularExpressions.Regex.Match(
                                opfContent, $@"<item[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*id\s*=\s*[""']{System.Text.RegularExpressions.Regex.Escape(coverId)}[""']",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                        }
                    }
                }

                if (coverMatch.Success)
                {
                    coverPath = coverMatch.Groups[1].Value;
                }
            }

            if (string.IsNullOrEmpty(coverPath))
            {
                // Fallback: look for common cover image names anywhere in the archive
                var coverEntry = archive.Entries.FirstOrDefault(e =>
                {
                    var name = e.Name.ToLowerInvariant();
                    return name.StartsWith("cover") && (name.EndsWith(".jpg") || name.EndsWith(".jpeg") || name.EndsWith(".png"));
                });

                if (coverEntry != null)
                {
                    return LoadBitmapFromEntry(coverEntry);
                }

                // Last resort: find any image in Images/covers folder
                coverEntry = archive.Entries.FirstOrDefault(e =>
                {
                    var path = e.FullName.ToLowerInvariant();
                    var ext = Path.GetExtension(path);
                    return (path.Contains("cover") || path.Contains("images")) &&
                           (ext == ".jpg" || ext == ".jpeg" || ext == ".png");
                });

                if (coverEntry != null)
                {
                    return LoadBitmapFromEntry(coverEntry);
                }

                return null;
            }

            // Resolve relative path from OPF location
            var fullCoverPath = string.IsNullOrEmpty(opfDir)
                ? coverPath
                : $"{opfDir}/{coverPath}";

            // Normalize path
            fullCoverPath = fullCoverPath.Replace('\\', '/').TrimStart('/');

            var foundEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Replace('\\', '/').Equals(fullCoverPath, StringComparison.OrdinalIgnoreCase));

            // If not found, try without the OPF directory prefix
            if (foundEntry == null)
            {
                foundEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.Replace('\\', '/').EndsWith(coverPath, StringComparison.OrdinalIgnoreCase));
            }

            if (foundEntry == null)
                return null;

            return LoadBitmapFromEntry(foundEntry);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? LoadBitmapFromEntry(ZipArchiveEntry entry)
    {
        try
        {
            using var coverStream = entry.Open();
            using var memoryStream = new MemoryStream();
            coverStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memoryStream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract cover image from a MOBI file using mobitool
    /// </summary>
    public BitmapImage? ExtractMobiCover(string inputPath)
    {
        var toolPath = FindMobiTool();
        if (toolPath == null)
            return null;

        // Create temp directory for extraction
        var tempDir = Path.Combine(Path.GetTempPath(), $"booky_cover_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var tempMobiPath = Path.Combine(tempDir, "input.mobi");
            File.Copy(inputPath, tempMobiPath);

            // Run mobitool to dump source files (includes cover)
            var psi = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = $"-s \"{tempMobiPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            process.WaitForExit(5000); // 5 second timeout for cover extraction

            var markupDir = Path.Combine(tempDir, "input_markup");
            if (!Directory.Exists(markupDir))
                return null;

            // Look for cover image - mobitool typically extracts it as cover.jpg or similar
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var coverFile = Directory.GetFiles(markupDir)
                .FirstOrDefault(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return imageExtensions.Contains(ext) &&
                           (name.Contains("cover") || name == "image00000" || name == "image00001");
                });

            // If no cover found, try the first image
            if (coverFile == null)
            {
                coverFile = Directory.GetFiles(markupDir)
                    .FirstOrDefault(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
            }

            if (coverFile == null)
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(coverFile);
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch
        {
            return null;
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Extract metadata (title, author) from a MOBI file using mobitool
    /// </summary>
    public async Task<BookMetadata> ExtractMobiMetadataAsync(string inputPath)
    {
        var toolPath = FindMobiTool();
        if (toolPath == null)
            return new BookMetadata();

        var psi = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = $"\"{inputPath}\"",  // No flags = print metadata
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            return new BookMetadata();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return ParseMobiToolOutput(output);
    }

    private static BookMetadata ParseMobiToolOutput(string output)
    {
        var metadata = new BookMetadata();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
            {
                metadata.Title = line["Title:".Length..].Trim();
            }
            else if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
            {
                metadata.Author = line["Author:".Length..].Trim();
            }
        }

        return metadata;
    }

    public async Task ConvertAsync(ConversionOptions options, PluginService? pluginService = null)
    {
        var inputPath = options.InputPath;
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        string? tempInputPath = null;

        try
        {
            // Run pre-conversion plugins (e.g., DRM removal)
            if (pluginService != null)
            {
                inputPath = await pluginService.RunPreConversionPluginsAsync(inputPath);

                // Track if plugins created a temp file
                if (inputPath != options.InputPath)
                    tempInputPath = inputPath;
            }

            // Try format plugins first
            if (pluginService != null)
            {
                var pluginResult = await pluginService.ConvertWithPluginAsync(inputPath, options.OutputPath, options.Title, options.Author ?? "");
                if (pluginResult != null)
                {
                    if (!pluginResult.Success)
                        throw new Exception(pluginResult.ErrorMessage ?? "Plugin conversion failed");

                    // Run post-conversion plugins
                    await pluginService.RunPostConversionPluginsAsync(options.OutputPath);
                    return;
                }
            }

            // Built-in conversion
            if (ext != ".mobi")
                throw new NotSupportedException($"Unsupported format: {ext}. Only MOBI files are supported.");

            var modifiedOptions = new ConversionOptions
            {
                InputPath = inputPath,
                OutputPath = options.OutputPath,
                Title = options.Title,
                Author = options.Author
            };

            await ConvertMobiToEpubAsync(modifiedOptions);

            // Run post-conversion plugins
            if (pluginService != null)
            {
                await pluginService.RunPostConversionPluginsAsync(options.OutputPath);
            }
        }
        finally
        {
            // Clean up temp file created by pre-conversion plugins
            if (tempInputPath != null && File.Exists(tempInputPath))
            {
                try { File.Delete(tempInputPath); } catch { }
            }
        }
    }


    private async Task ConvertMobiToEpubAsync(ConversionOptions options)
    {
        // Find mobitool.exe
        var toolPath = FindMobiTool();
        if (toolPath == null)
        {
            throw new FileNotFoundException(
                "mobitool.exe not found. Please place it in the Tools folder next to Booky.exe");
        }

        // mobitool creates {filename}_markup folder NEXT TO the input file (ignores -o flag)
        // So we copy the input to a temp location with a simple name
        var tempDir = Path.Combine(Path.GetTempPath(), $"booky_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var tempMobiPath = Path.Combine(tempDir, "input.mobi");
        var markupDir = Path.Combine(tempDir, "input_markup");

        File.Copy(options.InputPath, tempMobiPath);

        try
        {
            // Run mobitool to dump source files
            var psi = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = $"-s \"{tempMobiPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new Exception("Failed to start mobitool");

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"mobitool failed: {error}");
            }

            // mobitool creates input_markup folder next to input.mobi
            if (!Directory.Exists(markupDir))
            {
                throw new Exception($"mobitool did not create expected output folder");
            }

            // Find the extracted HTML file(s)
            var htmlFiles = Directory.GetFiles(markupDir, "*.html", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(markupDir, "*.htm", SearchOption.AllDirectories))
                .ToList();

            if (htmlFiles.Count == 0)
            {
                throw new Exception("No HTML content extracted from MOBI file");
            }

            // Combine all HTML files
            var sb = new StringBuilder();
            foreach (var htmlFile in htmlFiles.OrderBy(f => f))
            {
                var content = await File.ReadAllTextAsync(htmlFile);
                sb.AppendLine(content);
            }

            // Collect all images
            var images = new Dictionary<string, byte[]>();
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg" };
            foreach (var imageFile in Directory.GetFiles(markupDir))
            {
                var ext = Path.GetExtension(imageFile).ToLowerInvariant();
                if (imageExtensions.Contains(ext))
                {
                    var fileName = Path.GetFileName(imageFile);
                    images[fileName] = await File.ReadAllBytesAsync(imageFile);
                }
            }

            await CreateEpubAsync(options, sb.ToString(), images);
        }
        finally
        {
            // Cleanup entire temp directory
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static string? FindMobiTool()
    {
        // Check various locations
        var locations = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "mobitool.exe"),
            Path.Combine(AppContext.BaseDirectory, "mobitool.exe"),
            Path.Combine(Environment.CurrentDirectory, "Tools", "mobitool.exe"),
            Path.Combine(Environment.CurrentDirectory, "mobitool.exe"),
        };

        return locations.FirstOrDefault(File.Exists);
    }





    private static string EscapeHtml(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }

    private static async Task CreateEpubAsync(ConversionOptions options, string htmlContent, Dictionary<string, byte[]>? images)
    {
        // Clean up HTML content
        var cleanHtml = CleanupHtml(htmlContent);

        // Create EPUB structure in memory
        using var stream = new FileStream(options.OutputPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        // 1. mimetype (must be first, uncompressed)
        var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mimetypeEntry.Open()))
        {
            await writer.WriteAsync("application/epub+zip");
        }

        // 2. META-INF/container.xml
        var containerEntry = archive.CreateEntry("META-INF/container.xml");
        using (var writer = new StreamWriter(containerEntry.Open()))
        {
            await writer.WriteAsync("""
                <?xml version="1.0" encoding="UTF-8"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """);
        }

        // Build manifest entries for images
        var imageManifest = new StringBuilder();
        if (images != null)
        {
            int i = 0;
            foreach (var img in images.Keys)
            {
                var mediaType = GetImageMediaType(img);
                imageManifest.AppendLine($"    <item id=\"img{i}\" href=\"images/{img}\" media-type=\"{mediaType}\"/>");
                i++;
            }
        }

        // 3. OEBPS/content.opf
        var bookId = Guid.NewGuid().ToString();
        var opfEntry = archive.CreateEntry("OEBPS/content.opf");
        using (var writer = new StreamWriter(opfEntry.Open()))
        {
            await writer.WriteAsync($"""
                <?xml version="1.0" encoding="UTF-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" unique-identifier="bookid" version="3.0">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="bookid">{bookId}</dc:identifier>
                    <dc:title>{EscapeHtml(options.Title)}</dc:title>
                    <dc:creator>{EscapeHtml(options.Author ?? "Unknown")}</dc:creator>
                    <dc:language>en</dc:language>
                    <meta property="dcterms:modified">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</meta>
                  </metadata>
                  <manifest>
                    <item id="content" href="content.xhtml" media-type="application/xhtml+xml"/>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="style" href="style.css" media-type="text/css"/>
                {imageManifest}  </manifest>
                  <spine>
                    <itemref idref="content"/>
                  </spine>
                </package>
                """);
        }

        // 4. OEBPS/nav.xhtml (navigation)
        var navEntry = archive.CreateEntry("OEBPS/nav.xhtml");
        using (var writer = new StreamWriter(navEntry.Open()))
        {
            await writer.WriteAsync($"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
                <head>
                  <title>{EscapeHtml(options.Title)}</title>
                  <link rel="stylesheet" type="text/css" href="style.css"/>
                </head>
                <body>
                  <nav epub:type="toc">
                    <h1>Contents</h1>
                    <ol>
                      <li><a href="content.xhtml">{EscapeHtml(options.Title)}</a></li>
                    </ol>
                  </nav>
                </body>
                </html>
                """);
        }

        // 5. OEBPS/style.css
        var styleEntry = archive.CreateEntry("OEBPS/style.css");
        using (var writer = new StreamWriter(styleEntry.Open()))
        {
            await writer.WriteAsync("""
                body {
                  font-family: Georgia, "Times New Roman", serif;
                  line-height: 1.6;
                  margin: 1em;
                  text-align: justify;
                }
                h1 {
                  font-size: 1.8em;
                  margin-top: 1em;
                  margin-bottom: 0.5em;
                  text-align: center;
                }
                h2 {
                  font-size: 1.4em;
                  margin-top: 1em;
                  margin-bottom: 0.4em;
                }
                h3 {
                  font-size: 1.2em;
                  margin-top: 0.8em;
                  margin-bottom: 0.3em;
                }
                p {
                  margin-top: 0.5em;
                  margin-bottom: 0.5em;
                  text-indent: 1.5em;
                }
                img {
                  max-width: 100%;
                  height: auto;
                }
                """);
        }

        // 6. OEBPS/content.xhtml (main content)
        // Fix image paths to point to images/ folder
        var contentHtml = cleanHtml;
        if (images != null)
        {
            foreach (var imgName in images.Keys)
            {
                // Replace various possible image references
                contentHtml = contentHtml.Replace($"src=\"{imgName}\"", $"src=\"images/{imgName}\"");
                contentHtml = contentHtml.Replace($"src='{imgName}'", $"src='images/{imgName}'");
            }
        }

        var contentEntry = archive.CreateEntry("OEBPS/content.xhtml");
        using (var writer = new StreamWriter(contentEntry.Open()))
        {
            await writer.WriteAsync($"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml">
                <head>
                  <title>{EscapeHtml(options.Title)}</title>
                  <link rel="stylesheet" type="text/css" href="style.css"/>
                </head>
                <body>
                  {contentHtml}
                </body>
                </html>
                """);
        }

        // 7. Add images
        if (images != null)
        {
            foreach (var (name, data) in images)
            {
                var imgEntry = archive.CreateEntry($"OEBPS/images/{name}");
                using var imgStream = imgEntry.Open();
                await imgStream.WriteAsync(data);
            }
        }
    }

    private static string GetImageMediaType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

    private static string CleanupHtml(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        // Extract just the body content
        var body = doc.DocumentNode.SelectSingleNode("//body");
        if (body != null)
        {
            return body.InnerHtml;
        }

        // If no body tag, return as-is but strip html/head tags
        var content = doc.DocumentNode.InnerHtml;

        // Simple cleanup - remove doctype, html, head, body tags
        content = System.Text.RegularExpressions.Regex.Replace(content, @"<!DOCTYPE[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content, @"</?html[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content, @"<head>.*?</head>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        content = System.Text.RegularExpressions.Regex.Replace(content, @"</?body[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return content.Trim();
    }
}
