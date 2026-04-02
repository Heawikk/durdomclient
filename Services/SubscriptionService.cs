using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurdomClient.Models;

namespace DurdomClient.Services
{
    public class SubscriptionService
    {
        private static readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = null
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public event Action<string>? LogMessage;

        public async Task<(List<ServerConfig> Servers, string Error)> UpdateSubscriptionAsync(
            Subscription subscription,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(subscription.Url))
                    return (new(), "Subscription URL is empty.");

                var uri = new Uri(subscription.Url);
                if (uri.Scheme != "https" && uri.Scheme != "http")
                    return (new(), "Only http/https URLs are supported.");

                LogMessage?.Invoke($"Updating subscription: {subscription.Name}...");

                var response = await _httpClient.GetAsync(subscription.Url, ct);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(ct);
                var servers = VlessUriParser.ParseSubscription(content, subscription.Url);

                subscription.LastUpdated = DateTime.UtcNow;
                subscription.ServerCount = servers.Count;

                LogMessage?.Invoke($"Subscription updated: {servers.Count} server(s) found.");
                return (servers, string.Empty);
            }
            catch (OperationCanceledException)
            {
                return (new(), "Update cancelled.");
            }
            catch (Exception ex)
            {
                return (new(), $"Failed to update subscription: {ex.Message}");
            }
        }
    }
}
