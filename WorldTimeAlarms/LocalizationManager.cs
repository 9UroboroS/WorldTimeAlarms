using System;
using System.Windows;

namespace WorldTimeAlarms
{
    /// <summary>
    /// Administra la carga e intercambio dinámico del diccionario de recursos
    /// de idioma (español/inglés) para la interfaz de la aplicación.
    /// </summary>
    public static class LocalizationManager
    {
        public const string Spanish = "es";
        public const string English = "en";

        public static string CurrentLanguage { get; private set; } = Spanish;

        /// <summary>Se dispara cuando el idioma de la aplicación cambia.</summary>
        public static event Action? LanguageChanged;

        /// <summary>Obtiene el string traducido para la clave indicada, o la clave misma si no existe.</summary>
        public static string T(string key)
        {
            if (Application.Current?.TryFindResource(key) is string value)
                return value;
            return key;
        }

        /// <summary>Obtiene el string traducido y le aplica formato con los argumentos dados.</summary>
        public static string T(string key, params object[] args) =>
            string.Format(T(key), args);

        public static void ApplyLanguage(string languageCode)
        {
            string code = languageCode == English ? English : Spanish;
            string uriPath = code == English
                ? "Resources/Strings.en.xaml"
                : "Resources/Strings.es.xaml";

            var dictionary = new ResourceDictionary
            {
                Source = new Uri(uriPath, UriKind.Relative)
            };

            var existing = FindExistingLanguageDictionary();
            if (existing != null)
                ResourceDictionary.MergedDictionaries.Remove(existing);

            ResourceDictionary.MergedDictionaries.Add(dictionary);
            CurrentLanguage = code;
            LanguageChanged?.Invoke();
        }

        private static ResourceDictionary ResourceDictionary =>
            Application.Current.Resources;

        private static ResourceDictionary? FindExistingLanguageDictionary()
        {
            foreach (var dict in ResourceDictionary.MergedDictionaries)
            {
                if (dict.Source != null &&
                    (dict.Source.OriginalString.Contains("Strings.es.xaml") ||
                     dict.Source.OriginalString.Contains("Strings.en.xaml")))
                {
                    return dict;
                }
            }
            return null;
        }
    }
}
