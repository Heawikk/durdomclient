using System.Collections.Generic;
using DurdomClient.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DurdomClient.Services
{
    public static class XrayConfigBuilder
    {
        public static string Build(
            ServerConfig server,
            AppSettings settings,
            bool tunMode = false)
        {
            if (server.Protocol == ProxyProtocol.Hysteria2)
                throw new System.NotSupportedException(
                    "Hysteria2 requires a separate client and is not supported by xray-core. " +
                    "Please use a dedicated Hysteria2 client for this server.");

            var root = new JObject
            {
                ["log"] = new JObject { ["loglevel"] = "warning" },
                ["inbounds"]  = BuildInbounds(settings, tunMode),
                ["outbounds"] = BuildOutbounds(server, settings),
                ["routing"]   = BuildRouting(settings.RoutingMode, settings.ProxyExclusions),
                ["dns"]       = BuildDns()
            };

            return root.ToString(Formatting.Indented);
        }

        private static JArray BuildInbounds(AppSettings s, bool tunMode)
        {
            var arr = new JArray
            {
                new JObject
                {
                    ["tag"]      = "socks-in",
                    ["port"]     = s.LocalSocksPort,
                    ["listen"]   = "127.0.0.1",
                    ["protocol"] = "socks",
                    ["settings"] = new JObject { ["auth"] = "noauth", ["udp"] = true },
                    ["sniffing"] = new JObject
                    {
                        ["enabled"]     = true,
                        ["destOverride"] = new JArray("http", "tls")
                    }
                },
                new JObject
                {
                    ["tag"]      = "http-in",
                    ["port"]     = s.LocalHttpPort,
                    ["listen"]   = "127.0.0.1",
                    ["protocol"] = "http",
                    ["sniffing"] = new JObject
                    {
                        ["enabled"]     = true,
                        ["destOverride"] = new JArray("http", "tls")
                    }
                }
            };

            if (tunMode)
            {
                arr.Add(new JObject
                {
                    ["tag"]      = "tun-in",
                    ["protocol"] = "dokodemo-door",
                    ["port"]     = 12345,
                    ["listen"]   = "0.0.0.0",
                    ["settings"] = new JObject { ["network"] = "tcp,udp", ["followRedirect"] = true },
                    ["streamSettings"] = new JObject
                    {
                        ["sockopt"] = new JObject { ["tproxy"] = "tproxy" }
                    }
                });
            }

            return arr;
        }

        private static JArray BuildOutbounds(ServerConfig s, AppSettings settings)
        {
            var proxy = s.Protocol switch
            {
                ProxyProtocol.VMess       => BuildVMessOutbound(s),
                ProxyProtocol.Trojan      => BuildTrojanOutbound(s),
                ProxyProtocol.ShadowSocks => BuildShadowSocksOutbound(s),
                _                         => BuildVlessOutbound(s)
            };

            return new JArray
            {
                proxy,
                new JObject { ["tag"] = "direct",  ["protocol"] = "freedom" },
                new JObject { ["tag"] = "block",   ["protocol"] = "blackhole" }
            };
        }

        private static JObject BuildVlessOutbound(ServerConfig s) => new()
        {
            ["tag"]      = "proxy",
            ["protocol"] = "vless",
            ["settings"] = new JObject
            {
                ["vnext"] = new JArray
                {
                    new JObject
                    {
                        ["address"] = s.Address,
                        ["port"]    = s.Port,
                        ["users"]   = new JArray
                        {
                            new JObject
                            {
                                ["id"]         = s.Uuid,
                                ["encryption"] = s.Encryption,
                                ["flow"]       = s.Flow
                            }
                        }
                    }
                }
            },
            ["streamSettings"] = BuildStreamSettings(s),
            ["mux"] = new JObject { ["enabled"] = false }
        };

        private static JObject BuildVMessOutbound(ServerConfig s) => new()
        {
            ["tag"]      = "proxy",
            ["protocol"] = "vmess",
            ["settings"] = new JObject
            {
                ["vnext"] = new JArray
                {
                    new JObject
                    {
                        ["address"] = s.Address,
                        ["port"]    = s.Port,
                        ["users"]   = new JArray
                        {
                            new JObject
                            {
                                ["id"]       = s.Uuid,
                                ["alterId"]  = s.VmessAlterId,
                                ["security"] = "auto"
                            }
                        }
                    }
                }
            },
            ["streamSettings"] = BuildStreamSettings(s),
            ["mux"] = new JObject { ["enabled"] = false }
        };

        private static JObject BuildTrojanOutbound(ServerConfig s) => new()
        {
            ["tag"]      = "proxy",
            ["protocol"] = "trojan",
            ["settings"] = new JObject
            {
                ["servers"] = new JArray
                {
                    new JObject
                    {
                        ["address"]  = s.Address,
                        ["port"]     = s.Port,
                        ["password"] = s.Password
                    }
                }
            },
            ["streamSettings"] = BuildStreamSettings(s),
            ["mux"] = new JObject { ["enabled"] = false }
        };

        private static JObject BuildShadowSocksOutbound(ServerConfig s) => new()
        {
            ["tag"]      = "proxy",
            ["protocol"] = "shadowsocks",
            ["settings"] = new JObject
            {
                ["servers"] = new JArray
                {
                    new JObject
                    {
                        ["address"]  = s.Address,
                        ["port"]     = s.Port,
                        ["method"]   = s.SsMethod,
                        ["password"] = s.Password
                    }
                }
            }
        };

        private static JObject BuildStreamSettings(ServerConfig s)
        {
            var stream = new JObject
            {
                ["network"]  = s.Network,
                ["security"] = s.Security
            };

            if (s.Security == "tls")
            {
                stream["tlsSettings"] = new JObject
                {
                    ["serverName"]   = s.Sni,
                    ["fingerprint"]  = s.Fingerprint,
                    ["allowInsecure"] = s.AllowInsecure,
                    ["alpn"] = string.IsNullOrEmpty(s.Alpn)
                        ? new JArray("h2", "http/1.1")
                        : new JArray(s.Alpn.Split(','))
                };
            }
            else if (s.Security == "reality")
            {
                stream["realitySettings"] = new JObject
                {
                    ["serverName"] = s.Sni,
                    ["fingerprint"] = s.Fingerprint,
                    ["publicKey"]  = s.RealityPublicKey,
                    ["shortId"]    = s.RealityShortId,
                    ["spiderX"]    = s.RealitySpiderX
                };
            }

            if (s.Network == "xhttp")
            {
                stream["xhttpSettings"] = new JObject
                {
                    ["path"] = s.XhttpPath,
                    ["host"] = s.XhttpHost,
                    ["mode"] = s.XhttpMode
                };
            }
            else if (s.Network == "tcp")
            {
                stream["tcpSettings"] = new JObject
                {
                    ["header"] = new JObject { ["type"] = "none" }
                };
            }

            return stream;
        }

        private static JObject BuildRouting(RoutingMode mode, List<string> exclusions)
        {
            var routing = new JObject
            {
                ["domainStrategy"] = "IPIfNonMatch",
                ["rules"] = new JArray()
            };

            var rules = (JArray)routing["rules"]!;

            rules.Add(new JObject
            {
                ["type"]       = "field",
                ["outboundTag"] = "block",
                ["domain"]     = new JArray("geosite:category-ads-all")
            });

            if (exclusions.Count > 0)
            {
                var ipExclusions     = new JArray();
                var domainExclusions = new JArray();

                foreach (var entry in exclusions)
                {
                    var e = entry.Trim();
                    if (string.IsNullOrEmpty(e)) continue;
                    if (e.Contains('.') && (e.Contains('/') || !e.Contains('*')))
                    {
                        if (char.IsDigit(e[0]) || e[0] == ':' || e.Contains("::"))
                            ipExclusions.Add(e);
                        else
                            domainExclusions.Add(e);
                    }
                    else
                    {
                        domainExclusions.Add(e);
                    }
                }

                if (ipExclusions.Count > 0)
                    rules.Add(new JObject
                    {
                        ["type"]       = "field",
                        ["outboundTag"] = "direct",
                        ["ip"]         = ipExclusions
                    });

                if (domainExclusions.Count > 0)
                    rules.Add(new JObject
                    {
                        ["type"]       = "field",
                        ["outboundTag"] = "direct",
                        ["domain"]     = domainExclusions
                    });
            }

            if (mode == RoutingMode.Global)
            {
                rules.Add(new JObject
                {
                    ["type"]       = "field",
                    ["outboundTag"] = "proxy",
                    ["network"]    = "tcp,udp"
                });
            }
            else if (mode == RoutingMode.Direct)
            {
                rules.Add(new JObject
                {
                    ["type"]       = "field",
                    ["outboundTag"] = "direct",
                    ["network"]    = "tcp,udp"
                });
            }
            else
            {
                rules.Add(new JObject
                {
                    ["type"]       = "field",
                    ["outboundTag"] = "direct",
                    ["domain"]     = new JArray("geosite:cn")
                });
                rules.Add(new JObject
                {
                    ["type"]       = "field",
                    ["outboundTag"] = "direct",
                    ["ip"]         = new JArray("geoip:cn", "geoip:private")
                });
                rules.Add(new JObject
                {
                    ["type"]       = "field",
                    ["outboundTag"] = "proxy",
                    ["network"]    = "tcp,udp"
                });
            }

            return routing;
        }

        private static JObject BuildDns()
        {
            return new JObject
            {
                ["servers"] = new JArray
                {
                    new JObject
                    {
                        ["address"]   = "8.8.8.8",
                        ["domains"]   = new JArray("geosite:geolocation-!cn"),
                        ["expectIPs"] = new JArray("geoip:!cn")
                    },
                    new JObject
                    {
                        ["address"] = "223.5.5.5",
                        ["domains"] = new JArray("geosite:cn")
                    },
                    "1.1.1.1",
                    "localhost"
                }
            };
        }
    }
}
