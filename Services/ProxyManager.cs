using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DurdomClient.Services
{
    public class ProxyManager : IDisposable
    {
        private Process? _tun2socksProcess;
        private readonly string _tun2socksPath;

        public bool IsSystemProxyActive { get; private set; }
        public bool IsTunActive { get; private set; }

        public event Action<string>? LogMessage;

        public ProxyManager()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _tun2socksPath = System.IO.Path.Combine(baseDir, "Assets", "tun2socks.exe");
        }

        public void EnableSystemProxy(int httpPort, int socksPort)
        {
            try
            {
                SetWinInetProxy($"127.0.0.1:{httpPort}", enabled: true);
                IsSystemProxyActive = true;
                LogMessage?.Invoke($"System proxy enabled: 127.0.0.1:{httpPort}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to set system proxy: {ex.Message}");
            }
        }

        public void DisableSystemProxy()
        {
            try
            {
                SetWinInetProxy(string.Empty, enabled: false);
                IsSystemProxyActive = false;
                LogMessage?.Invoke("System proxy disabled.");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to clear system proxy: {ex.Message}");
            }
        }

        private static void SetWinInetProxy(string proxyServer, bool enabled)
        {
            const string regPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
            using var key = Registry.CurrentUser.OpenSubKey(regPath, writable: true)
                ?? throw new InvalidOperationException("Cannot open Internet Settings registry key.");

            if (enabled)
            {
                key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
                key.SetValue("ProxyOverride", "localhost;127.*;10.*;172.16.*;192.168.*;<local>", RegistryValueKind.String);
            }
            else
            {
                key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            }

            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }

        public async Task<bool> EnableTunAsync(int socksPort)
        {
            if (!System.IO.File.Exists(_tun2socksPath))
            {
                LogMessage?.Invoke($"tun2socks.exe not found at: {_tun2socksPath}");
                LogMessage?.Invoke("Download from https://github.com/xjasonlyu/tun2socks/releases");
                return false;
            }

            if (IsTunActive) await DisableTunAsync();

            _tun2socksProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _tun2socksPath,
                    Arguments = $"-device tun://tun0 -proxy socks5://127.0.0.1:{socksPort} -loglevel warn",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            _tun2socksProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) LogMessage?.Invoke($"[tun2socks] {e.Data}");
            };
            _tun2socksProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) LogMessage?.Invoke($"[tun2socks] {e.Data}");
            };
            _tun2socksProcess.Exited += (_, _) =>
            {
                IsTunActive = false;
                LogMessage?.Invoke("[tun2socks] Process exited.");
            };

            try
            {
                _tun2socksProcess.Start();
                _tun2socksProcess.BeginOutputReadLine();
                _tun2socksProcess.BeginErrorReadLine();

                await Task.Delay(1000);

                if (_tun2socksProcess == null || _tun2socksProcess.HasExited)
                {
                    IsTunActive = false;
                    return false;
                }

                IsTunActive = true;
                LogMessage?.Invoke($"TUN mode active (socks5://127.0.0.1:{socksPort})");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to start TUN mode: {ex.Message}");
                IsTunActive = false;
                return false;
            }
        }

        public Task DisableTunAsync()
        {
            if (_tun2socksProcess != null && !_tun2socksProcess.HasExited)
            {
                try { _tun2socksProcess.Kill(entireProcessTree: true); }
                catch { }
            }
            _tun2socksProcess?.Dispose();
            _tun2socksProcess = null;
            IsTunActive = false;
            LogMessage?.Invoke("TUN mode disabled.");
            return Task.CompletedTask;
        }

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(
            IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        public void Dispose()
        {
            DisableSystemProxy();
            DisableTunAsync().GetAwaiter().GetResult();
            _tun2socksProcess?.Dispose();
        }
    }
}
