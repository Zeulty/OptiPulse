using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using ChlorideTweaks.Data;

namespace ChlorideTweaks.Services
{
    public enum AppLanguage
    {
        English = 0,
        Turkish = 1,
        Spanish = 2,
        Chinese = 3
    }

    /// <summary>
    /// Central localization service. Binds in XAML via
    /// {Binding [Key], Source={x:Static services:LocalizationService.Instance}}
    /// and in code via L(key). Raises Item[] so every binding refreshes
    /// when the language changes.
    /// </summary>
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        public static LocalizationService Instance { get; } = new();

        private AppLanguage _language = AppLanguage.English;

        public AppLanguage Language
        {
            get => _language;
            private set { _language = value; OnPropertyChanged("Item[]"); }
        }

        /// <summary>Localized string for the current language.</summary>
        public string this[string key] => Translations.Get(Language, key);

        /// <summary>Shortcut for code-behind / services.</summary>
        public static string L(string key) => Instance[key];

        /// <summary>
        /// Culture matching the CURRENT APP language (not the OS culture), for
        /// formatting dates so month names etc. follow the selected language
        /// (e.g. "Eyl" instead of "Sep" while the UI is Turkish).
        /// </summary>
        public static CultureInfo CurrentCulture => GetCulture(Instance.Language);

        internal static CultureInfo GetCulture(AppLanguage lang) => lang switch
        {
            AppLanguage.Turkish => new CultureInfo("tr-TR"),
            AppLanguage.Spanish => new CultureInfo("es-ES"),
            AppLanguage.Chinese => new CultureInfo("zh-CN"),
            _ => new CultureInfo("en-US")
        };

        public static string Code(AppLanguage lang) => lang switch
        {
            AppLanguage.Turkish => "tr",
            AppLanguage.Spanish => "es",
            AppLanguage.Chinese => "zh",
            _ => "en"
        };

        public static AppLanguage FromCode(string? code) => code switch
        {
            "tr" => AppLanguage.Turkish,
            "es" => AppLanguage.Spanish,
            "zh" => AppLanguage.Chinese,
            _ => AppLanguage.English
        };

        /// <summary>Switches the active language; optionally persists it.</summary>
        public static void Set(AppLanguage lang, bool persist)
        {
            Instance.Language = lang;
            if (persist)
                AppStateService.SetLanguage(Code(lang));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
