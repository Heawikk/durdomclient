using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using DurdomClient.Models;
using Binding = System.Windows.Data.Binding;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace DurdomClient.Helpers
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            value is Visibility.Visible;
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is true ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            value is Visibility.Collapsed;
    }

    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string enumStr && value != null)
                return value.ToString() == enumStr;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is true && parameter is string enumStr)
                return Enum.Parse(targetType, enumStr);
            return Binding.DoNothing;
        }
    }

    public class InverseBoolConverter : MarkupExtension, IValueConverter
    {
        private static readonly InverseBoolConverter _instance = new();
        public override object ProvideValue(IServiceProvider serviceProvider) => _instance;
        public object Convert(object v, Type t, object p, CultureInfo c) => v is false;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is false;
    }

    public class LogLevelToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush _info    = LogBrush("#5A7A9A");
        private static readonly SolidColorBrush _warning = LogBrush("#C8922A");
        private static readonly SolidColorBrush _error   = LogBrush("#E05050");

        private static SolidColorBrush LogBrush(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is LogLevel l ? l switch
            {
                LogLevel.Error   => _error,
                LogLevel.Warning => _warning,
                _                => _info
            } : _info;

        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }

    public class PingToStringConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is null) return "·";
            int ms = (int)value;
            return ms < 0 ? "✕" : $"{ms}ms";
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class PingToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush _none = Brush("#354560");
        private static readonly SolidColorBrush _good = Brush("#22C55E");
        private static readonly SolidColorBrush _ok   = Brush("#F59E0B");
        private static readonly SolidColorBrush _bad  = Brush("#EF4444");

        private static SolidColorBrush Brush(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is null) return _none;
            int ms = (int)value;
            if (ms < 0)   return _bad;
            if (ms < 100) return _good;
            if (ms < 300) return _ok;
            return _bad;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
