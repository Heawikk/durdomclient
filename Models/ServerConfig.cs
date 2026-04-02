using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace DurdomClient.Models
{
    public enum ProxyProtocol { VLESS, VMess, Trojan, ShadowSocks, Hysteria2 }

    public partial class ServerConfig : ObservableObject
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; } = 443;
        public ProxyProtocol Protocol { get; set; } = ProxyProtocol.VLESS;

        public string Uuid { get; set; } = string.Empty;
        public string Flow { get; set; } = string.Empty;
        public string Encryption { get; set; } = "none";
        public int VmessAlterId { get; set; } = 0;

        public string Password { get; set; } = string.Empty;
        public string SsMethod { get; set; } = "aes-256-gcm";

        public string Network { get; set; } = "tcp";
        public string Security { get; set; } = "reality";

        public string Sni { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = "chrome";
        public bool AllowInsecure { get; set; } = false;
        public string Alpn { get; set; } = string.Empty;

        public string RealityPublicKey { get; set; } = string.Empty;
        public string RealityShortId { get; set; } = string.Empty;
        public string RealitySpiderX { get; set; } = string.Empty;

        public string XhttpPath { get; set; } = "/";
        public string XhttpHost { get; set; } = string.Empty;
        public string XhttpMode { get; set; } = "auto";

        public string? SubscriptionUrl { get; set; }
        public System.DateTime AddedAt { get; set; } = System.DateTime.UtcNow;

        [JsonIgnore]
        [ObservableProperty]
        private int? _pingMs;

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Name) ? $"{Address}:{Port}" : Name;
    }

    public class Subscription
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = "My Subscription";
        public string Url { get; set; } = string.Empty;
        public System.DateTime LastUpdated { get; set; } = System.DateTime.MinValue;
        public int ServerCount { get; set; } = 0;
    }

    public class AppSettings
    {
        public List<ServerConfig> Servers { get; set; } = new();
        public List<Subscription> Subscriptions { get; set; } = new();
        public string? SelectedServerId { get; set; }
        public int LocalSocksPort { get; set; } = 10808;
        public int LocalHttpPort { get; set; } = 10809;
        public bool SystemProxyEnabled { get; set; } = true;
        public bool TunModeEnabled { get; set; } = false;
        public bool AutoConnect { get; set; } = false;
        public RoutingMode RoutingMode { get; set; } = RoutingMode.Global;
        public List<string> ProxyExclusions { get; set; } = new()
        {
            "127.0.0.0/8",
            "10.0.0.0/8",
            "172.16.0.0/12",
            "192.168.0.0/16",
            "::1/128",
            "fc00::/7",
            "localhost",
            "*.local"
        };
    }

    public enum RoutingMode { Global, GFWList, Direct }
}
