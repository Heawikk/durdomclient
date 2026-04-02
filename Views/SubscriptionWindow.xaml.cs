using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DurdomClient.Helpers;
using DurdomClient.Models;
using DurdomClient.Services;
using DurdomClient.ViewModels;

namespace DurdomClient.Views
{
    public partial class SubscriptionWindow : Window
    {
        private readonly MainViewModel _vm;

        public SubscriptionWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            SubList.ItemsSource = vm.Settings.Subscriptions;
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e) => DragMove();
        private void CloseWindow(object sender, RoutedEventArgs e) => Close();

        private async void AddSubscriptionClick(object sender, RoutedEventArgs e)
        {
            var url = SubUrlBox.Text.Trim();
            var name = SubNameBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(url))
            {
                StatusLabel.Text = LanguageManager.Get("StrErrEnterUrl");
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                StatusLabel.Text = LanguageManager.Get("StrErrInvalidUrl");
                return;
            }

            StatusLabel.Text = LanguageManager.Get("StrDownloading");
            IsEnabled = false;

            try
            {
                var sub = new Subscription
                {
                    Name = string.IsNullOrWhiteSpace(name) ? url : name,
                    Url = url
                };

                var svc = new SubscriptionService();
                var (servers, error) = await svc.UpdateSubscriptionAsync(sub);

                if (!string.IsNullOrEmpty(error))
                {
                    StatusLabel.Text = $"Error: {error}";
                    return;
                }

                var existing = _vm.Servers.Where(s => s.SubscriptionUrl == url).ToList();
                foreach (var s in existing)
                {
                    _vm.Servers.Remove(s);
                    _vm.Settings.Servers.Remove(s);
                }

                var dupSub = _vm.Settings.Subscriptions.FirstOrDefault(s => s.Url == url);
                if (dupSub != null) _vm.Settings.Subscriptions.Remove(dupSub);

                _vm.AddSubscription(sub);
                foreach (var s in servers)
                    _vm.AddServer(s);

                _vm.SaveSettings();
                SubList.ItemsSource = null;
                SubList.ItemsSource = _vm.Settings.Subscriptions;

                StatusLabel.Text = $"Done! Added {servers.Count} server(s) from subscription.";
                SubUrlBox.Text = string.Empty;
                SubNameBox.Text = "My Subscription";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void ImportVlessUrisClick(object sender, RoutedEventArgs e)
        {
            var text = VlessUriBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusLabel.Text = "Paste proxy URIs first (vless://, vmess://, trojan://, ss://, hysteria2://).";
                return;
            }

            int count = 0;
            foreach (var line in text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var cfg = VlessUriParser.ParseAnyUri(line.Trim());
                if (cfg != null)
                {
                    _vm.AddServer(cfg);
                    count++;
                }
            }

            _vm.SaveSettings();
            StatusLabel.Text = count > 0
                ? $"Imported {count} server(s)."
                : "No valid proxy URIs found.";

            if (count > 0)
                VlessUriBox.Text = string.Empty;
        }

        private void RemoveSubscriptionClick(object sender, RoutedEventArgs e)
        {
            if (SubList.SelectedItem is not Subscription selected) return;

            var toRemove = _vm.Servers.Where(s => s.SubscriptionUrl == selected.Url).ToList();
            foreach (var s in toRemove)
            {
                _vm.Servers.Remove(s);
                _vm.Settings.Servers.Remove(s);
            }

            _vm.Settings.Subscriptions.Remove(selected);
            _vm.SaveSettings();

            SubList.ItemsSource = null;
            SubList.ItemsSource = _vm.Settings.Subscriptions;
            StatusLabel.Text = $"Removed subscription '{selected.Name}' and its {toRemove.Count} server(s).";
        }
    }
}