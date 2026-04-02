using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DurdomClient.Helpers;
using DurdomClient.Models;
using DurdomClient.Services;
using System.Collections.Specialized;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace DurdomClient.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly XrayManager _xray = new();
        private readonly ProxyManager _proxy = new();
        private readonly SubscriptionService _subService = new();
        private CancellationTokenSource? _updateCts;

        [ObservableProperty]
        private AppSettings _settings = SettingsStore.Load();

        [ObservableProperty]
        private ObservableCollection<ServerConfig> _servers = new();

        [ObservableProperty]
        private ServerConfig? _selectedServer;

        [ObservableProperty]
        private string _statusText = "Disconnected";

        [ObservableProperty]
        private string _statusColor = "#EF4444";

        [ObservableProperty]
        private bool _isConnected = false;

        [ObservableProperty]
        private bool _isBusy = false;

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        [ObservableProperty]
        private bool _isLogPaused = false;

        public string PauseButtonText =>
            IsLogPaused
                ? LanguageManager.Get("StrResume")
                : LanguageManager.Get("StrPause");

        [ObservableProperty]
        private bool _tunMode = false;

        [ObservableProperty]
        private bool _systemProxyMode = true;

        public MainViewModel()
        {
            _xray.StatusChanged += OnXrayStatusChanged;
            _xray.LogMessage += AppendLog;
            _proxy.LogMessage += AppendLog;
            _subService.LogMessage += AppendLog;

            LanguageManager.Changed += () =>
                Application.Current.Dispatcher.Invoke(() =>
                    OnPropertyChanged(nameof(PauseButtonText)));

            foreach (var s in Settings.Servers)
                Servers.Add(s);

            SelectedServer = Servers.FirstOrDefault(s => s.Id == Settings.SelectedServerId)
                             ?? Servers.FirstOrDefault();

            TunMode = Settings.TunModeEnabled;
            SystemProxyMode = Settings.SystemProxyEnabled;

            if (Settings.AutoConnect && SelectedServer != null)
                _ = ConnectAsync();
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (SelectedServer == null)
            {
                AppendLog("No server selected.");
                return;
            }

            IsBusy = true;
            StatusText = "Connecting...";
            StatusColor = "#F59E0B";

            try
            {
                Settings.SelectedServerId = SelectedServer.Id;
                var configJson = XrayConfigBuilder.Build(SelectedServer, Settings, TunMode);
                bool started = await _xray.StartAsync(configJson);

                if (started)
                {
                    if (SystemProxyMode)
                        _proxy.EnableSystemProxy(Settings.LocalHttpPort, Settings.LocalSocksPort);

                    if (TunMode)
                        await _proxy.EnableTunAsync(Settings.LocalSocksPort);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DisconnectAsync()
        {
            IsBusy = true;
            try
            {
                await _xray.StopAsync();

                if (_proxy.IsSystemProxyActive)
                    _proxy.DisableSystemProxy();

                if (_proxy.IsTunActive)
                    await _proxy.DisableTunAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddServerFromClipboard()
        {
            try
            {
                var text = Clipboard.GetText()?.Trim() ?? string.Empty;
                int added = 0;

                foreach (var line in text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cfg = VlessUriParser.ParseAnyUri(line.Trim());
                    if (cfg != null)
                    {
                        AddServer(cfg);
                        added++;
                    }
                }

                AppendLog(added > 0 ? $"Added {added} server(s) from clipboard." : "No valid proxy URIs found in clipboard.");
            }
            catch (Exception ex)
            {
                AppendLog($"Clipboard error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void RemoveServer()
        {
            if (SelectedServer == null) return;
            Settings.Servers.Remove(SelectedServer);
            Servers.Remove(SelectedServer);
            SelectedServer = Servers.FirstOrDefault();
            SaveSettings();
        }

        [RelayCommand]
        private async Task UpdateAllSubscriptionsAsync()
        {
            _updateCts?.Cancel();
            _updateCts = new CancellationTokenSource();
            IsBusy = true;

            try
            {
                foreach (var sub in Settings.Subscriptions.ToList())
                {
                    var (newServers, error) = await _subService.UpdateSubscriptionAsync(sub, _updateCts.Token);

                    if (!string.IsNullOrEmpty(error))
                    {
                        AppendLog(error);
                        continue;
                    }

                    var old = Servers.Where(s => s.SubscriptionUrl == sub.Url).ToList();
                    foreach (var s in old)
                    {
                        Servers.Remove(s);
                        Settings.Servers.Remove(s);
                    }

                    foreach (var s in newServers)
                        AddServer(s);
                }

                SaveSettings();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearLog() => LogEntries.Clear();

        [RelayCommand]
        private void ToggleLogPause()
        {
            IsLogPaused = !IsLogPaused;
            OnPropertyChanged(nameof(PauseButtonText));
        }

        [RelayCommand]
        private async Task PingServersAsync()
        {
            IsBusy = true;
            AppendLog("Pinging all servers...");
            try
            {
                foreach (var s in Servers)
                    s.PingMs = null;

                var tasks = Servers.Select(async server =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        using var tcp = new TcpClient();
                        using var cts = new CancellationTokenSource(3000);
                        await tcp.ConnectAsync(server.Address, server.Port, cts.Token);
                        server.PingMs = (int)sw.ElapsedMilliseconds;
                    }
                    catch
                    {
                        server.PingMs = -1;
                    }
                }).ToArray();

                await Task.WhenAll(tasks);

                int ok   = Servers.Count(s => s.PingMs >= 0);
                int fail = Servers.Count(s => s.PingMs == -1);
                AppendLog($"Ping complete: {ok} reachable, {fail} unreachable.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenExclusions()
        {
            var win = new Views.ExclusionWindow(Settings);
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
            SaveSettings();
        }

        partial void OnSystemProxyModeChanged(bool value)
        {
            SaveSettings();
            if (!IsConnected) return;
            if (value)
                _proxy.EnableSystemProxy(Settings.LocalHttpPort, Settings.LocalSocksPort);
            else
                _proxy.DisableSystemProxy();
        }

        partial void OnTunModeChanged(bool value)
        {
            SaveSettings();
            if (!IsConnected) return;
            _ = value ? _proxy.EnableTunAsync(Settings.LocalSocksPort) : _proxy.DisableTunAsync();
        }


        public void AddServer(ServerConfig cfg)
        {
            Servers.Add(cfg);
            Settings.Servers.Add(cfg);
            SelectedServer ??= cfg;
        }

        public void AddSubscription(Subscription sub)
        {
            Settings.Subscriptions.Add(sub);
            SaveSettings();
        }

        public void SaveSettings()
        {
            Settings.TunModeEnabled = TunMode;
            Settings.SystemProxyEnabled = SystemProxyMode;
            SettingsStore.Save(Settings);
        }

        private void OnXrayStatusChanged(XrayStatus status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnected = status == XrayStatus.Running;
                StatusText = status switch
                {
                    XrayStatus.Running => $"Connected — SOCKS5: 127.0.0.1:{Settings.LocalSocksPort}",
                    XrayStatus.Starting => "Starting...",
                    XrayStatus.Error => "Error",
                    _ => "Disconnected"
                };
                StatusColor = status switch
                {
                    XrayStatus.Running => "#22C55E",
                    XrayStatus.Starting => "#F59E0B",
                    _ => "#EF4444"
                };
            });
        }

        private void AppendLog(string message)
        {
            if (IsLogPaused) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                bool isError = message.IndexOf("fail",      StringComparison.OrdinalIgnoreCase) >= 0
                            || message.IndexOf("error",     StringComparison.OrdinalIgnoreCase) >= 0
                            || message.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0
                            || message.IndexOf("panic",     StringComparison.OrdinalIgnoreCase) >= 0;

                bool isWarn = !isError && (
                               message.IndexOf("warn",    StringComparison.OrdinalIgnoreCase) >= 0
                            || message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0);

                LogEntries.Add(new LogEntry
                {
                    Time    = DateTime.Now.ToString("HH:mm:ss"),
                    Message = message,
                    Level   = isError ? LogLevel.Error
                            : isWarn  ? LogLevel.Warning
                            : LogLevel.Info
                });

                while (LogEntries.Count > 500)
                    LogEntries.RemoveAt(0);
            });
        }

        partial void OnSelectedServerChanged(ServerConfig? value)
        {
            SaveSettings();
            if (IsConnected && value != null)
            {
                AppendLog($"Server changed to '{value.Name}', reconnecting...");
                _ = ConnectAsync();
            }
        }

        public void Dispose()
        {
            _updateCts?.Cancel();
            _xray.Dispose();
            _proxy.Dispose();
        }
    }
}
