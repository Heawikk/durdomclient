using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using DurdomClient.Models;
using Newtonsoft.Json.Linq;

namespace DurdomClient.Services
{
    public static class VlessUriParser
    {
        public static List<ServerConfig> ParseSubscription(string rawContent, string subscriptionUrl)
        {
            var results = new List<ServerConfig>();

            string decoded;
            try
            {
                string padded = rawContent.Trim().Replace('-', '+').Replace('_', '/');
                int mod = padded.Length % 4;
                if (mod != 0) padded += new string('=', 4 - mod);
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            }
            catch
            {
                decoded = rawContent;
            }

            foreach (var line in decoded.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var cfg = ParseAnyUri(line.Trim());
                if (cfg != null)
                {
                    cfg.SubscriptionUrl = subscriptionUrl;
                    results.Add(cfg);
                }
            }

            return results;
        }

        public static ServerConfig? ParseAnyUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return null;
            if (uri.StartsWith("vless://",    StringComparison.OrdinalIgnoreCase)) return ParseUri(uri);
            if (uri.StartsWith("vmess://",    StringComparison.OrdinalIgnoreCase)) return ParseVmessUri(uri);
            if (uri.StartsWith("trojan://",   StringComparison.OrdinalIgnoreCase)) return ParseTrojanUri(uri);
            if (uri.StartsWith("ss://",       StringComparison.OrdinalIgnoreCase)) return ParseShadowSocksUri(uri);
            if (uri.StartsWith("hysteria2://",StringComparison.OrdinalIgnoreCase)) return ParseHysteria2Uri(uri);
            if (uri.StartsWith("hy2://",      StringComparison.OrdinalIgnoreCase)) return ParseHysteria2Uri(uri);
            return null;
        }

        public static ServerConfig? ParseUri(string uri)
        {
            try
            {
                if (!uri.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                    return null;

                string name = string.Empty;
                int hashIndex = uri.LastIndexOf('#');
                if (hashIndex >= 0)
                {
                    name = Uri.UnescapeDataString(uri.Substring(hashIndex + 1));
                    uri = uri.Substring(0, hashIndex);
                }

                var u = new Uri(uri);
                string uuid = u.UserInfo;
                string host = u.Host;
                int port = u.Port > 0 ? u.Port : 443;

                var query = HttpUtility.ParseQueryString(u.Query);

                var cfg = new ServerConfig
                {
                    Protocol    = ProxyProtocol.VLESS,
                    Name        = name,
                    Address     = host,
                    Port        = port,
                    Uuid        = uuid,
                    Network     = query["type"] ?? "tcp",
                    Security    = query["security"] ?? "none",
                    Flow        = query["flow"] ?? string.Empty,
                    Encryption  = query["encryption"] ?? "none",
                    Sni         = query["sni"] ?? string.Empty,
                    Fingerprint = query["fp"] ?? "chrome",
                    Alpn        = query["alpn"] ?? string.Empty,
                };

                cfg.AllowInsecure = query["allowInsecure"] == "1" || query["allowInsecure"] == "true";
                cfg.RealityPublicKey = query["pbk"] ?? string.Empty;
                cfg.RealityShortId   = query["sid"] ?? string.Empty;
                cfg.RealitySpiderX   = query["spx"] ?? string.Empty;
                cfg.XhttpPath = query["path"] ?? "/";
                cfg.XhttpHost = query["host"] ?? string.Empty;
                cfg.XhttpMode = query["mode"] ?? "auto";

                if (string.IsNullOrWhiteSpace(cfg.Name))
                    cfg.Name = $"{host}:{port}";

                return cfg;
            }
            catch { return null; }
        }

        private static ServerConfig? ParseVmessUri(string uri)
        {
            try
            {
                var b64 = uri.Substring("vmess://".Length).Trim();
                string padded = b64.Replace('-', '+').Replace('_', '/');
                int mod = padded.Length % 4;
                if (mod != 0) padded += new string('=', 4 - mod);
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
                var obj = JObject.Parse(json);

                string host    = obj["add"]?.ToString() ?? string.Empty;
                int    port    = obj["port"]?.ToObject<int>() ?? 443;
                string name    = obj["ps"]?.ToString()  ?? $"{host}:{port}";
                string uuid    = obj["id"]?.ToString()  ?? string.Empty;
                int    alterId = obj["aid"]?.ToObject<int>() ?? 0;
                string net     = obj["net"]?.ToString() ?? "tcp";
                string tls     = obj["tls"]?.ToString() ?? string.Empty;
                string sni     = obj["sni"]?.ToString() ?? obj["host"]?.ToString() ?? string.Empty;
                string path    = obj["path"]?.ToString() ?? "/";
                string fp      = obj["fp"]?.ToString()  ?? string.Empty;

                return new ServerConfig
                {
                    Protocol     = ProxyProtocol.VMess,
                    Name         = name,
                    Address      = host,
                    Port         = port,
                    Uuid         = uuid,
                    VmessAlterId = alterId,
                    Network      = net,
                    Security     = string.IsNullOrEmpty(tls) ? "none" : tls,
                    Sni          = sni,
                    Fingerprint  = string.IsNullOrEmpty(fp) ? "chrome" : fp,
                    XhttpPath    = path,
                };
            }
            catch { return null; }
        }

        private static ServerConfig? ParseTrojanUri(string uri)
        {
            try
            {
                string name = string.Empty;
                int hashIndex = uri.LastIndexOf('#');
                if (hashIndex >= 0)
                {
                    name = Uri.UnescapeDataString(uri.Substring(hashIndex + 1));
                    uri = uri.Substring(0, hashIndex);
                }

                var u        = new Uri(uri);
                string pass  = Uri.UnescapeDataString(u.UserInfo);
                string host  = u.Host;
                int    port  = u.Port > 0 ? u.Port : 443;
                var    query = HttpUtility.ParseQueryString(u.Query);

                return new ServerConfig
                {
                    Protocol     = ProxyProtocol.Trojan,
                    Name         = string.IsNullOrWhiteSpace(name) ? $"{host}:{port}" : name,
                    Address      = host,
                    Port         = port,
                    Password     = pass,
                    Network      = query["type"] ?? "tcp",
                    Security     = query["security"] ?? "tls",
                    Sni          = query["sni"] ?? string.Empty,
                    Fingerprint  = query["fp"]  ?? "chrome",
                    Alpn         = query["alpn"] ?? string.Empty,
                    AllowInsecure = query["allowInsecure"] == "1",
                    XhttpPath    = query["path"] ?? "/",
                    XhttpHost    = query["host"] ?? string.Empty,
                };
            }
            catch { return null; }
        }

        private static ServerConfig? ParseShadowSocksUri(string uri)
        {
            try
            {
                string name = string.Empty;
                int hashIndex = uri.LastIndexOf('#');
                if (hashIndex >= 0)
                {
                    name = Uri.UnescapeDataString(uri.Substring(hashIndex + 1));
                    uri = uri.Substring(0, hashIndex);
                }

                string body = uri.Substring("ss://".Length);

                string method = "aes-256-gcm", password = string.Empty, host = string.Empty;
                int port = 443;

                int atSign = body.LastIndexOf('@');
                if (atSign >= 0)
                {
                    string encodedCreds = body.Substring(0, atSign);
                    string hostPart     = body.Substring(atSign + 1);

                    string creds;
                    try
                    {
                        string padded = encodedCreds.Replace('-', '+').Replace('_', '/');
                        int mod = padded.Length % 4;
                        if (mod != 0) padded += new string('=', 4 - mod);
                        creds = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
                    }
                    catch
                    {
                        creds = Uri.UnescapeDataString(encodedCreds);
                    }

                    int colon = creds.IndexOf(':');
                    if (colon >= 0)
                    {
                        method   = creds.Substring(0, colon);
                        password = creds.Substring(colon + 1);
                    }

                    int lastColon = hostPart.LastIndexOf(':');
                    if (lastColon >= 0)
                    {
                        host = hostPart.Substring(0, lastColon).Trim('[', ']');
                        int.TryParse(hostPart.Substring(lastColon + 1), out port);
                    }
                }
                else
                {
                    string padded = body.Replace('-', '+').Replace('_', '/');
                    int mod = padded.Length % 4;
                    if (mod != 0) padded += new string('=', 4 - mod);
                    string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

                    int at2 = decoded.LastIndexOf('@');
                    if (at2 < 0) return null;
                    string creds    = decoded.Substring(0, at2);
                    string hostPart = decoded.Substring(at2 + 1);

                    int colon = creds.IndexOf(':');
                    if (colon >= 0) { method = creds.Substring(0, colon); password = creds.Substring(colon + 1); }

                    int lastColon = hostPart.LastIndexOf(':');
                    if (lastColon >= 0)
                    {
                        host = hostPart.Substring(0, lastColon).Trim('[', ']');
                        int.TryParse(hostPart.Substring(lastColon + 1), out port);
                    }
                }

                return new ServerConfig
                {
                    Protocol  = ProxyProtocol.ShadowSocks,
                    Name      = string.IsNullOrWhiteSpace(name) ? $"{host}:{port}" : name,
                    Address   = host,
                    Port      = port,
                    Password  = password,
                    SsMethod  = method,
                    Security  = "none",
                    Network   = "tcp",
                };
            }
            catch { return null; }
        }

        private static ServerConfig? ParseHysteria2Uri(string uri)
        {
            try
            {
                string name = string.Empty;
                int hashIndex = uri.LastIndexOf('#');
                if (hashIndex >= 0)
                {
                    name = Uri.UnescapeDataString(uri.Substring(hashIndex + 1));
                    uri = uri.Substring(0, hashIndex);
                }

                string normalized = uri
                    .Replace("hysteria2://", "https://", StringComparison.OrdinalIgnoreCase)
                    .Replace("hy2://",       "https://", StringComparison.OrdinalIgnoreCase);

                var u     = new Uri(normalized);
                string pass = Uri.UnescapeDataString(u.UserInfo);
                string host = u.Host;
                int    port = u.Port > 0 ? u.Port : 443;
                var    q   = HttpUtility.ParseQueryString(u.Query);

                return new ServerConfig
                {
                    Protocol     = ProxyProtocol.Hysteria2,
                    Name         = string.IsNullOrWhiteSpace(name) ? $"{host}:{port}" : name,
                    Address      = host,
                    Port         = port,
                    Password     = pass,
                    Sni          = q["sni"] ?? string.Empty,
                    AllowInsecure = q["insecure"] == "1",
                    Security     = "tls",
                    Network      = "udp",
                };
            }
            catch { return null; }
        }
    }
}
