using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ChlorideTweaks.Services;

namespace ChlorideTweaks
{
    /// <summary>
    /// Accent color palette for the whole interface. Preset swatches and the
    /// custom RGB / hex picker apply the theme live through ThemeService and
    /// persist it for the next start. Available to every user, free or VIP.
    /// </summary>
    public partial class AppearanceWindow : Window
    {
        private Border? _selectedSwatch;
        private bool _updating;

        // Slider drags apply the theme live but persist debounced, so rapid
        // changes do not write state.json for every intermediate color.
        private readonly DispatcherTimer _persistTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
        private bool _persistPending;

        public AppearanceWindow()
        {
            InitializeComponent();
            BuildPresets();
            LoadCurrent();

            _persistTimer.Tick += (_, _) =>
            {
                _persistTimer.Stop();
                _persistPending = false;
                ThemeService.PersistAccent();
            };
            Closing += (_, _) =>
            {
                if (_persistPending) ThemeService.PersistAccent();
            };
        }

        // ---------------- presets ----------------

        private void BuildPresets()
        {
            foreach (Color preset in ThemeService.Presets)
            {
                var color = preset;
                var swatch = new Border
                {
                    Style = (Style)FindResource("PresetSwatch"),
                    Background = new SolidColorBrush(color),
                    ToolTip = ThemeService.ToHex(color),
                    Tag = color
                };
                swatch.MouseLeftButtonUp += (_, _) => SelectPreset(color);
                PresetPanel.Children.Add(swatch);
            }
        }

        private void SelectPreset(Color color)
        {
            ThemeService.SetAccent(color);
            LoadCurrent();
        }

        private void HighlightSwatch()
        {
            if (_selectedSwatch != null)
            {
                _selectedSwatch.BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
                _selectedSwatch.Child = null;
            }

            _selectedSwatch = null;
            foreach (object child in PresetPanel.Children)
            {
                if (child is Border b && b.Tag is Color c && c == ThemeService.CurrentAccent)
                {
                    _selectedSwatch = b;
                    b.BorderBrush = Brushes.White;
                    b.Child = new TextBlock
                    {
                        Text = "✓",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    break;
                }
            }
        }

        // ---------------- loading / applying ----------------

        private void LoadCurrent()
        {
            _updating = true;
            Color c = ThemeService.CurrentAccent;
            RedSlider.Value = c.R;
            GreenSlider.Value = c.G;
            BlueSlider.Value = c.B;
            RedValue.Text = c.R.ToString();
            GreenValue.Text = c.G.ToString();
            BlueValue.Text = c.B.ToString();
            HexInput.Text = ThemeService.ToHex(c);
            HexPreview.Text = ThemeService.ToHex(c);
            _updating = false;

            HighlightSwatch();
        }

        private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updating) return;

            var color = Color.FromRgb(
                (byte)Math.Round(RedSlider.Value),
                (byte)Math.Round(GreenSlider.Value),
                (byte)Math.Round(BlueSlider.Value));

            RedValue.Text = color.R.ToString();
            GreenValue.Text = color.G.ToString();
            BlueValue.Text = color.B.ToString();
            HexInput.Text = ThemeService.ToHex(color);
            HexPreview.Text = ThemeService.ToHex(color);

            // Live preview while dragging; persisted once the drag settles.
            ThemeService.SetAccentPreview(color);
            _persistPending = true;
            _persistTimer.Stop();
            _persistTimer.Start();
            HighlightSwatch();
        }

        private void HexInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyHex();
                e.Handled = true;
            }
        }

        private void HexInput_LostFocus(object sender, RoutedEventArgs e) => ApplyHex();

        private void ApplyHex()
        {
            if (!ThemeService.TryParseHex(HexInput.Text, out Color color))
                return;   // keep the current theme on malformed input

            ThemeService.SetAccent(color);
            LoadCurrent();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeService.ResetToDefault();
            LoadCurrent();
        }
    }
}
