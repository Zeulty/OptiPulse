using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ChlorideTweaks.Data;
using ChlorideTweaks.Models;
using ChlorideTweaks.Services;

namespace ChlorideTweaks
{
    /// <summary>
    /// Sidebar category entry: a simple logo plus the localized category
    /// name. Display refreshes when the language changes.
    /// </summary>
    public sealed class CategoryItem : INotifyPropertyChanged
    {
        /// <summary>One simple logo per category (consistent with the emoji
        /// icons already used by the packages and VIP badges).</summary>
        private static readonly Dictionary<string, string> Logos = new()
        {
            ["Home"]         = "\U0001F3E0",   // 🏠
            ["CPU"]          = "\u2699\uFE0F", // ⚙️
            ["GPU"]          = "\U0001F3AE",   // 🎮
            ["RAM"]          = "\U0001F9E0",   // 🧠
            ["Disk"]         = "\U0001F4BE",   // 💾
            ["USB"]          = "\U0001F50C",   // 🔌
            ["Input Delay"]  = "\u2328\uFE0F", // ⌨️
            ["Network"]      = "\U0001F310",   // 🌐
            ["Game Booster"] = "\U0001F680",   // 🚀
            ["UWP Apps"]     = "\U0001F9F9",   // 🧹
            ["Startup Apps"] = "\u25B6\uFE0F", // ▶️
            ["Scan"]         = "\U0001F50D",   // 🔍
            ["Privacy"]      = "\U0001F512",   // 🔒
            ["Game Launcher"]= "\U0001F579\uFE0F", // 🕹️
            ["Game Packages"]= "\U0001F4E6",   // 📦
        };

        public string Key { get; }

        public string Logo => Logos.GetValueOrDefault(Key, "\u2022");

        public string Display => LocalizationService.L("Cat_" + Key);

        public CategoryItem(string key)
        {
            Key = key;
            LocalizationService.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Display));
        }

        public override string ToString() => Display;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        private readonly List<Tweak> _allTweaks;
        private readonly ObservableCollection<Tweak> _visibleTweaks = new();
        private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
        private bool _monitorsRunning;
        private bool _sampling;
        private bool _videoPaused = AppStateService.State.VideoPaused;

        private PcInfo? _pcInfo;
        private string? _currentCategory;
        private string _displayName = "";

        // Built lazily on the first visit of the Game Packages tab (GetAll does
        // hardware detection) - never during window construction.
        private List<GamePackageViewModel>? _packageVms;

        public List<CategoryItem> Categories { get; } =
            TweakData.CategoryOrder.ConvertAll(c => new CategoryItem(c));

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _allTweaks = TweakData.GetAll();
            TweakService.Initialize(_allTweaks);
            GamePackageService.Initialize(_allTweaks);
            TweakList.ItemsSource = _visibleTweaks;

            // Restore which tweaks were applied the last time the app ran
            foreach (var tweak in _allTweaks.Where(t => AppStateService.IsTweakEnabled(t.Id)))
                tweak.IsEnabled = true;

            // RAM slider starts at 8 GB (the most common size); the page-open
            // refresh snaps it to the applied profile, if any.
            RamSlider.Value = 1;

            // USB sliders start at 15 packets (the sweet spot); the page-open
            // refresh snaps them to the applied buffers, if any.
            KbSlider.Value = 2;
            MouseSlider.Value = 2;

            // Background video: starts once the window is loaded and loops
            // until the app closes. Purely cosmetic - any failure (missing
            // file, missing codec) silently falls back to the regular
            // themed backdrop.
            Loaded += (_, _) => StartBackgroundVideo();
            StateChanged += (_, _) =>
            {
                try
                {
                    if (WindowState == WindowState.Minimized)
                        BackgroundVideo.Pause();
                    else if (!_videoPaused)
                        BackgroundVideo.Play();
                }
                catch { }
            };

            // Sidebar version label follows the assembly (<Version> in the
            // .csproj), so releases update it automatically.
            VersionText.Text = "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version()).ToString(3);

            // Language selector shows the active language
            LanguageBox.SelectedIndex = (int)LocalizationService.Instance.Language;

            // Welcome text shows the PC name
            _displayName = Environment.MachineName;
            OsVersionText.Text = SystemInfoService.GetWindowsVersionLabel();
            TimeZoneText.Text = TimeZoneInfo.Local.DisplayName;

            RefreshTexts();
            UpdateClock();
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
            Closed += (_, _) => _clockTimer.Stop();

            // Live resource monitors: prime the CPU baseline once, then sample
            // on the UI timer but do all heavy work (WMI) on a worker thread.
            ResourceMonitorService.Initialize();
            _monitorTimer.Tick += async (_, _) => await SampleMonitorsAsync();
            Closed += (_, _) => _monitorTimer.Stop();
            StartMonitors();

            // Refresh every code-behind text when the user switches language
            LocalizationService.Instance.PropertyChanged += (_, _) => RefreshTexts();

            // Refresh security statuses when accent color changes
            ThemeService.AccentChanged += _ =>
            {
                Dispatcher.Invoke(() =>
                {
                    RefreshStatuses();
                });
            };

            Loaded += MainWindow_Loaded;
            CategoryList.SelectedIndex = 0; // Home
        }

        // Hardware/security info is gathered on a background thread so startup stays snappy.
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AnimatePageIn();

            try
            {
                var pc = await Task.Run(SystemInfoService.Collect);
                ApplySystemInfo(pc);
            }
            catch
            {
                // Keep the placeholder texts; the section still fades in below.
            }
            finally
            {
                SystemInfoPanel.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });
            }
        }

        private void ApplySystemInfo(PcInfo pc)
        {
            _pcInfo = pc;

            // Details are formatted in the CURRENT language; RefreshTexts
            // re-runs this after a language switch, so the Home screen
            // re-localizes without querying WMI again.
            var v = SystemInfoService.FormatDetails(pc);
            CpuNameText.Text = v.CpuName;
            CpuDetailText.Text = v.CpuDetail;
            GpuNameText.Text = v.GpuName;
            GpuDetailText.Text = v.GpuDetail;
            RamText.Text = v.RamTotal;
            RamDetailText.Text = v.RamDetail;
            BoardText.Text = v.Board;
            BoardDetailText.Text = v.BoardDetail;
            StorageText.Text = v.StorageTotal;
            StorageDetailText.Text = v.StorageDetail;
            DisplayText.Text = v.Display;
            DisplayDetailText.Text = v.DisplayDetail;

            // Secure Boot is not applicable on legacy BIOS installs
            RefreshStatuses();
            SecureBootDetailText.Text = v.SecureBootDetail;
            TpmDetailText.Text = v.TpmDetail;

            UptimeDetailText.Text = v.UptimeDetail;
            UptimeText.Text = SystemInfoService.FormatUptime(DateTime.UtcNow - pc.BootTimeUtc);
        }

        // Colors the status dot + label: green (enabled), yellow (disabled), gray (unknown).
        private void ApplyStatus(Ellipse dot, TextBlock label, bool? state, string unknownKey)
        {
            string key = state switch
            {
                true => "BrushRiskSafe",
                false => "BrushRiskMod",
                _ => "BrushTextMuted"
            };
            var brush = (Brush)FindResource(key);
            dot.Fill = brush;
            label.Text = state switch
            {
                true => LocalizationService.L("StatusEnabled"),
                false => LocalizationService.L("StatusDisabled"),
                _ => LocalizationService.L(unknownKey)
            };
            label.Foreground = state == null ? (Brush)FindResource("BrushTextPrimary") : brush;
        }

        private void RefreshStatuses()
        {
            if (_pcInfo == null) return;

            ApplyStatus(SecureBootDot, SecureBootText, _pcInfo.SecureBoot,
                _pcInfo.Uefi ? "StatusUnknown" : "StatusUnsupported");
            ApplyStatus(TpmDot, TpmText, _pcInfo.Tpm, "StatusNotDetected");
        }

        // Re-renders every text that is set from code (XAML bindings refresh on their own).
        private void RefreshTexts()
        {
            WelcomeText.Text = string.Format(LocalizationService.L("WelcomePattern"), _displayName);
            RefreshStatuses();
            RefreshRamPanel();
            UpdateVideoToggleButton();

            // Re-format the system info details in the new language.
            if (_pcInfo != null)
                ApplySystemInfo(_pcInfo);

            // Re-render the scan result cards (titles/details are localized).
            if (_scanFindings != null)
                RenderScanResults();

            // Re-localize the UWP app list (names + statuses + footer).
            if (_uwpRows != null)
            {
                foreach (UwpRow row in _uwpRows)
                {
                    row.NameText.Text = LocalizationService.L("UwpApp_" + row.Key);
                    UpdateUwpRowStatus(row);
                }
                RebuildUwpList();
            }

            // Re-localize the startup app list (rebuild cards: names,
            // commands, locations and statuses all follow the language).
            if (_startupRows != null)
            {
                foreach (StartupRow row in _startupRows)
                    BuildStartupCard(row);
                RebuildStartupList();
            }

            // Re-localize the USB buffer cards (state lines + readouts).
            if (KbSlider != null)
                RefreshUsbPanel();

            if (_currentCategory is { } category && category != "Home")
                HeaderText.Text = string.Format(LocalizationService.L("HeaderPattern"),
                    LocalizationService.L("Cat_" + category));
        }

        private void UpdateClock()
        {
            // 12-hour format with AM/PM, hour and minutes only
            ClockText.Text = DateTime.Now.ToString("hh:mm tt");
            if (_pcInfo != null)
                UptimeText.Text = SystemInfoService.FormatUptime(DateTime.UtcNow - _pcInfo.BootTimeUtc);
        }

        // ---------------- Background video ----------------

        private void StartBackgroundVideo()
        {
            try
            {
                UpdateVideoToggleButton();
                string path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Assets", "background.mp4");
                if (!System.IO.File.Exists(path)) return;
                BackgroundVideo.Source = new Uri(path);
                BackgroundVideo.Play();
            }
            catch
            {
                // Video is cosmetic; keep the themed backdrop on any failure.
            }
        }

        private void BackgroundVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            // If the user previously paused the video, pause as soon as the first frame
            // is decoded so the background remains visible as a still frame.
            if (_videoPaused)
            {
                try { BackgroundVideo.Pause(); } catch { }
            }
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (_videoPaused) return;
            // Loop forever: rewind and replay until the app closes.
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }

        private void BackgroundVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Missing codec / unreadable file: stay silently on the backdrop.
        }

        private void VideoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _videoPaused = !_videoPaused;
            AppStateService.SetVideoPaused(_videoPaused);
            try
            {
                if (_videoPaused)
                    BackgroundVideo.Pause();
                else
                    BackgroundVideo.Play();
            }
            catch
            {
            }
            UpdateVideoToggleButton();
        }

        private void UpdateVideoToggleButton()
        {
            if (VideoToggleText == null || VideoToggleIcon == null) return;
            VideoToggleIcon.Text = _videoPaused ? "▶" : "⏸";
            VideoToggleText.Text = LocalizationService.L(_videoPaused ? "BgVideoPlay" : "BgVideoPause");
        }

        // ---------------- Live resource monitors ----------------

        // Sampling runs only while the Home page is visible: no work at all
        // while the user browses tweaks (and none after the window closes).
        private void StartMonitors()
        {
            if (_monitorsRunning) return;
            _monitorsRunning = true;
            _monitorTimer.Start();
            _ = SampleMonitorsAsync();
        }

        private void StopMonitors()
        {
            if (!_monitorsRunning) return;
            _monitorsRunning = false;
            _monitorTimer.Stop();
        }

        // The timer fires on the UI thread, but the sample itself
        // runs on the worker thread; only the final text/bar updates
        // are marshalled back.
        private async Task SampleMonitorsAsync()
        {
            if (_sampling) return;
            _sampling = true;
            try
            {
                ResourceSample sample;
                try
                {
                    sample = await Task.Run(ResourceMonitorService.Sample);
                }
                catch
                {
                    return;
                }
                ApplyMonitors(sample);
            }
            finally
            {
                _sampling = false;
            }
        }

        private void ApplyMonitors(ResourceSample s)
        {
            // CPU: usage % + load detail line
            if (s.CpuPercent is { } cpu)
            {
                CpuValueText.Text = $"{cpu:0}%";
                CpuBar.Value = Math.Clamp(cpu, 0, 100);
                CpuLoadText.Text = cpu >= 85
                    ? LocalizationService.L("MonitorHighLoad")
                    : LocalizationService.L("MonitorLoadNormal");
            }
            else
            {
                CpuValueText.Text = "\u2014";
                CpuBar.Value = 0;
                CpuLoadText.Text = " ";
            }

            // Memory: usage % + "x.x / y.y GB"
            if (s.MemTotalGb > 0)
            {
                MemValueText.Text = $"{s.MemPercent:0}%";
                MemBar.Value = Math.Clamp(s.MemPercent, 0, 100);
                MemDetailText.Text = string.Format(
                    LocalizationService.L("MonitorMemPattern"), s.MemUsedGb, s.MemTotalGb);
            }
            else
            {
                MemValueText.Text = "\u2014";
                MemBar.Value = 0;
                MemDetailText.Text = " ";
            }

            // Disk: activity %
            if (s.DiskPercent is { } disk)
            {
                DiskValueText.Text = $"{disk:0}%";
                DiskBar.Value = Math.Clamp(disk, 0, 100);
                DiskDetailText.Text = LocalizationService.L("MonitorDiskActivity");
            }
            else
            {
                DiskValueText.Text = "\u2014";
                DiskBar.Value = 0;
                DiskDetailText.Text = " ";
            }
        }

        // ---------------- Toast notifications ----------------

        // Shows completion / failure feedback at the bottom of the window and
        // fades it out automatically. A newer toast replaces the visible one.

        private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(4) };
        private int _toastSerial;

        /// <summary>Shows an app toast from any code (e.g. TweakService).</summary>
        public static void ShowAppToast(string message, bool success)
        {
            if (Application.Current?.MainWindow is MainWindow window)
                window.ShowToast(message, success);
        }

        private void ShowToast(string message, bool success)
        {
            var accent = (Brush)FindResource(success ? "BrushNeonGreen" : "BrushRiskHigh");
            ToastText.Text = message;
            ToastDot.Fill = accent;
            ToastHost.BorderBrush = accent;

            ToastHost.Visibility = Visibility.Visible;
            ToastHost.BeginAnimation(OpacityProperty, null);   // cancel a running fade-out
            ToastHost.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            // Each toast gets a serial so only the newest one hides the host;
            // a follow-up toast must not be faded out by an older timer.
            int serial = ++_toastSerial;
            _toastTimer.Stop();
            _toastTimer.Tick -= ToastTimer_Tick;
            _toastTimer.Tick += ToastTimer_Tick;
            _toastTimer.Start();
        }

        private void ToastTimer_Tick(object? sender, EventArgs e)
        {
            _toastTimer.Stop();
            int serial = _toastSerial;

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) =>
            {
                if (serial == _toastSerial)
                    ToastHost.Visibility = Visibility.Collapsed;
            };
            ToastHost.BeginAnimation(OpacityProperty, fade);
        }

        // ---------------- Scan page ----------------

        // The scan itself is free for every user; cleaning what it finds is
        // the VIP part. Findings stay raw so a language switch re-renders
        // every detail line without re-scanning.

        private List<ScanFinding>? _scanFindings;
        private bool _scanRunning;

        private static readonly Dictionary<string, string> ScanIcons = new()
        {
            ["temp"] = "\U0001F5C2\uFE0F",
            ["prefetch"] = "\u26A1",
            ["wu-cache"] = "\U0001F504",
            ["thumbcache"] = "\U0001F5BC\uFE0F",
            ["dumps"] = "\U0001F4A5",
            ["recycle"] = "\U0001F5D1\uFE0F",
            ["browser"] = "\U0001F310",
            ["muicache"] = "\U0001F9FE",
            ["tracing"] = "\U0001F4E1",
            ["uninstall"] = "\U0001F4E6",
            ["startup"] = "\U0001F680",
            ["shortcuts"] = "\U0001F517"
        };

        private void RefreshScanPage()
        {
            if (_scanFindings != null)
                RenderScanResults();
        }

        private async void ScanStart_Click(object sender, RoutedEventArgs e)
        {
            if (_scanRunning) return;

            _scanRunning = true;
            ScanStartButton.IsEnabled = false;
            ScanCleanAllButton.Visibility = Visibility.Collapsed;
            ScanReclaimableText.Text = "";
            ScanResultsPanel.Children.Clear();

            IProgress<string> progress = new Progress<string>(id =>
                ScanStatusText.Text = string.Format(
                    LocalizationService.L("ScanScanningPattern"),
                    LocalizationService.L("ScanItem_" + id + "_Name")));

            try
            {
                var findings = await Task.Run(() => ScanService.Scan(progress.Report));
                _scanFindings = findings;
                RenderScanResults();

                int issues = findings.Count(f => f.HasFix);
                ScanStatusText.Text = issues > 0
                    ? string.Format(LocalizationService.L("ScanDonePattern"), findings.Count, issues)
                    : LocalizationService.L("ScanNoIssues");
            }
            catch (Exception ex)
            {
                ScanStatusText.Text = ex.Message;
            }
            finally
            {
                _scanRunning = false;
                ScanStartButton.IsEnabled = true;
            }
        }

        private void RenderScanResults()
        {
            ScanResultsPanel.Children.Clear();
            if (_scanFindings == null) return;

            long reclaimable = 0;
            int cleanable = 0;

            foreach (ScanFinding finding in _scanFindings)
            {
                if (!finding.HasFix) continue;
                reclaimable += finding.Bytes;
                cleanable++;

                ScanResultsPanel.Children.Add(BuildScanCard(finding));
            }

            if (cleanable > 0)
            {
                ScanCleanAllButton.Visibility = Visibility.Visible;
                ScanReclaimableText.Text = string.Format(
                    LocalizationService.L("ScanReclaimablePattern"), FormatScanBytes(reclaimable));
            }
            else
            {
                ScanCleanAllButton.Visibility = Visibility.Collapsed;
                ScanReclaimableText.Text = "";
            }
        }

        private Border BuildScanCard(ScanFinding finding)
        {
            string icon = ScanIcons.GetValueOrDefault(finding.Id, "\u2022");
            string name = LocalizationService.L("ScanItem_" + finding.Id + "_Name");
            string description = LocalizationService.L("ScanItem_" + finding.Id + "_Desc");
            string detail = FormatScanDetail(finding);

            var card = new Border
            {
                Style = (Style)FindResource("StatCard"),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(14, 10, 14, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            var texts = new StackPanel();
            texts.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("BrushTextPrimary"),
                TextWrapping = TextWrapping.Wrap
            });
            texts.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 10.5,
                Foreground = (Brush)FindResource("BrushTextMuted"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            header.Children.Add(iconText);
            header.Children.Add(texts);
            Grid.SetColumn(header, 0);
            grid.Children.Add(header);

            var detailText = new TextBlock
            {
                Text = detail,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("BrushNeonGreen"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 16, 0),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(detailText, 1);
            grid.Children.Add(detailText);

            var cleanButton = new Button
            {
                Style = (Style)FindResource("PillButton"),
                MinWidth = 90,
                Content = LocalizationService.L("ScanCleanBtn"),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = finding.Id
            };
            cleanButton.Click += ScanClean_Click;
            Grid.SetColumn(cleanButton, 2);
            grid.Children.Add(cleanButton);

            card.Child = grid;
            return card;
        }

        private static string FormatScanDetail(ScanFinding finding)
        {
            if (finding.Kind == ScanDetailKind.Files)
                return string.Format(
                    LocalizationService.L("ScanDetailFilesPattern"),
                    FormatScanBytes(finding.Bytes), finding.Count);
            if (finding.Kind == ScanDetailKind.Entries)
                return string.Format(
                    LocalizationService.L("ScanDetailEntriesPattern"), finding.Count);
            return string.Format(
                LocalizationService.L("ScanDetailItemsPattern"), finding.Count);
        }

        private static string FormatScanBytes(long bytes)
        {
            double gb = bytes / 1024.0 / 1024 / 1024;
            if (gb >= 1024) return $"{gb / 1024.0:0.#} TB";
            if (gb >= 10) return $"{gb:0} GB";
            if (gb >= 1) return $"{gb:0.#} GB";
            return $"{bytes / 1024.0 / 1024:0.#} MB";
        }

        private async void ScanClean_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string id }) return;
            if (_scanRunning) return;

            _scanRunning = true;
            try
            {
                if (sender is Button button)
                {
                    button.IsEnabled = false;
                    button.Content = LocalizationService.L("ScanCleaning");
                }

                await Task.Run(() => ScanService.Fix(id));

                // Mark the card as cleaned.
                if (_scanFindings is { } findings)
                {
                    var finding = findings.FirstOrDefault(f => f.Id == id);
                    if (finding != null)
                        findings[findings.IndexOf(finding)] = new ScanFinding
                        {
                            Id = id, Kind = finding.Kind, Count = 0
                        };
                }
                RenderScanResults();

                MainWindow.ShowAppToast(string.Format(
                    LocalizationService.L("ScanCleanToastPattern"),
                    LocalizationService.L("ScanItem_" + id + "_Name")), success: true);
            }
            finally
            {
                _scanRunning = false;
            }
        }

        private async void ScanCleanAll_Click(object sender, RoutedEventArgs e)
        {
            if (_scanRunning) return;
            if (_scanFindings?.Any(f => f.HasFix) != true) return;

            _scanRunning = true;
            ScanCleanAllButton.IsEnabled = false;
            ScanStartButton.IsEnabled = false;
            try
            {
                var ids = _scanFindings.Where(f => f.HasFix).Select(f => f.Id).ToList();
                foreach (string id in ids)
                    await Task.Run(() => ScanService.Fix(id));

                _scanFindings = _scanFindings
                    .Select(f => f.HasFix ? new ScanFinding { Id = f.Id, Kind = f.Kind } : f)
                    .ToList();
                RenderScanResults();

                ScanStatusText.Text = string.Format(
                    LocalizationService.L("ScanCleanAllToastPattern"), ids.Count);
                MainWindow.ShowAppToast(string.Format(
                    LocalizationService.L("ScanCleanAllToastPattern"), ids.Count), success: true);
            }
            finally
            {
                _scanRunning = false;
                ScanCleanAllButton.IsEnabled = true;
                ScanStartButton.IsEnabled = true;
            }
        }

        // ---------------- UWP Apps page ----------------

        // Safe built-in app remover. Browsing/selecting is free; deleting is
        // the VIP part. Rows keep their raw state so search filtering and
        // language switches re-render without re-querying PowerShell.

        private sealed class UwpRow
        {
            public string Key = "";
            public string PackageName = "";
            public bool Installed;
            public Border Card = null!;
            public CheckBox CheckBox = null!;
            public TextBlock NameText = null!;
            public TextBlock StatusText = null!;
        }

        private List<UwpRow>? _uwpRows;
        private bool _uwpRemoving;

        private void RefreshUwpPage()
        {

            if (_uwpRows != null)
            {
                RebuildUwpList();
                return;
            }

            // First visit: query which catalog apps are actually installed.
            UwpStatusText.Text = LocalizationService.L("UwpChecking");
            UwpSelectAllButton.IsEnabled = false;
            UwpDeleteButton.IsEnabled = false;

            _ = LoadUwpRowsAsync();
        }

        private async Task LoadUwpRowsAsync()
        {
            var installed = await Task.Run(UwpService.GetInstalledNames);

            await Dispatcher.InvokeAsync(() =>
            {
                _uwpRows = UwpService.Catalog
                    .Select(app => new UwpRow
                    {
                        Key = app.Key,
                        PackageName = app.PackageName,
                        Installed = installed.Contains(app.PackageName)
                    })
                    .ToList();

                // UI elements must be created on the UI thread.
                foreach (UwpRow row in _uwpRows)
                    BuildUwpCard(row);

                UwpSelectAllButton.IsEnabled = true;
                RebuildUwpList();
            });
        }

        private void RebuildUwpList()
        {
            if (_uwpRows == null) return;

            UwpListPanel.Children.Clear();
            string filter = UwpSearchBox.Text.Trim();

            foreach (UwpRow row in _uwpRows)
            {
                string name = LocalizationService.L("UwpApp_" + row.Key);
                bool matches = filter.Length == 0
                    || name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || row.PackageName.Contains(filter, StringComparison.OrdinalIgnoreCase);

                UwpListPanel.Children.Add(row.Card);
                row.Card.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            }

            UwpStatusText.Text = string.Format(
                LocalizationService.L("UwpInstalledSummaryPattern"),
                _uwpRows.Count(r => r.Installed),
                _uwpRows.Count);
            UpdateUwpFooter();
        }

        private void BuildUwpCard(UwpRow row)
        {
            var card = new Border
            {
                Style = (Style)FindResource("StatCard"),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 8, 12, 8),
                Opacity = row.Installed ? 1.0 : 0.55
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = row.Installed && !_uwpRemoving
            };
            checkBox.Checked += (_, _) => UpdateUwpFooter();
            checkBox.Unchecked += (_, _) => UpdateUwpFooter();
            Grid.SetColumn(checkBox, 0);
            grid.Children.Add(checkBox);

            var texts = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
            var nameText = new TextBlock
            {
                Text = LocalizationService.L("UwpApp_" + row.Key),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("BrushTextPrimary")
            };
            texts.Children.Add(nameText);
            row.NameText = nameText;
            texts.Children.Add(new TextBlock
            {
                Text = row.PackageName,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)FindResource("BrushTextMuted"),
                Margin = new Thickness(0, 1, 0, 0)
            });
            Grid.SetColumn(texts, 1);
            grid.Children.Add(texts);

            var status = new TextBlock
            {
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(status, 2);
            grid.Children.Add(status);

            card.Child = grid;
            row.Card = card;
            row.CheckBox = checkBox;
            row.StatusText = status;
            UpdateUwpRowStatus(row);
        }

        private void UpdateUwpRowStatus(UwpRow row)
        {
            row.StatusText.Text = row.Installed
                ? LocalizationService.L("UwpInstalled")
                : LocalizationService.L("UwpNotInstalled");
            row.StatusText.Foreground = row.Installed
                ? (Brush)FindResource("BrushNeonGreen")
                : (Brush)FindResource("BrushTextMuted");
            row.Card.Opacity = row.Installed ? 1.0 : 0.55;
        }

        private void UpdateUwpFooter()
        {
            int selected = _uwpRows?.Count(r => r.Installed && r.CheckBox.IsChecked == true) ?? 0;
            UwpSelectedText.Text = selected > 0
                ? string.Format(LocalizationService.L("UwpSelectedPattern"), selected)
                : " ";
            UwpDeleteButton.IsEnabled = selected > 0 && !_uwpRemoving;

            bool anyVisibleUnchecked = _uwpRows?.Any(r =>
                r.Card.Visibility == Visibility.Visible && r.Installed && r.CheckBox.IsChecked != true) == true;
            UwpSelectAllButton.Content = LocalizationService.L(
                anyVisibleUnchecked ? "UwpSelectAllBtn" : "UwpClearSelectionBtn");
        }

        private void UwpSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            UwpSearchHint.Visibility = UwpSearchBox.Text.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (_uwpRows != null) RebuildUwpList();
        }

        private void UwpSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_uwpRows == null) return;

            bool anyVisibleUnchecked = _uwpRows.Any(r =>
                r.Card.Visibility == Visibility.Visible && r.Installed && r.CheckBox.IsChecked != true);

            foreach (UwpRow row in _uwpRows)
            {
                if (row.Card.Visibility != Visibility.Visible || !row.Installed) continue;
                row.CheckBox.IsChecked = anyVisibleUnchecked;
            }
            UpdateUwpFooter();
        }

        private async void UwpDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_uwpRows == null || _uwpRemoving) return;
            var selected = _uwpRows
                .Where(r => r.Installed && r.CheckBox.IsChecked == true)
                .ToList();
            if (selected.Count == 0) return;

            var confirm = MessageBox.Show(
                string.Format(LocalizationService.L("UwpConfirmPattern"), selected.Count),
                LocalizationService.L("UwpTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            _uwpRemoving = true;
            UwpDeleteButton.IsEnabled = false;
            UwpSelectAllButton.IsEnabled = false;
            UwpStatusText.Text = LocalizationService.L("UwpRemoving");
            foreach (UwpRow row in _uwpRows)
                row.CheckBox.IsEnabled = false;

            try
            {
                await Task.Run(() => UwpService.RemoveApps(selected.Select(r => r.PackageName)));

                // Refresh: apps may still be installed if they were locked.
                var stillInstalled = await Task.Run(UwpService.GetInstalledNames);
                foreach (UwpRow row in _uwpRows)
                {
                    row.Installed = stillInstalled.Contains(row.PackageName);
                    row.CheckBox.IsChecked = false;
                    row.CheckBox.IsEnabled = row.Installed;
                    UpdateUwpRowStatus(row);
                }

                int removed = selected.Count(r => !r.Installed);
                UwpStatusText.Text = string.Format(
                    LocalizationService.L("UwpRemovedToastPattern"), removed);
                MainWindow.ShowAppToast(string.Format(
                    LocalizationService.L("UwpRemovedToastPattern"), removed), success: true);
            }
            finally
            {
                _uwpRemoving = false;
                UwpSelectAllButton.IsEnabled = true;
                foreach (UwpRow row in _uwpRows)
                    row.CheckBox.IsEnabled = row.Installed;
                UpdateUwpFooter();
            }
        }

        // ---------------- Startup Apps page ----------------

        // Manage what runs at boot. Browsing is free; disabling/enabling is
        // the VIP part. Disabled entries are parked (never deleted) and can
        // be re-enabled anytime.

        private sealed class StartupRow
        {
            public StartupEntry Entry = null!;
            public Border Card = null!;
            public CheckBox CheckBox = null!;
            public TextBlock NameText = null!;
            public TextBlock StatusText = null!;
        }

        private List<StartupRow>? _startupRows;
        private bool _startupBusy;

        private void RefreshStartupPage()
        {

            if (_startupRows != null)
            {
                RebuildStartupList();
                return;
            }

            StartupStatusText.Text = LocalizationService.L("StartupChecking");
            StartupDisableButton.IsEnabled = false;
            StartupEnableButton.IsEnabled = false;

            _ = LoadStartupRowsAsync();
        }

        private async Task LoadStartupRowsAsync()
        {
            List<StartupEntry> entries = await Task.Run(StartupService.GetEntries);

            await Dispatcher.InvokeAsync(() =>
            {
                _startupRows = entries.Select(e => new StartupRow { Entry = e }).ToList();
                foreach (StartupRow row in _startupRows)
                    BuildStartupCard(row);

                StartupDisableButton.IsEnabled = true;
                StartupEnableButton.IsEnabled = true;
                RebuildStartupList();
            });
        }

        private void RebuildStartupList()
        {
            if (_startupRows == null) return;

            StartupListPanel.Children.Clear();
            foreach (StartupRow row in _startupRows)
            {
                StartupListPanel.Children.Add(row.Card);
            }

            StartupStatusText.Text = string.Format(
                LocalizationService.L("StartupSummaryPattern"),
                _startupRows.Count(r => r.Entry.Enabled),
                _startupRows.Count);
            UpdateStartupFooter();
        }

        private void BuildStartupCard(StartupRow row)
        {
            var card = new Border
            {
                Style = (Style)FindResource("StatCard"),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 8, 12, 8),
                Opacity = row.Entry.Enabled ? 1.0 : 0.6
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = !_startupBusy
            };
            checkBox.Checked += (_, _) => UpdateStartupFooter();
            checkBox.Unchecked += (_, _) => UpdateStartupFooter();
            Grid.SetColumn(checkBox, 0);
            grid.Children.Add(checkBox);

            var texts = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
            var nameText = new TextBlock
            {
                Text = row.Entry.Name,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("BrushTextPrimary"),
                TextWrapping = TextWrapping.Wrap
            };
            texts.Children.Add(nameText);
            row.NameText = nameText;

            var commandText = new TextBlock
            {
                Text = TrimCommand(row.Entry.Command),
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)FindResource("BrushTextMuted"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0),
                ToolTip = row.Entry.Command
            };
            texts.Children.Add(commandText);

            var locationText = new TextBlock
            {
                Text = LocalizationService.L("StartupLoc_" + row.Entry.Location),
                FontSize = 9.5,
                Foreground = (Brush)FindResource("BrushTextMuted"),
                Margin = new Thickness(0, 2, 0, 0)
            };
            texts.Children.Add(locationText);
            Grid.SetColumn(texts, 1);
            grid.Children.Add(texts);

            var status = new TextBlock
            {
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(status, 2);
            grid.Children.Add(status);
            row.StatusText = status;

            card.Child = grid;
            row.Card = card;
            row.CheckBox = checkBox;
            UpdateStartupRowStatus(row);
        }

        private void UpdateStartupRowStatus(StartupRow row)
        {
            row.StatusText.Text = row.Entry.Enabled
                ? LocalizationService.L("StartupEnabled")
                : LocalizationService.L("StartupDisabled");
            row.StatusText.Foreground = row.Entry.Enabled
                ? (Brush)FindResource("BrushNeonGreen")
                : (Brush)FindResource("BrushTextMuted");
            row.Card.Opacity = row.Entry.Enabled ? 1.0 : 0.6;
        }

        private static string TrimCommand(string command) =>
            command.Length > 90 ? command[..90] + "\u2026" : command;

        private void UpdateStartupFooter()
        {
            int selected = _startupRows?.Count(r => r.CheckBox.IsChecked == true) ?? 0;
            StartupSelectedText.Text = selected > 0
                ? string.Format(LocalizationService.L("StartupSelectedPattern"), selected)
                : " ";
            StartupDisableButton.IsEnabled = selected > 0 && !_startupBusy;
            StartupEnableButton.IsEnabled = selected > 0 && !_startupBusy;
        }

        private async Task RunStartupActionAsync(bool enable)
        {
            if (_startupRows == null || _startupBusy) return;
            var selected = _startupRows.Where(r => r.CheckBox.IsChecked == true).ToList();
            if (selected.Count == 0) return;

            _startupBusy = true;
            StartupDisableButton.IsEnabled = false;
            StartupEnableButton.IsEnabled = false;
            foreach (StartupRow row in _startupRows)
                row.CheckBox.IsEnabled = false;
            StartupStatusText.Text = LocalizationService.L(enable ? "StartupEnabling" : "StartupDisabling");

            int changed = 0;
            try
            {
                foreach (StartupRow row in selected)
                {
                    // Only flip rows whose state actually changes.
                    if (row.Entry.Enabled == enable) continue;
                    bool ok = await Task.Run(() => StartupService.SetEnabled(row.Entry, enable));
                    if (ok)
                    {
                        row.Entry.Enabled = enable;
                        changed++;
                    }
                }

                if (changed > 0)
                {
                    MainWindow.ShowAppToast(string.Format(
                        LocalizationService.L(enable
                            ? "StartupEnabledToastPattern"
                            : "StartupDisabledToastPattern"), changed), success: true);
                }

                // Refresh from the real state.
                List<StartupEntry> entries = await Task.Run(StartupService.GetEntries);
                await Dispatcher.InvokeAsync(() =>
                {
                    _startupRows = entries.Select(e => new StartupRow { Entry = e }).ToList();
                    foreach (StartupRow row in _startupRows)
                        BuildStartupCard(row);
                    RebuildStartupList();
                });
            }
            finally
            {
                _startupBusy = false;
                foreach (StartupRow row in _startupRows ?? new List<StartupRow>())
                    row.CheckBox.IsEnabled = true;
                UpdateStartupFooter();
            }
        }

        private void StartupDisable_Click(object sender, RoutedEventArgs e) =>
            _ = RunStartupActionAsync(enable: false);

        private void StartupEnable_Click(object sender, RoutedEventArgs e) =>
            _ = RunStartupActionAsync(enable: true);

        // ---------------- USB buffer sliders ----------------

        // The USB page replaces the old per-size buffer cards with one slider
        // card per device class. State comes straight from the registry, so
        // values applied by Game Packages or the old cards show up here too.

        private bool _usbBusy;

        private int SelectedKbPackets =>
            TweakService.UsbBufferSizes[Math.Clamp((int)KbSlider.Value, 0, TweakService.UsbBufferSizes.Count - 1)];

        private int SelectedMousePackets =>
            TweakService.UsbBufferSizes[Math.Clamp((int)MouseSlider.Value, 0, TweakService.UsbBufferSizes.Count - 1)];

        private void KbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            UpdateUsbUi(keyboard: true);

        private void MouseSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            UpdateUsbUi(keyboard: false);

        private async void KbApply_Click(object sender, RoutedEventArgs e) =>
            await RunUsbAsync(keyboard: true, apply: true);

        private async void KbRevert_Click(object sender, RoutedEventArgs e) =>
            await RunUsbAsync(keyboard: true, apply: false);

        private async void MouseApply_Click(object sender, RoutedEventArgs e) =>
            await RunUsbAsync(keyboard: false, apply: true);

        private async void MouseRevert_Click(object sender, RoutedEventArgs e) =>
            await RunUsbAsync(keyboard: false, apply: false);

        private async Task RunUsbAsync(bool keyboard, bool apply)
        {
            if (_usbBusy) return;

            // Extreme low buffers get a final confirmation: users reported
            // the cursor jumping, moving on its own or lagging with 5 packets.
            if (apply)
            {
                int selected = keyboard ? SelectedKbPackets : SelectedMousePackets;
                if (selected <= 5)
                {
                    var confirm = MessageBox.Show(
                        string.Format(LocalizationService.L("UsbConfirmBody"),
                            LocalizationService.L(keyboard ? "UsbKeyboardName" : "UsbMouseName"),
                            string.Format(LocalizationService.L("UsbPacketsPattern"), selected)),
                        LocalizationService.L("UsbConfirmTitle"),
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes) return;
                }
            }

            _usbBusy = true;
            if (keyboard)
            {
                KbStatusText.Text = LocalizationService.L(apply ? "StatusApplying" : "StatusReverting");
            }
            else
            {
                MouseStatusText.Text = LocalizationService.L(apply ? "StatusApplying" : "StatusReverting");
            }
            UpdateUsbUi(keyboard);
            UpdateUsbUi(!keyboard);
            try
            {
                int? packets = apply ? (keyboard ? SelectedKbPackets : SelectedMousePackets) : null;
                await TweakService.RunUsbBufferAsync(keyboard, packets);
            }
            finally
            {
                _usbBusy = false;
                if (keyboard) KbStatusText.Text = " ";
                else MouseStatusText.Text = " ";
                RefreshUsbPanel();
            }
        }

        // Snaps both sliders to the currently applied values (page open only,
        // so a language refresh never yanks them away mid-drag).
        private void SyncUsbSlidersToApplied()
        {
            SyncOneSlider(TweakService.CurrentKeyboardBuffer(), KbSlider);
            SyncOneSlider(TweakService.CurrentMouseBuffer(), MouseSlider);
        }

        private static void SyncOneSlider(int? current, Slider slider)
        {
            if (current is not { } packets) return;
            for (int i = 0; i < TweakService.UsbBufferSizes.Count; i++)
            {
                if (TweakService.UsbBufferSizes[i] == packets)
                {
                    slider.Value = i;
                    return;
                }
            }
        }

        // Reloads the applied-state lines; called when the page opens, after
        // a run finishes and on language changes.
        private void RefreshUsbPanel()
        {
            KbStateText.Text = FormatUsbState(TweakService.CurrentKeyboardBuffer());
            MouseStateText.Text = FormatUsbState(TweakService.CurrentMouseBuffer());
            UpdateUsbUi(keyboard: true);
            UpdateUsbUi(keyboard: false);
        }

        private static string FormatUsbState(int? current) => current is { } packets
            ? string.Format(LocalizationService.L("UsbStatePattern"), packets)
            : LocalizationService.L("UsbStateDefault");

        // Updates the readout and button states for one card.
        private void UpdateUsbUi(bool keyboard)
        {
            int packets = keyboard ? SelectedKbPackets : SelectedMousePackets;
            int? current = keyboard ? TweakService.CurrentKeyboardBuffer() : TweakService.CurrentMouseBuffer();
            Slider slider = keyboard ? KbSlider : MouseSlider;
            Button apply = keyboard ? KbApplyButton : MouseApplyButton;
            Button revert = keyboard ? KbRevertButton : MouseRevertButton;

            string value = string.Format(LocalizationService.L("UsbPacketsPattern"), packets);
            if (keyboard)
            {
                KbValueText.Text = value;
                UpdateUsbWarning(KbWarnText, packets);
            }
            else
            {
                MouseValueText.Text = value;
                UpdateUsbWarning(MouseWarnText, packets);
            }

            slider.IsEnabled = !_usbBusy;
            apply.IsEnabled = !_usbBusy && current != packets;
            revert.IsEnabled = !_usbBusy && current != null;
        }

        // Shows the low-buffer warning for the selected size: red for the
        // extreme 5-packet buffer (reported to make the cursor jump, drift
        // or lag), amber for 10.
        private static void UpdateUsbWarning(TextBlock warnText, int packets)
        {
            if (packets <= 5)
            {
                warnText.Text = LocalizationService.L("UsbWarnExtreme");
                warnText.Foreground = (Brush)Application.Current.FindResource("BrushRiskHigh");
                warnText.Visibility = Visibility.Visible;
            }
            else if (packets <= 10)
            {
                warnText.Text = LocalizationService.L("UsbWarnLow");
                warnText.Foreground = (Brush)Application.Current.FindResource("BrushRiskMod");
                warnText.Visibility = Visibility.Visible;
            }
            else
            {
                warnText.Text = " ";
                warnText.Visibility = Visibility.Collapsed;
            }
        }

        // ---------------- Collapsible sidebar ----------------

        // The sidebar animates between 220px (expanded) and a 64px icon-only
        // rail. The column width animates via GridLengthAnimation; the labels
        // FADE out (never clipped) and are removed from the layout once the
        // fade completes.

        private bool _sidebarCollapsed;

        /// <summary>Animated 1 -> 0 as the labels fade out.</summary>
        public static readonly DependencyProperty SidebarLabelOpacityProperty =
            DependencyProperty.Register(nameof(SidebarLabelOpacity), typeof(double),
                typeof(MainWindow), new PropertyMetadata(1.0));

        public double SidebarLabelOpacity
        {
            get => (double)GetValue(SidebarLabelOpacityProperty);
            set => SetValue(SidebarLabelOpacityProperty, value);
        }

        /// <summary>False while collapsed: hides labels, language box,
        /// subscription badge and brand text from the layout.</summary>
        public static readonly DependencyProperty SidebarExpandedProperty =
            DependencyProperty.Register(nameof(SidebarExpanded), typeof(bool),
                typeof(MainWindow), new PropertyMetadata(true));

        public bool SidebarExpanded
        {
            get => (bool)GetValue(SidebarExpandedProperty);
            set => SetValue(SidebarExpandedProperty, value);
        }

        /// <summary>Left when expanded, Center when collapsed (icon rail).</summary>
        public static readonly DependencyProperty SidebarItemsAlignmentProperty =
            DependencyProperty.Register(nameof(SidebarItemsAlignment), typeof(HorizontalAlignment),
                typeof(MainWindow), new PropertyMetadata(HorizontalAlignment.Left));

        public HorizontalAlignment SidebarItemsAlignment
        {
            get => (HorizontalAlignment)GetValue(SidebarItemsAlignmentProperty);
            set => SetValue(SidebarItemsAlignmentProperty, value);
        }

        /// <summary>Right when expanded, Center when collapsed (toggle button).</summary>
        public static readonly DependencyProperty SidebarToggleAlignmentProperty =
            DependencyProperty.Register(nameof(SidebarToggleAlignment), typeof(HorizontalAlignment),
                typeof(MainWindow), new PropertyMetadata(HorizontalAlignment.Right));

        public HorizontalAlignment SidebarToggleAlignment
        {
            get => (HorizontalAlignment)GetValue(SidebarToggleAlignmentProperty);
            set => SetValue(SidebarToggleAlignmentProperty, value);
        }

        /// <summary>True while collapsed: shows the compact rail buttons.</summary>
        public static readonly DependencyProperty SidebarCollapsedProperty =
            DependencyProperty.Register(nameof(SidebarCollapsed), typeof(bool),
                typeof(MainWindow), new PropertyMetadata(false));

        public bool SidebarCollapsed
        {
            get => (bool)GetValue(SidebarCollapsedProperty);
            set => SetValue(SidebarCollapsedProperty, value);
        }

        /// <summary>ContentPresenter inset inside sidebar items: 8px when
        /// expanded, 0 in the rail so icons center.</summary>
        public static readonly DependencyProperty SidebarContentOffsetProperty =
            DependencyProperty.Register(nameof(SidebarContentOffset), typeof(Thickness),
                typeof(MainWindow), new PropertyMetadata(new Thickness(8, 0, 0, 0)));

        public Thickness SidebarContentOffset
        {
            get => (Thickness)GetValue(SidebarContentOffsetProperty);
            set => SetValue(SidebarContentOffsetProperty, value);
        }

        /// <summary>Category icon margin: asymmetric next to the label,
        /// zero in the centered rail.</summary>
        public static readonly DependencyProperty SidebarIconMarginProperty =
            DependencyProperty.Register(nameof(SidebarIconMargin), typeof(Thickness),
                typeof(MainWindow), new PropertyMetadata(new Thickness(2, 0, 10, 0)));

        public Thickness SidebarIconMargin
        {
            get => (Thickness)GetValue(SidebarIconMarginProperty);
            set => SetValue(SidebarIconMarginProperty, value);
        }

        /// <summary>Selection accent bar offset: reaches past the 14px padding
        /// when expanded, sits at the rail edge when collapsed.</summary>
        public static readonly DependencyProperty SidebarAccentOffsetProperty =
            DependencyProperty.Register(nameof(SidebarAccentOffset), typeof(Thickness),
                typeof(MainWindow), new PropertyMetadata(new Thickness(-14, -10, 0, -10)));

        public Thickness SidebarAccentOffset
        {
            get => (Thickness)GetValue(SidebarAccentOffsetProperty);
            set => SetValue(SidebarAccentOffsetProperty, value);
        }

        private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            bool collapsed = _sidebarCollapsed;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Adaptive insets for the 64px icon rail.
            SidebarItemsAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            SidebarToggleAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
            SidebarContentOffset = collapsed ? new Thickness(0) : new Thickness(8, 0, 0, 0);
            SidebarIconMargin = collapsed ? new Thickness(0) : new Thickness(2, 0, 10, 0);
            SidebarAccentOffset = collapsed
                ? new Thickness(-6, -10, 0, -10)
                : new Thickness(-14, -10, 0, -10);

            if (collapsed)
            {
                // Fade the labels out FIRST while the sidebar is still full
                // width - nothing ever gets clipped - then remove them from
                // the layout and shrink the rail.
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(100))
                {
                    EasingFunction = ease
                };
                fadeOut.Completed += (_, _) =>
                {
                    SidebarExpanded = false;
                    SidebarCollapsed = true;
                    AnimateSidebarWidth(64, ease);
                };
                BeginAnimation(SidebarLabelOpacityProperty, fadeOut);
            }
            else
            {
                // Grow the rail back FIRST; only when there is room again do
                // the wide elements (brand, language box, badge, pill)
                // return to the layout and the labels fade in - so nothing
                // is ever clipped mid-growth either.
                var grow = new GridLengthAnimation
                {
                    From = new GridLength(SidebarColumn.Width.Value, GridUnitType.Pixel),
                    To = new GridLength(220, GridUnitType.Pixel),
                    Duration = TimeSpan.FromMilliseconds(200),
                    Easing = ease
                };
                grow.Completed += (_, _) =>
                {
                    SidebarExpanded = true;
                    SidebarCollapsed = false;
                    BeginAnimation(SidebarLabelOpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
                };
                SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, grow);
            }
        }

        private void AnimateSidebarWidth(double targetWidth, IEasingFunction ease)
        {
            var width = new GridLengthAnimation
            {
                From = new GridLength(SidebarColumn.Width.Value, GridUnitType.Pixel),
                To = new GridLength(targetWidth, GridUnitType.Pixel),
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = ease
            };
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, width);
        }

        private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageBox.SelectedItem is ComboBoxItem { Tag: string code })
                LocalizationService.Set(LocalizationService.FromCode(code), persist: true);
        }

        // Opens the accent color palette; the theme applies live and persists.
        private void AppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AppearanceWindow { Owner = this };
            window.ShowDialog();
        }

        // ---------------- RAM profile slider ----------------

        // The RAM page is a single card: a draggable slider that snaps to the
        // supported sizes (slider value = index into TweakService.RamSizes),
        // plus Apply / Revert buttons.

        private bool _ramBusy;

        private int SelectedRamGb =>
            TweakService.RamSizes[Math.Clamp((int)RamSlider.Value, 0, TweakService.RamSizes.Count - 1)];

        private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            RefreshRamUi();

        private async void RamApply_Click(object sender, RoutedEventArgs e) => await RunRamAsync(apply: true);

        private async void RamRevert_Click(object sender, RoutedEventArgs e) => await RunRamAsync(apply: false);

        private async Task RunRamAsync(bool apply)
        {
            if (_ramBusy) return;

            _ramBusy = true;
            RamStatusText.Text = LocalizationService.L(apply ? "StatusApplying" : "StatusReverting");
            RefreshRamUi();
            try
            {
                await TweakService.RunRamProfileAsync(SelectedRamGb, apply);
            }
            finally
            {
                _ramBusy = false;
                RamStatusText.Text = " ";
                RefreshRamPanel();
            }
        }

        // Snaps the slider to the currently applied profile (page open only,
        // so a language refresh never yanks it away mid-drag).
        private void SyncRamSliderToApplied()
        {
            if (TweakService.CurrentRamProfile() is not { } gb) return;
            for (int i = 0; i < TweakService.RamSizes.Count; i++)
            {
                if (TweakService.RamSizes[i] == gb)
                {
                    RamSlider.Value = i;
                    return;
                }
            }
        }

        // Reloads the applied-state line; called when the page opens, after a
        // run finishes and on language changes.
        private void RefreshRamPanel()
        {
            RamStateText.Text = TweakService.CurrentRamProfile() is { } gb
                ? string.Format(LocalizationService.L("RamStatePattern"), gb)
                : LocalizationService.L("RamStateNone");
            RefreshRamUi();
        }

        // Updates the readout and button states for the current
        // slider position, busy state and applied state.
        private void RefreshRamUi()
        {
            int gb = SelectedRamGb;
            int? applied = TweakService.CurrentRamProfile();

            RamValueText.Text = $"{gb} GB";

            RamSlider.IsEnabled = !_ramBusy;
            RamApplyButton.IsEnabled = !_ramBusy && applied != gb;
            RamRevertButton.IsEnabled = !_ramBusy && applied != null;
        }

        private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryList.SelectedItem is not CategoryItem cat) return;

            _currentCategory = cat.Key;
            bool isHome         = cat.Key == "Home";
            bool isGameLauncher = cat.Key == "Game Launcher";
            bool isPackages     = cat.Key == "Game Packages";
            bool isRam          = cat.Key == "RAM";
            bool isScan         = cat.Key == "Scan";
            bool isUwp          = cat.Key == "UWP Apps";
            bool isStartup      = cat.Key == "Startup Apps";
            bool isUsb          = cat.Key == "USB";

            // Resource monitors only run while the Home page is visible.
            if (isHome) StartMonitors();
            else StopMonitors();

            // â”€â”€â”€ Panel visibility â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            WelcomePanel.Visibility      = isHome         ? Visibility.Visible : Visibility.Collapsed;
            GameLauncherPanel.Visibility = isGameLauncher ? Visibility.Visible : Visibility.Collapsed;
            PackagesPanel.Visibility     = isPackages     ? Visibility.Visible : Visibility.Collapsed;
            RamPanel.Visibility          = isRam          ? Visibility.Visible : Visibility.Collapsed;
            ScanPanel.Visibility         = isScan         ? Visibility.Visible : Visibility.Collapsed;
            UwpPanel.Visibility          = isUwp          ? Visibility.Visible : Visibility.Collapsed;
            StartupPanel.Visibility      = isStartup      ? Visibility.Visible : Visibility.Collapsed;
            UsbPanel.Visibility          = isUsb          ? Visibility.Visible : Visibility.Collapsed;

            bool isSpecialPage = isHome || isGameLauncher || isPackages || isRam || isScan || isUwp || isStartup || isUsb;
            ListHeader.Visibility  = isSpecialPage ? Visibility.Collapsed : Visibility.Visible;
            TweakScroll.Visibility = isSpecialPage ? Visibility.Collapsed : Visibility.Visible;

            // â”€â”€â”€ Per-page logic â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (isGameLauncher)
            {
                GameListControl.ItemsSource = null;
                GameListControl.ItemsSource = Services.GameLauncherService.GetGames();
            }

            if (isPackages)
                RefreshPackagesPanel();

            if (isRam)
            {
                SyncRamSliderToApplied();
                RefreshRamPanel();
            }

            if (isScan)
                RefreshScanPage();

            if (isUwp)
                RefreshUwpPage();

            if (isStartup)
                RefreshStartupPage();

            if (isUsb)
            {
                SyncUsbSlidersToApplied();
                RefreshUsbPanel();
            }

            if (!isSpecialPage)
            {
                HeaderText.Text = string.Format(LocalizationService.L("HeaderPattern"),
                    LocalizationService.L("Cat_" + cat.Key));

                bool isUsbCat = cat.Key == "USB";
                TweakList.ItemsSource = _allTweaks
                    .Where(t => t.Category == cat.Key && (!isUsbCat || t.Category != "USB"))
                    .ToList();

                TweakScroll.ScrollToTop();
            }

            AnimatePageIn();
        }

        private void RefreshPackagesPanel()
        {
            if (PackageList.ItemsSource == null)
            {
                _packageVms ??= GamePackageService.GetAll()
                    .Select(p => new GamePackageViewModel(p)).ToList();
                PackageList.ItemsSource = _packageVms;
            }
        }

        // Page transition animation: content slides up gently into view.
        private void AnimatePageIn()
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = ease
            };
            PageHost.BeginAnimation(OpacityProperty, fade);

            if (PageHost.RenderTransform is TranslateTransform slide)
            {
                slide.BeginAnimation(
                    TranslateTransform.YProperty,
                    new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = ease });
            }
        }

        // ===================== GAME LAUNCHER =====================

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executables (*.exe)|*.exe",
                Title = LocalizationService.L("SelectGameExe")
            };

            if (dlg.ShowDialog() == true)
            {
                Services.GameLauncherService.AddGame(dlg.FileName);
                GameListControl.ItemsSource = null;
                GameListControl.ItemsSource = Services.GameLauncherService.GetGames();
            }
        }

        private async void PlayGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is string path)
            {
                var result = MessageBox.Show(
                    LocalizationService.L("LaunchWarningBody"),
                    LocalizationService.L("LaunchWarningTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                await Services.GameLauncherService.LaunchGameAsync(path);
            }
        }

        private void RemoveGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is string path)
            {
                Services.GameLauncherService.RemoveGame(path);
                GameListControl.ItemsSource = null;
                GameListControl.ItemsSource = Services.GameLauncherService.GetGames();
            }
        }

    }
}
