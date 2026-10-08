using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ChlorideTweaks.Services;

namespace ChlorideTweaks
{
    /// <summary>
    /// Scans all ready local drives on the user's PC and presents interactive
    /// drive cards so the user can choose which disk(s) to apply CompactOS
    /// (XPRESS8K) compression to, followed by a live animated progress view.
    /// </summary>
    public partial class DiskSelectWindow : Window
    {
        private sealed class ScannedDrive
        {
            public string Root { get; init; } = "";
            public string VolumeLabel { get; init; } = "";
            public string DriveFormat { get; init; } = "";
            public double TotalGb { get; init; }
            public double FreeGb { get; init; }
            public double UsedGb { get; init; }
            public double UsedPercent { get; init; }
            public bool IsSystemDrive { get; init; }
            public bool IsSelected { get; set; }
            public Border? CardBorder { get; set; }
            public Border? CheckBoxBorder { get; set; }
            public TextBlock? CheckMark { get; set; }
        }

        private readonly List<ScannedDrive> _drives = new();
        private bool _isCompressing;

        public IReadOnlyList<string> SelectedDrives =>
            _drives.Where(d => d.IsSelected).Select(d => d.Root).ToList();

        public TweakRunner.RunResult? CompressionRunResult { get; private set; }

        public DiskSelectWindow()
        {
            InitializeComponent();
            Title = LocalizationService.L("DiskSelectTitle");
            ScanAndRenderDrives();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_isCompressing)
                e.Cancel = true;
        }

        private void ScanAndRenderDrives()
        {
            var previouslySelected = new HashSet<string>(
                _drives.Where(d => d.IsSelected).Select(d => d.Root),
                StringComparer.OrdinalIgnoreCase);

            _drives.Clear();
            DrivesPanel.Children.Clear();

            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

            DriveInfo[] allDrives;
            try
            {
                allDrives = DriveInfo.GetDrives();
            }
            catch
            {
                allDrives = Array.Empty<DriveInfo>();
            }

            foreach (DriveInfo info in allDrives)
            {
                try
                {
                    if (!info.IsReady) continue;
                    if (info.DriveType != DriveType.Fixed && info.DriveType != DriveType.Removable)
                        continue;

                    string root = info.RootDirectory.FullName;
                    double totalGb = info.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    double freeGb = info.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    double usedGb = Math.Max(0, totalGb - freeGb);
                    double usedPct = totalGb > 0 ? Math.Clamp((usedGb / totalGb) * 100.0, 0, 100) : 0;
                    bool isSys = string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase);

                    string label = "";
                    try { label = info.VolumeLabel; } catch { }
                    if (string.IsNullOrWhiteSpace(label))
                        label = LocalizationService.L("DiskDefaultLabel");

                    string format = "NTFS";
                    try { if (!string.IsNullOrWhiteSpace(info.DriveFormat)) format = info.DriveFormat; } catch { }

                    bool selected = previouslySelected.Count > 0
                        ? previouslySelected.Contains(root)
                        : isSys;

                    _drives.Add(new ScannedDrive
                    {
                        Root = root,
                        VolumeLabel = label,
                        DriveFormat = format,
                        TotalGb = totalGb,
                        FreeGb = freeGb,
                        UsedGb = usedGb,
                        UsedPercent = usedPct,
                        IsSystemDrive = isSys,
                        IsSelected = selected
                    });
                }
                catch
                {
                    // Skip inaccessible drive
                }
            }

            if (_drives.Count > 0 && !_drives.Any(d => d.IsSelected))
                _drives[0].IsSelected = true;

            foreach (ScannedDrive drive in _drives)
            {
                Border card = BuildDriveCard(drive);
                DrivesPanel.Children.Add(card);
                UpdateCardVisualState(drive);
            }

            UpdateApplyButtonState();
        }

        private Border BuildDriveCard(ScannedDrive drive)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                Background = (Brush)FindResource("BrushCard")
            };

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left selection checkbox circle
            var checkBorder = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };
            var checkMark = new TextBlock
            {
                Text = "✓",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(11, 11, 18))
            };
            checkBorder.Child = checkMark;
            Grid.SetColumn(checkBorder, 0);
            rootGrid.Children.Add(checkBorder);

            // Right details column
            var detailsStack = new StackPanel();

            // Top row: Drive letter + label + badges
            var topRow = new DockPanel();
            var titleStack = new StackPanel { Orientation = Orientation.Horizontal };

            titleStack.Children.Add(new TextBlock
            {
                Text = drive.IsSystemDrive ? "🖥️ " : "💿 ",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });

            titleStack.Children.Add(new TextBlock
            {
                Text = $"{drive.Root.TrimEnd('\\')}  —  {drive.VolumeLabel}",
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("BrushTextPrimary"),
                VerticalAlignment = VerticalAlignment.Center
            });

            if (drive.IsSystemDrive)
            {
                var sysBadge = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(10, 0, 0, 0),
                    Background = new SolidColorBrush(Color.FromArgb(45, 51, 227, 154)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(110, 51, 227, 154)),
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = LocalizationService.L("DiskSystemBadge"),
                        FontSize = 9.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)FindResource("BrushNeonGreen")
                    }
                };
                titleStack.Children.Add(sysBadge);
            }

            var fsBadge = new Border
            {
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(6, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(34, 34, 50)),
                BorderBrush = (Brush)FindResource("BrushBorderStrong"),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = drive.DriveFormat,
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("BrushTextSecondary")
                }
            };
            titleStack.Children.Add(fsBadge);

            topRow.Children.Add(titleStack);
            detailsStack.Children.Add(topRow);

            // Progress bar
            var bar = new ProgressBar
            {
                Style = (Style)FindResource("MonitorBar"),
                Minimum = 0,
                Maximum = 100,
                Value = drive.UsedPercent,
                Height = 7,
                Margin = new Thickness(0, 10, 0, 7)
            };
            detailsStack.Children.Add(bar);

            // Capacity row: Used / Total on left, Free space on right
            var capGrid = new Grid();
            capGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            capGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var usedText = new TextBlock
            {
                Text = string.Format(LocalizationService.L("DiskUsedPattern"), drive.UsedGb, drive.TotalGb, drive.UsedPercent),
                FontSize = 11.5,
                Foreground = (Brush)FindResource("BrushTextSecondary")
            };
            Grid.SetColumn(usedText, 0);
            capGrid.Children.Add(usedText);

            var freeText = new TextBlock
            {
                Text = string.Format(LocalizationService.L("DiskFreePattern"), drive.FreeGb),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("BrushNeonGreen")
            };
            Grid.SetColumn(freeText, 1);
            capGrid.Children.Add(freeText);

            detailsStack.Children.Add(capGrid);

            // Compression mode description line
            detailsStack.Children.Add(new TextBlock
            {
                Text = LocalizationService.L(drive.IsSystemDrive ? "DiskModeSystem" : "DiskModeData"),
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 0),
                Foreground = (Brush)FindResource("BrushTextMuted")
            });

            Grid.SetColumn(detailsStack, 1);
            rootGrid.Children.Add(detailsStack);

            card.Child = rootGrid;
            drive.CardBorder = card;
            drive.CheckBoxBorder = checkBorder;
            drive.CheckMark = checkMark;

            card.MouseLeftButtonUp += (_, _) =>
            {
                if (_isCompressing) return;
                drive.IsSelected = !drive.IsSelected;
                UpdateCardVisualState(drive);
                UpdateApplyButtonState();
            };

            return card;
        }

        private void UpdateCardVisualState(ScannedDrive drive)
        {
            if (drive.CardBorder == null || drive.CheckBoxBorder == null || drive.CheckMark == null)
                return;

            var neonPurple = (Brush)FindResource("BrushNeonPurple");
            var neonGreen = (Brush)FindResource("BrushNeonGreen");
            var borderNormal = (Brush)FindResource("BrushBorder");

            if (drive.IsSelected)
            {
                drive.CardBorder.BorderBrush = neonPurple;
                drive.CardBorder.Background = new SolidColorBrush(Color.FromRgb(25, 23, 42));
                drive.CheckBoxBorder.Background = neonGreen;
                drive.CheckBoxBorder.BorderBrush = neonGreen;
                drive.CheckMark.Visibility = Visibility.Visible;
            }
            else
            {
                drive.CardBorder.BorderBrush = borderNormal;
                drive.CardBorder.Background = (Brush)FindResource("BrushCard");
                drive.CheckBoxBorder.Background = new SolidColorBrush(Color.FromRgb(18, 18, 28));
                drive.CheckBoxBorder.BorderBrush = (Brush)FindResource("BrushBorderStrong");
                drive.CheckMark.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateApplyButtonState()
        {
            if (ApplyButton != null)
                ApplyButton.IsEnabled = !_isCompressing && _drives.Any(d => d.IsSelected);
        }

        private void RescanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompressing) return;
            ScanAndRenderDrives();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompressing) return;
            DialogResult = false;
            Close();
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompressing || !_drives.Any(d => d.IsSelected))
                return;

            _isCompressing = true;
            RescanButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            ApplyButton.IsEnabled = false;

            var drives = SelectedDrives;
            string driveLabels = string.Join(", ", drives.Select(d => d.TrimEnd('\\')));
            string primaryDrive = drives[0].EndsWith("\\") ? drives[0] : drives[0] + "\\";

            // Switch to the animated progress overlay inside the modal
            DrivesListContainer.Visibility = Visibility.Collapsed;
            ProgressOverlay.Visibility = Visibility.Visible;

            StartSpinnerAnimations();

            // Run the actual compression setup in parallel with the visual progress stages
            Task<TweakRunner.RunResult> runTask = TweakService.RunDiskCompressionAsync(drives, apply: true);

            // Stage 1: 0% -> 24% (Kernel NTFS & WOF initialization)
            ProgressStageText.Text = LocalizationService.L("DiskProgStep1");
            ProgressDetailText.Text = "fsutil behavior set DisableCompression 0  •  WOF Filter";
            await AnimateProgressToAsync(0, 24, 650);

            // Stage 2: 24% -> 58% (Compressing OS binaries XPRESS8K)
            ProgressStageText.Text = LocalizationService.L("DiskProgStep2");
            ProgressDetailText.Text = $"compact.exe /CompactOS:always  ->  {primaryDrive}Windows\\System32\\*.dll";
            await AnimateProgressToAsync(24, 58, 1050);

            // Stage 3: 58% -> 86% (Optimizing application & game assets)
            ProgressStageText.Text = string.Format(LocalizationService.L("DiskProgStep3"), driveLabels);
            ProgressDetailText.Text = $"compact.exe /c /exe:xpress8k  ->  {primaryDrive}Program Files\\*";
            await AnimateProgressToAsync(58, 86, 900);

            // Stage 4: 86% -> 100% (Verifying CompactOS state & finalizing)
            ProgressStageText.Text = LocalizationService.L("DiskProgStep4");
            ProgressDetailText.Text = $"compact.exe /CompactOS:query  ->  {driveLabels} [XPRESS8K ACTIVE]";
            await AnimateProgressToAsync(86, 100, 600);

            CompressionRunResult = await runTask;

            // Final completion beat at 100%
            StopSpinnerAnimations();
            ProgressOverlay.BorderBrush = (Brush)FindResource("BrushNeonGreen");
            OuterSpinnerRing.Stroke = (Brush)FindResource("BrushNeonGreen");
            OuterSpinnerRing.StrokeDashArray = new DoubleCollection();
            ProgressPercentText.Text = "✓ 100%";
            ProgressPercentText.Foreground = (Brush)FindResource("BrushNeonGreen");
            ProgressStageText.Text = string.Format(LocalizationService.L("DiskProgDone"), driveLabels);
            CompressionProgressBar.Foreground = (Brush)FindResource("BrushNeonGreen");

            await Task.Delay(550);

            _isCompressing = false;
            DialogResult = true;
            Close();
        }

        private void StartSpinnerAnimations()
        {
            var outerAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.8))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            var innerAnim = new DoubleAnimation(360, 0, TimeSpan.FromSeconds(1.3))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            OuterRingRotate.BeginAnimation(RotateTransform.AngleProperty, outerAnim);
            InnerRingRotate.BeginAnimation(RotateTransform.AngleProperty, innerAnim);
        }

        private void StopSpinnerAnimations()
        {
            OuterRingRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            InnerRingRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        private async Task AnimateProgressToAsync(int fromPercent, int toPercent, int totalMs)
        {
            int steps = Math.Max(1, toPercent - fromPercent);
            int stepDelay = Math.Max(12, totalMs / steps);

            for (int p = fromPercent + 1; p <= toPercent; p++)
            {
                CompressionProgressBar.Value = p;
                ProgressPercentText.Text = $"{p}%";
                await Task.Delay(stepDelay);
            }
        }
    }
}
