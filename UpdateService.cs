using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace EdulinkerPen
{
    public class UpdateInfo
    {
        public Version NewVersion { get; set; } = null!;
        public string DownloadUrl { get; set; } = string.Empty;
        public string DownloadedFilePath { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }

    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/neohum/edulinker-pen/releases/latest";
        private static readonly HttpClient _httpClient;
        private UpdateInfo? _pendingUpdate;

        public UpdateInfo? PendingUpdate => _pendingUpdate;

        static UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("EdulinkerPen", GetCurrentVersion().ToString()));
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public static Version GetCurrentVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            return ver ?? new Version(1, 0, 0);
        }

        /// <summary>
        /// Checks GitHub Releases for a newer version.
        /// Returns UpdateInfo if a new version is available, null otherwise.
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(GitHubApiUrl);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Parse tag_name (e.g., "v1.0.1" → "1.0.1")
                var tagName = root.GetProperty("tag_name").GetString();
                if (string.IsNullOrWhiteSpace(tagName))
                    return null;

                var versionString = tagName.TrimStart('v', 'V');
                if (!Version.TryParse(versionString, out var remoteVersion))
                    return null;

                var currentVersion = GetCurrentVersion();

                // Compare only Major.Minor.Build (ignore Revision)
                var current = new Version(currentVersion.Major, currentVersion.Minor,
                    Math.Max(currentVersion.Build, 0));
                var remote = new Version(remoteVersion.Major, remoteVersion.Minor,
                    Math.Max(remoteVersion.Build, 0));

                if (remote <= current)
                    return null;

                // Find the .exe asset in the release
                string? downloadUrl = null;
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                    return null;

                var releaseNotes = "";
                if (root.TryGetProperty("body", out var body))
                    releaseNotes = body.GetString() ?? "";

                var info = new UpdateInfo
                {
                    NewVersion = remoteVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = releaseNotes
                };

                _pendingUpdate = info;
                return info;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] CheckForUpdate failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads the update EXE to a temp folder.
        /// Returns true if download succeeded.
        /// </summary>
        public async Task<bool> DownloadUpdateAsync(UpdateInfo info)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "EdulinkerPenUpdate");
                Directory.CreateDirectory(tempDir);

                var fileName = $"EdulinkerPen_{info.NewVersion}.exe";
                var filePath = Path.Combine(tempDir, fileName);

                // Clean up previous download if exists
                if (File.Exists(filePath))
                    File.Delete(filePath);

                using var response = await _httpClient.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);

                info.DownloadedFilePath = filePath;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Download failed: {ex.Message}");
                CleanupTempFiles();
                return false;
            }
        }

        /// <summary>
        /// Creates and executes a batch script that:
        /// 1. Waits for the current process to exit
        /// 2. Replaces the current EXE with the new one (with retry logic)
        /// 3. Restarts the application
        /// 4. Deletes itself
        /// </summary>
        public void ApplyUpdate(UpdateInfo info)
        {
            var currentExePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? Assembly.GetExecutingAssembly().Location;

            var currentPid = Environment.ProcessId;
            var batchPath = Path.Combine(Path.GetTempPath(), "EdulinkerPenUpdate", "update.bat");

            var batchContent = $@"@echo off
chcp 65001 >nul 2>&1
echo Edulinker-Pen 업데이트 중...
echo 프로세스 종료 대기 중 (PID: {currentPid})...

:wait_loop
tasklist /FI ""PID eq {currentPid}"" 2>nul | find ""{currentPid}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)

echo 프로세스 종료 확인. 파일 교체 중...

set RETRY=0
:copy_loop
if %RETRY% GEQ 10 (
    echo 업데이트 실패: 파일 교체 불가
    pause
    goto cleanup
)

copy /Y ""{info.DownloadedFilePath}"" ""{currentExePath}"" >nul 2>&1
if errorlevel 1 (
    set /a RETRY+=1
    echo 재시도 %RETRY%/10...
    timeout /t 1 /nobreak >nul
    goto copy_loop
)

echo 업데이트 완료. 앱을 재실행합니다...
start """" ""{currentExePath}""

:cleanup
del ""{info.DownloadedFilePath}"" >nul 2>&1
del ""%~f0"" >nul 2>&1
";

            File.WriteAllText(batchPath, batchContent, System.Text.Encoding.GetEncoding(949));

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            Process.Start(psi);

            // Shutdown the current application
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }

        /// <summary>
        /// Cleans up any temporary download files.
        /// </summary>
        public static void CleanupTempFiles()
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "EdulinkerPenUpdate");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Cleanup failed: {ex.Message}");
            }
        }
    }
}
