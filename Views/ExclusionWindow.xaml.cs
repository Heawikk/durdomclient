using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DurdomClient.Models;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DurdomClient.Views
{
    public partial class ExclusionWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly ObservableCollection<string> _items;

        public ExclusionWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            _items = new ObservableCollection<string>(settings.ProxyExclusions);
            ExclusionList.ItemsSource = _items;
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e) => DragMove();

        private void AddEntryClick(object sender, RoutedEventArgs e)
        {
            var entry = NewEntryBox.Text.Trim();
            if (string.IsNullOrEmpty(entry)) return;
            if (!_items.Contains(entry))
            {
                _items.Add(entry);
                StatusLabel.Text = $"Added: {entry}";
            }
            NewEntryBox.Text = string.Empty;
        }

        private void RemoveEntryClick(object sender, RoutedEventArgs e)
        {
            if (ExclusionList.SelectedItem is string selected)
            {
                _items.Remove(selected);
                StatusLabel.Text = $"Removed: {selected}";
            }
        }

        private void NewEntryBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddEntryClick(sender, e);
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            _settings.ProxyExclusions.Clear();
            foreach (var item in _items)
                _settings.ProxyExclusions.Add(item);
            Close();
        }

        private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    }
}
