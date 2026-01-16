using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Booky.Services;

/// <summary>
/// Service for sending files to Kindle via Amazon's web API (same as Chrome extension).
/// Uses cookie-based authentication instead of device registration.
/// </summary>
public class KindleWebService
{
    private const string BaseUrl = "https://www.amazon.com/sendtokindle";
    // Mimic Chrome extension exactly
    private const string ExtName = "chrome_ocs";
    private const string ExtVersion = "2.1.1.7";

    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private string? _csrfToken;
    private DateTime _csrfTokenTime;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Booky", "kindle_web_cookies.json");

    public KindleWebService()
    {
        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        _httpClient = new HttpClient(handler);
        // Full Chrome user agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://www.amazon.com");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.amazon.com/sendtokindle");

        LoadCookies();
    }

    public bool IsConfigured => HasValidCookies();

    /// <summary>
    /// Import cookies from WebView2 after user logs in.
    /// </summary>
    public void ImportCookies(IEnumerable<(string Name, string Value, string Domain, string Path)> cookies)
    {
        foreach (var cookie in cookies)
        {
            try
            {
                _cookieContainer.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
            }
            catch
            {
                // Skip invalid cookies
            }
        }
        SaveCookies();
    }

    /// <summary>
    /// Check if we have valid Amazon session cookies.
    /// </summary>
    public bool HasValidCookies()
    {
        var amazonCookies = _cookieContainer.GetCookies(new Uri("https://www.amazon.com"));
        return amazonCookies.Cast<Cookie>().Any(c => c.Name == "session-id" || c.Name == "ubid-main");
    }

    /// <summary>
    /// Verify the session is valid by trying to get a CSRF token.
    /// </summary>
    public async Task<bool> VerifySessionAsync()
    {
        try
        {
            var token = await GetCsrfTokenAsync(forceRefresh: true);
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Send a file to Kindle.
    /// </summary>
    public async Task<SendResult> SendFileAsync(string filePath, string title, string author)
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Booky", "debug.log");
        void Log(string msg) => File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss} [KindleWeb] {msg}\n");

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return new SendResult { Success = false, Error = "File not found" };

            var fileExtension = fileInfo.Extension.TrimStart('.').ToLower();
            Log($"Sending file: {fileInfo.Name}, size: {fileInfo.Length}, ext: {fileExtension}");

            // 1. Get CSRF token
            var csrfToken = await GetCsrfTokenAsync();
            if (string.IsNullOrEmpty(csrfToken))
            {
                Log("Failed to get CSRF token - session may have expired");
                return new SendResult { Success = false, Error = "Session expired. Please log in again." };
            }
            Log($"Got CSRF token: {csrfToken[..Math.Min(20, csrfToken.Length)]}...");

            // 2. Call /init to get upload URL
            var initResult = await CallInitAsync(csrfToken, fileInfo.Length, fileExtension);
            if (!initResult.Success)
            {
                Log($"Init failed: {initResult.Error}");
                return new SendResult { Success = false, Error = initResult.Error };
            }
            Log($"Got upload URL and token");

            // 3. Upload file to S3
            var uploadSuccess = await UploadToS3Async(initResult.UploadUrl!, filePath, fileInfo.Length);
            if (!uploadSuccess)
            {
                Log("S3 upload failed");
                return new SendResult { Success = false, Error = "Failed to upload file" };
            }
            Log("File uploaded to S3");

            // 4. Call /send-v2 to trigger delivery
            var sendResult = await CallSendAsync(csrfToken, initResult.StkToken!, title, author, fileExtension, fileInfo.Length);
            Log($"Send result: {(sendResult.Success ? "Success" : sendResult.Error)}");

            return sendResult;
        }
        catch (Exception ex)
        {
            Log($"Exception: {ex.Message}");
            return new SendResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<string?> GetCsrfTokenAsync(bool forceRefresh = false)
    {
        // Return cached token if still valid (< 60 seconds old)
        if (!forceRefresh && !string.IsNullOrEmpty(_csrfToken) &&
            (DateTime.Now - _csrfTokenTime).TotalSeconds < 60)
        {
            return _csrfToken;
        }

        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/empty");
            if (!response.IsSuccessStatusCode)
                return null;

            var html = await response.Content.ReadAsStringAsync();

            // Extract CSRF token: name='csrfToken' value='...' />
            var match = Regex.Match(html, @"name='csrfToken'\s+value='([^']+)'");
            if (match.Success)
            {
                _csrfToken = match.Groups[1].Value;
                _csrfTokenTime = DateTime.Now;
                return _csrfToken;
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private async Task<InitResult> CallInitAsync(string csrfToken, long fileSize, string fileExtension)
    {
        var requestBody = new
        {
            extName = ExtName,
            appVersion = ExtVersion,
            fileSize = fileSize,
            fileExtension = fileExtension
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/init")
        {
            Content = content
        };
        request.Headers.Add("anti-csrftoken-a2z", csrfToken);
        request.Headers.Add("Accept", "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new InitResult { Success = false, Error = $"Init API failed: {response.StatusCode}" };
        }

        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseText);
            var uploadUrl = json.GetProperty("uploadUrl").GetString();
            var stkToken = json.GetProperty("stkToken").GetString();

            if (string.IsNullOrEmpty(uploadUrl) || string.IsNullOrEmpty(stkToken))
            {
                return new InitResult { Success = false, Error = "Invalid init response" };
            }

            return new InitResult { Success = true, UploadUrl = uploadUrl, StkToken = stkToken };
        }
        catch (Exception ex)
        {
            return new InitResult { Success = false, Error = $"Failed to parse init response: {ex.Message}" };
        }
    }

    private async Task<bool> UploadToS3Async(string uploadUrl, string filePath, long fileSize)
    {
        using var fileStream = File.OpenRead(filePath);
        using var content = new StreamContent(fileStream);
        content.Headers.ContentLength = fileSize;

        // S3 presigned URL upload - no special headers needed
        var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private async Task<SendResult> CallSendAsync(string csrfToken, string stkToken, string title, string author, string fileExtension, long fileSize)
    {
        // Match Chrome extension format exactly
        var requestBody = new Dictionary<string, object>
        {
            ["extName"] = ExtName,
            ["extVersion"] = ExtVersion,
            ["inputFormat"] = fileExtension,
            ["stkToken"] = stkToken,
            ["title"] = title,
            ["dataType"] = "file",
            ["archive"] = false,
            ["deviceList"] = new string[0], // Empty = send to all
            ["fileSize"] = fileSize,
            ["inputFileName"] = $"{title}.{fileExtension}",
            ["batchId"] = Guid.NewGuid().ToString("N")[..16]
        };

        if (!string.IsNullOrEmpty(author))
        {
            requestBody["author"] = author;
        }

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/send-v2")
        {
            Content = content
        };
        request.Headers.Add("anti-csrftoken-a2z", csrfToken);
        request.Headers.Add("Accept", "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // Clean up error message - don't show raw HTML
            var error = responseText.Contains("<html")
                ? $"Amazon returned error {response.StatusCode}"
                : responseText;
            return new SendResult { Success = false, Error = error };
        }

        return new SendResult { Success = true };
    }

    private void SaveCookies()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var amazonCookies = _cookieContainer.GetCookies(new Uri("https://www.amazon.com"));
            var cookieList = amazonCookies.Cast<Cookie>()
                .Select(c => new CookieData
                {
                    Name = c.Name,
                    Value = c.Value,
                    Domain = c.Domain,
                    Path = c.Path,
                    Expires = c.Expires
                })
                .ToList();

            var json = JsonSerializer.Serialize(cookieList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private void LoadCookies()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;

            var json = File.ReadAllText(SettingsPath);
            var cookieList = JsonSerializer.Deserialize<List<CookieData>>(json);

            if (cookieList == null)
                return;

            foreach (var c in cookieList)
            {
                // Skip expired cookies
                if (c.Expires != DateTime.MinValue && c.Expires < DateTime.Now)
                    continue;

                try
                {
                    _cookieContainer.Add(new Cookie(c.Name, c.Value, c.Path, c.Domain)
                    {
                        Expires = c.Expires
                    });
                }
                catch
                {
                    // Skip invalid cookies
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    public void ClearCookies()
    {
        try
        {
            if (File.Exists(SettingsPath))
                File.Delete(SettingsPath);
        }
        catch
        {
            // Ignore
        }

        // Create a new cookie container (can't clear existing one)
        // Note: This requires restarting the service
    }

    private class CookieData
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Domain { get; set; } = "";
        public string Path { get; set; } = "/";
        public DateTime Expires { get; set; }
    }

    private class InitResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? UploadUrl { get; set; }
        public string? StkToken { get; set; }
    }

    public class SendResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
