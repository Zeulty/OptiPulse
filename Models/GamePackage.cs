using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ChlorideTweaks.Services;

namespace ChlorideTweaks.Models
{
    /// <summary>A curated bundle of existing tweaks applied with one click.</summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class GamePackage
    {
        public string Id { get; init; } = "";
        public string Icon { get; init; } = "";

        /// <summary>True for packages that get the extra-strong warning in the confirmation dialog.</summary>
        public bool RequiresStrongWarning { get; init; }

        /// <summary>Resolved tweaks in execution order (the same instances the UI shows).</summary>
        public List<Tweak> Tweaks { get; init; } = new();

        public string Title => LocalizationService.L("PkgTitle_" + Id);
        public string Description => LocalizationService.L("PkgDesc_" + Id);
        public string SafetyNote => LocalizationService.L("PkgSafety_" + Id);
    }

    /// <summary>Live progress of a package run (1-based index of the tweak being applied).</summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed record PackageProgress(int Index, int Total, string TweakName);

    /// <summary>Outcome of a package run.</summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed record PackageRunResult(int Applied, int AlreadyEnabled, int Failed, int Total, long ExtraData = 0);

    /// <summary>
    /// UI wrapper around a GamePackage: localized texts,
    /// live progress and the apply command.
    /// </summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class GamePackageViewModel : INotifyPropertyChanged
    {
        public GamePackage Package { get; }

        public string Icon => Package.Icon;
        public string Title => Package.Title;
        public string Description => Package.Description;
        public string SafetyNote => Package.SafetyNote;
        public List<string> IncludedNames => Package.Tweaks.Select(t => t.LocalName).ToList();

        public bool IsRunning { get; private set; }
        public bool CanApply => !IsRunning && !GamePackageService.IsRunning;

        public int ProgressMaximum => Package.Tweaks.Count;

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            private set { _progressValue = value; OnPropertyChanged(); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        public ICommand ApplyCommand { get; }

        public GamePackageViewModel(GamePackage package)
        {
            Package = package;
            ApplyCommand = new RelayCommand(_ => { _ = RunAsync(); }, _ => CanApply);

            // Refresh every localized text when the user switches language.
            LocalizationService.Instance.PropertyChanged += (_, _) => RefreshTexts();
            // Other cards re-evaluate their Apply button when a package starts/stops.
            GamePackageService.RunningChanged += () => OnPropertyChanged(nameof(CanApply));
        }

        private void RefreshTexts()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(SafetyNote));
            OnPropertyChanged(nameof(IncludedNames));
            OnPropertyChanged(nameof(CanApply));
        }

        private async Task RunAsync()
        {
            if (!CanApply) return;
            if (!GamePackageService.ConfirmApply(Package)) return;

            IsRunning = true;
            GamePackageService.MarkRunning(true);
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(CanApply));
            StatusText = "";
            ProgressValue = 0;

            var progress = new Progress<PackageProgress>(p =>
            {
                StatusText = string.Format(
                    LocalizationService.L("PkgProgressPattern"), p.Index, p.Total, p.TweakName);
                ProgressValue = p.Index - 1;
            });

            var result = await GamePackageService.ApplyAsync(Package, progress);

            IsRunning = false;
            GamePackageService.MarkRunning(false);
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(CanApply));

            GamePackageService.ShowResult(Package, result);
            StatusText = "";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
