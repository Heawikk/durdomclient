using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurdomClient.Models;

namespace DurdomClient.Services
{
    public enum XrayStatus
    {
        Stopped,
        Starting,
        Running,
        Error
    }

    public class XrayManager : IDisposable
    {
        private Process? _process;
        private CancellationTokenSource? _cts;
        private readonly string _xrayPath;
        private readonly string _configPath;
        private readonly JobObjectManager _job = new();

        public XrayStatus Status { get; private set; } = XrayStatus.Stopped;
        public event Action<XrayStatus>? StatusChanged;
        public event Action<string>? LogMessage;

        public XrayManager()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _xrayPath = Path.Combine(baseDir, "Assets", "xray.exe");
            _configPath = Path.Combine(baseDir, "xray-config.json");
        }

        public bool IsXrayAvailable() => File.Exists(_xrayPath);

        public async Task<bool> StartAsync(string configJson)
        {
            if (Status == XrayStatus.Running)
                await StopAsync();

            if (!IsXrayAvailable())
            {
                SetStatus(XrayStatus.Error);
                LogMessage?.Invoke($"xray.exe not found at: {_xrayPath}");
                LogMessage?.Invoke("Download xray-core from https://github.com/XTLS/Xray-core/releases and place xray.exe in the Assets folder.");
                return false;
            }

            await File.WriteAllTextAsync(_configPath, configJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            _cts = new CancellationTokenSource();
            SetStatus(XrayStatus.Starting);

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _xrayPath,
                    Arguments = $"run -c \"{_configPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) LogMessage?.Invoke($"[xray] {e.Data}");
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) LogMessage?.Invoke($"[xray] {e.Data}");
            };
            _process.Exited += (_, _) =>
            {
                SetStatus(XrayStatus.Stopped);
                LogMessage?.Invoke("[xray] Process exited.");
            };

            try
            {
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                _job.AssignProcess(_process); 

                await Task.Delay(500, _cts.Token);

                if (_process.HasExited)
                {
                    SetStatus(XrayStatus.Error);
                    return false;
                }

                SetStatus(XrayStatus.Running);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to start xray: {ex.Message}");
                SetStatus(XrayStatus.Error);
                return false;
            }
        }

        public Task StopAsync()
        {
            _cts?.Cancel();

            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch { /* process may have already exited */ }
            }

            _process?.Dispose();
            _process = null;
            SetStatus(XrayStatus.Stopped);
            return Task.CompletedTask;
        }

        private void SetStatus(XrayStatus status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _process?.Dispose();
            _job.Dispose();
        }
    }
}
