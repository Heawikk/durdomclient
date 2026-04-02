using System.Linq;
using System.Windows;
using System.Windows.Input;
using DurdomClient.Helpers;
using DurdomClient.Models;
using DurdomClient.ViewModels;
using DurdomClient.Views;
using Application = System.Windows.Application;

namespace DurdomClient;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _reallyClose = false;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        _vm.LogEntries.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                && !_vm.IsLogPaused)
            {
                Dispatcher.InvokeAsync(
                    () => LogScroller.ScrollToEnd(),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        };

        InitTrayIcon();
        LanguageManager.Changed += UpdateTrayMenuText;
    }

    private void InitTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon    = CreateTrayIcon(),
            Text    = "Durdom Client",
            Visible = true
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(LanguageManager.Get("StrTrayShow"), null, (_, _) => ShowFromTray());
        menu.Items.Add(LanguageManager.Get("StrTrayExit"), null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void UpdateTrayMenuText()
    {
        if (_trayIcon?.ContextMenuStrip == null) return;
        Dispatcher.Invoke(() =>
        {
            _trayIcon.ContextMenuStrip.Items[0].Text = LanguageManager.Get("StrTrayShow");
            _trayIcon.ContextMenuStrip.Items[1].Text = LanguageManager.Get("StrTrayExit");
        });
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/app.ico");
        var stream = Application.GetResourceStream(uri)?.Stream;
        return stream != null ? new System.Drawing.Icon(stream) : SystemIcons();
    }

    private static System.Drawing.Icon SystemIcons()
    {
        var bmp = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.Transparent);
        using var br = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(79, 107, 255));
        g.FillEllipse(br, 2, 2, 12, 12);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private void ShowFromTray()
    {
        Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); });
    }

    private void ExitApp()
    {
        _reallyClose = true;
        Dispatcher.Invoke(Close);
    }

    public void LangToggle_Click(object sender, RoutedEventArgs e)
    {
        LanguageManager.Switch();
        LangBtn.Content = LanguageManager.Current == "en" ? "RU" : "EN";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleMaximize();
        else
            DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e) =>
        ToggleMaximize();

    private void CloseClick(object sender, RoutedEventArgs e) => Hide();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OpenSubscriptionWindow(object sender, RoutedEventArgs e)
    {
        var win = new SubscriptionWindow(_vm);
        win.Owner = this;
        win.ShowDialog();
        SidebarSubList.ItemsSource = null;
        SidebarSubList.ItemsSource = _vm.Settings.Subscriptions;
    }

    private void RemoveSubscriptionClick(object sender, RoutedEventArgs e)
    {
        if (SidebarSubList.SelectedItem is not Subscription selected) return;

        var toRemove = _vm.Servers
            .Where(s => s.SubscriptionUrl == selected.Url)
            .ToList();
        foreach (var s in toRemove)
        {
            _vm.Servers.Remove(s);
            _vm.Settings.Servers.Remove(s);
        }

        _vm.Settings.Subscriptions.Remove(selected);
        _vm.SaveSettings();

        SidebarSubList.ItemsSource = null;
        SidebarSubList.ItemsSource = _vm.Settings.Subscriptions;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _trayIcon?.Dispose();
        _vm.SaveSettings();
        _vm.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}