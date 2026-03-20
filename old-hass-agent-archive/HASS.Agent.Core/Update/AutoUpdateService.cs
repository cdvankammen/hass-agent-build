using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace HASS.Agent.Core.Update
{
    /// <summary>
    /// Cross-platform auto-update service for HASS Agent
    /// </summary>
    public class AutoUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly string _currentVersion;
        private readonly string _updateUrl;
        private readonly string _downloadPath;
        
        public event EventHandler<UpdateAvailableEventArgs>? UpdateAvailable;
        public event EventHandler<UpdateProgressEventArgs>? DownloadProgress;
        public event EventHandler<UpdateCompletedEventArgs>? UpdateCompleted;
        
        public AutoUpdateService(string currentVersion, string updateUrl = "https://api.github.com/repos/your-repo/hass-agent/releases/latest")
        {
            _currentVersion = currentVersion;
            _updateUrl = updateUrl;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"HASS.Agent/{currentVersion}");
            
            // Platform-specific download path
            _downloadPath = GetDownloadPath();
        }
        
        private static string GetDownloadPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HASSAgent", "Updates");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "HASSAgent", "Updates");
            else
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hass-agent", "updates");
        }
        
        /// <summary>
        /// Check for updates from GitHub releases
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(_updateUrl);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (release == null) return null;
                
                var latestVersion = release.TagName?.TrimStart('v') ?? "0.0.0";
                
                if (IsNewerVersion(latestVersion, _currentVersion))
                {
                    var updateInfo = new UpdateInfo
                    {
                        CurrentVersion = _currentVersion,
                        NewVersion = latestVersion,
                        ReleaseNotes = release.Body ?? "",
                        ReleaseUrl = release.HtmlUrl ?? "",
                        DownloadUrl = GetPlatformSpecificDownloadUrl(release),
                        PublishedAt = release.PublishedAt
                    };
                    
                    UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(updateInfo));
                    return updateInfo;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                return null;
            }
        }
        
        private string? GetPlatformSpecificDownloadUrl(GitHubRelease release)
        {
            if (release.Assets == null) return null;
            
            string pattern;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                pattern = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                pattern = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            else
                pattern = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            
            foreach (var asset in release.Assets)
            {
                if (asset.Name?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true)
                    return asset.BrowserDownloadUrl;
            }
            
            return null;
        }
        
        /// <summary>
        /// Download update package
        /// </summary>
        public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo)
        {
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
                return null;
            
            try
            {
                Directory.CreateDirectory(_downloadPath);
                
                var fileName = Path.GetFileName(new Uri(updateInfo.DownloadUrl).AbsolutePath);
                var localPath = Path.Combine(_downloadPath, fileName);
                
                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;
                
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                
                var buffer = new byte[8192];
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloadedBytes += bytesRead;
                    
                    if (totalBytes > 0)
                    {
                        var progress = (int)((downloadedBytes * 100) / totalBytes);
                        DownloadProgress?.Invoke(this, new UpdateProgressEventArgs(progress, downloadedBytes, totalBytes));
                    }
                }
                
                return localPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading update: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Verify downloaded file checksum
        /// </summary>
        public async Task<bool> VerifyChecksumAsync(string filePath, string expectedSha256)
        {
            try
            {
                using var sha256 = SHA256.Create();
                await using var stream = File.OpenRead(filePath);
                var hash = await sha256.ComputeHashAsync(stream);
                var actualHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return actualHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Apply downloaded update
        /// </summary>
        public void ApplyUpdate(string updatePackagePath)
        {
            var installScript = GetInstallScript(updatePackagePath);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, use a batch script to replace files after exit
                var scriptPath = Path.Combine(_downloadPath, "update.bat");
                File.WriteAllText(scriptPath, installScript);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else
            {
                // On Unix, use a shell script
                var scriptPath = Path.Combine(_downloadPath, "update.sh");
                File.WriteAllText(scriptPath, installScript);
                
                // Make executable
                Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit();
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{scriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            
            UpdateCompleted?.Invoke(this, new UpdateCompletedEventArgs(true, "Update started. Application will restart."));
        }
        
        private string GetInstallScript(string updatePackagePath)
        {
            var appPath = AppContext.BaseDirectory;
            var processId = Environment.ProcessId;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $@"@echo off
timeout /t 2 /nobreak >nul
taskkill /pid {processId} /f >nul 2>&1
timeout /t 1 /nobreak >nul

if ""{updatePackagePath}"" ENDSWITH "".msi"" (
    msiexec /i ""{updatePackagePath}"" /quiet
) else (
    REM Extract zip and copy files
    powershell -Command ""Expand-Archive -Path '{updatePackagePath}' -DestinationPath '{appPath}' -Force""
)

start """" ""{appPath}\HASS.Agent.Desktop.exe""
del ""%~f0""
";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $@"#!/bin/bash
sleep 2
kill {processId} 2>/dev/null || true
sleep 1

# Mount and copy from DMG
if [[ ""{updatePackagePath}"" == *.dmg ]]; then
    hdiutil attach ""{updatePackagePath}"" -nobrowse -quiet
    MOUNT_POINT=$(hdiutil info | grep ""/Volumes/HASS"" | awk '{{print $NF}}')
    cp -R ""$MOUNT_POINT/HASS Agent.app"" /Applications/
    hdiutil detach ""$MOUNT_POINT"" -quiet
else
    # Extract tarball
    tar -xzf ""{updatePackagePath}"" -C /Applications/
fi

open /Applications/""HASS Agent.app""
rm -f ""$0""
";
            }
            else
            {
                return $@"#!/bin/bash
sleep 2
kill {processId} 2>/dev/null || true
sleep 1

# Extract tarball
tar -xzf ""{updatePackagePath}"" -C /opt/hass-agent/

# Restart service if running as service
if systemctl is-active --quiet hass-agent; then
    sudo systemctl restart hass-agent
else
    /opt/hass-agent/HASS.Agent.Desktop &
fi

rm -f ""$0""
";
            }
        }
        
        private static bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                var current = new Version(currentVersion);
                var newer = new Version(newVersion);
                return newer > current;
            }
            catch
            {
                return string.Compare(newVersion, currentVersion, StringComparison.Ordinal) > 0;
            }
        }
    }
    
    #region Models
    
    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "";
        public string NewVersion { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string ReleaseUrl { get; set; } = "";
        public string? DownloadUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
    }
    
    public class UpdateAvailableEventArgs : EventArgs
    {
        public UpdateInfo UpdateInfo { get; }
        public UpdateAvailableEventArgs(UpdateInfo info) => UpdateInfo = info;
    }
    
    public class UpdateProgressEventArgs : EventArgs
    {
        public int ProgressPercentage { get; }
        public long BytesDownloaded { get; }
        public long TotalBytes { get; }
        
        public UpdateProgressEventArgs(int progress, long downloaded, long total)
        {
            ProgressPercentage = progress;
            BytesDownloaded = downloaded;
            TotalBytes = total;
        }
    }
    
    public class UpdateCompletedEventArgs : EventArgs
    {
        public bool Success { get; }
        public string Message { get; }
        
        public UpdateCompletedEventArgs(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
    
    internal class GitHubRelease
    {
        public string? TagName { get; set; }
        public string? Name { get; set; }
        public string? Body { get; set; }
        public string? HtmlUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
        public GitHubAsset[]? Assets { get; set; }
    }
    
    internal class GitHubAsset
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
    }
    
    #endregion
}
