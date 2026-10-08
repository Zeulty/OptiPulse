using System.Windows;
using ChlorideTweaks.Models;
using ChlorideTweaks.Services;

namespace ChlorideTweaks
{
    /// <summary>
    /// Details popup for a single tweak: what it does, who it is for, its
    /// purpose, risk level and the features it disables. The apply/revert
    /// switch is available right in the window.
    /// </summary>
    public partial class TweakDetailWindow : Window
    {
        private readonly Tweak _tweak;

        public TweakDetailWindow(Tweak tweak)
        {
            InitializeComponent();
            _tweak = tweak;
            DataContext = tweak;
            RefreshTitle();

            // Keep the title localized while the window is open, and stop
            // listening once it closes so the window can be collected.
            LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
            Closed += (_, _) => LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e) => RefreshTitle();

        private void RefreshTitle() =>
            Title = string.Format(LocalizationService.L("DetailTitlePattern"), _tweak.LocalName);
    }
}
