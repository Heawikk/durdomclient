using System;
using System.Linq;
using System.Windows;
using Application = System.Windows.Application;

namespace DurdomClient.Helpers
{
    public static class LanguageManager
    {
        public static string Current { get; private set; } = "en";

        public static event Action? Changed;

        public static void Initialize(string lang = "en")
        {
            Current = lang;
            ApplyDictionary();
        }

        public static void Switch()
        {
            Current = Current == "en" ? "ru" : "en";
            ApplyDictionary();
            Changed?.Invoke();
        }

        public static string Get(string key) =>
            Application.Current.Resources[key] as string ?? key;

        private static void ApplyDictionary()
        {
            var uri = new Uri(
                $"pack://application:,,,/Resources/Strings.{Current}.xaml",
                UriKind.Absolute);

            var newDict = new ResourceDictionary { Source = uri };
            var merged  = Application.Current.Resources.MergedDictionaries;

            var old = merged.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("/Resources/Strings.") == true);
            if (old != null) merged.Remove(old);

            merged.Add(newDict);
        }
    }
}
