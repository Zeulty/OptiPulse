using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ChlorideTweaks.Data;
using ChlorideTweaks.Services;

namespace ChlorideTweaks.Models
{
    /// <summary>
    /// A single tweak shown as a card. The toggle switch applies the tweak
    /// (ON) or runs the revert commands (OFF); clicking the card opens a
    /// details window with the description, risk level and side effects.
    /// Display texts are localized and refresh when the language changes.
    /// </summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class Tweak : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string WhoFor { get; set; } = "";
        public string Purpose { get; set; } = "";
        public string SideEffect { get; set; } = "";
        public RiskLevel Risk { get; set; } = RiskLevel.Safe;

        /// <summary>Registry modifications applied directly (ON), replacing the old .reg imports.</summary>
        public IReadOnlyList<RegistryChange>? ApplyRegistry { get; set; }

        /// <summary>Raw cmd.exe command strings executed in memory (ON).</summary>
        public IReadOnlyList<string>? ApplyCommands { get; set; }

        /// <summary>Registry modifications that undo the tweak (OFF).</summary>
        public IReadOnlyList<RegistryChange>? RevertRegistry { get; set; }

        /// <summary>Commands that undo the tweak (OFF); null = no automatic revert.</summary>
        public IReadOnlyList<string>? RevertCommands { get; set; }

        /// <summary>True if a command needs a visible console window (menus / file pickers).</summary>
        public bool Interactive { get; set; }

        /// <summary>True for run-once actions (cleanups, launchers) that keep no ON state.</summary>
        public bool OneShot { get; set; }

        /// <summary>Tweaks that write the same values (RAM profiles, buffer sizes...);
        /// enabling one disables the others in the group.</summary>
        public string? ConflictGroup { get; set; }

        /// <summary>True when a revert exists (commands or registry modifications).</summary>
        public bool CanRevert =>
            (RevertCommands?.Count ?? 0) > 0 || (RevertRegistry?.Count ?? 0) > 0;

        // ----- Localized display texts (fall back to the English values) -----
        public string LocalName => Translations.GetTweak(LocalizationService.Instance.Language, Id)?.Name ?? Name;
        public string LocalDescription => Translations.GetTweak(LocalizationService.Instance.Language, Id)?.Description ?? Description;
        public string LocalWhoFor => Translations.GetTweak(LocalizationService.Instance.Language, Id)?.WhoFor ?? WhoFor;
        public string LocalPurpose => Translations.GetTweak(LocalizationService.Instance.Language, Id)?.Purpose ?? Purpose;
        public string LocalSideEffect => Translations.GetTweak(LocalizationService.Instance.Language, Id)?.SideEffect ?? SideEffect;
        public string LocalCategory => Translations.Get(LocalizationService.Instance.Language, "Cat_" + Category);
        public string LocalRiskLabel => LocalizationService.L(Risk switch
        {
            RiskLevel.Moderate => "RiskMod",
            RiskLevel.High => "RiskHigh",
            _ => "RiskSafe"
        });

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanToggle));
            }
        }

        public bool CanToggle => !IsBusy;

        /// <summary>True for persistent tweaks that show the ON/OFF switch;
        /// one-shot actions (cleanups) show a run button instead.</summary>
        public bool IsToggleable => !OneShot;

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatus)); }
        }

        public bool HasStatus => StatusText.Length > 0;

        public ICommand ToggleCommand { get; }
        public ICommand DetailsCommand { get; }

        public Tweak()
        {
            ToggleCommand = new RelayCommand(_ => { _ = TweakService.ToggleAsync(this); });
            DetailsCommand = new RelayCommand(_ => ShowDetails());

            // Refresh every localized text when the user switches language.
            LocalizationService.Instance.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(LocalName));
                OnPropertyChanged(nameof(LocalDescription));
                OnPropertyChanged(nameof(LocalWhoFor));
                OnPropertyChanged(nameof(LocalPurpose));
                OnPropertyChanged(nameof(LocalSideEffect));
                OnPropertyChanged(nameof(LocalCategory));
                OnPropertyChanged(nameof(LocalRiskLabel));
            };
        }

        private void ShowDetails()
        {
            var window = new TweakDetailWindow(this)
            {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        /// <summary>
        /// Re-raises IsEnabled so OneWay-bound toggles snap back to the
        /// authoritative state after a run (e.g. when a command failed).
        /// </summary>
        public void ResyncToggle() => OnPropertyChanged(nameof(IsEnabled));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
