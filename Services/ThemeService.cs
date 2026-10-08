using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ChlorideTweaks.Services
{
    /// <summary>
    /// Runtime accent-color theming. The whole interface is recolored by
    /// swapping the accent family resources (the neon purple/pink palette,
    /// gradients and glow effects) while the neutral backgrounds, text colors
    /// and the semantic colors (green = success, red = risk, yellow = warning)
    /// stay untouched, so nothing on screen ever loses its meaning.
    ///
    /// Live updates work through three mechanisms together:
    ///  - resource values are replaced, so everything bound with
    ///    DynamicResource (glow colors, gradient stops) refreshes instantly;
    ///  - the previous shared brush/gradient/effect instances are mutated
    ///    in place, so elements that resolved them with StaticResource
    ///    refresh too;
    ///  - the choice persists to state.json and is re-applied at startup,
    ///    before any window is created.
    ///
    /// Free for every user - no license checks here by design.
    /// </summary>
    public static class ThemeService
    {
        /// <summary>The built-in neon purple the app ships with.</summary>
        public static readonly Color DefaultAccent = Color.FromRgb(0x7B, 0x5C, 0xFA);

        /// <summary>Accent colors offered as presets in the appearance window.</summary>
        public static readonly Color[] Presets =
        {
            Color.FromRgb(0x7B, 0x5C, 0xFA),   // neon purple (default)
            Color.FromRgb(0x8A, 0x7C, 0xFF),   // periwinkle
            Color.FromRgb(0x4F, 0x8C, 0xFF),   // azure
            Color.FromRgb(0x2C, 0x7D, 0xE9),   // ocean blue
            Color.FromRgb(0x00, 0xCF, 0xE8),   // cyan
            Color.FromRgb(0x00, 0xD9, 0xA6),   // mint
            Color.FromRgb(0x33, 0xE3, 0x9A),   // green
            Color.FromRgb(0x9B, 0xE8, 0x35),   // lime
            Color.FromRgb(0xF5, 0xD9, 0x0A),   // yellow
            Color.FromRgb(0xFF, 0xB0, 0x20),   // amber
            Color.FromRgb(0xFF, 0x7A, 0x1A),   // orange
            Color.FromRgb(0xF0, 0x28, 0x6F),   // magenta
            Color.FromRgb(0xE2, 0x3F, 0xFF),   // violet
            Color.FromRgb(0xB4, 0xB8, 0xC4),   // silver
        };

        /// <summary>The accent currently applied (before any change this is
        /// DefaultAccent; Initialize() syncs it with the saved value).</summary>
        public static Color CurrentAccent { get; private set; } = DefaultAccent;

        /// <summary>Fired whenever the accent color changes (both live preview and committed).</summary>
        public static event Action<Color>? AccentChanged;

        private static bool _initialized;

        /// <summary>Applies the accent saved in state.json. Call once at app
        /// startup, before the first window is created.</summary>
        public static void Initialize()
        {
            string? saved = AppStateService.State.AccentColor;
            if (TryParseHex(saved, out Color accent))
                Apply(accent, persist: false);
            else
                Apply(DefaultAccent, persist: false);
        }

        /// <summary>Applies an accent color immediately (whole UI recolors
        /// live) and persists it for the next start.</summary>
        public static void SetAccent(Color accent) => Apply(accent, persist: true);

        /// <summary>Applies an accent color immediately WITHOUT persisting it -
        /// used for the live RGB slider preview, so dragging does not write
        /// state.json for every intermediate color. Follow up with
        /// PersistAccent() (the appearance window debounces this).</summary>
        public static void SetAccentPreview(Color accent) => Apply(accent, persist: false);

        /// <summary>Persists the currently applied accent.</summary>
        public static void PersistAccent() =>
            AppStateService.SetAccentColor(ToHex(CurrentAccent));

        /// <summary>Resets to the built-in neon purple.</summary>
        public static void ResetToDefault() => SetAccent(DefaultAccent);

        // ---------------- application ----------------

        private static void Apply(Color accent, bool persist)
        {
            if (Application.Current?.Resources == null) return;
            if (_initialized && accent == CurrentAccent) return;

            _initialized = true;
            CurrentAccent = accent;
            var resources = Application.Current.Resources;

            // Derived family colors.
            Color light = Lighten(accent, 0.18);          // hover / drag partner (old pink)
            Color dim = Dim(accent);                      // mascot halos
            Color glowBg = WithHsl(accent, l: 0.15, s: 0.6); // window backdrop halo

            // Color resources (DynamicResource users refresh instantly).
            resources["ColNeonPurple"] = accent;
            resources["ColNeonPink"] = light;
            resources["ColBgGlow"] = glowBg;
            resources["ColAccentSoft"] = WithAlpha(accent, 0x66);
            resources["ColAccentSoftFade"] = WithAlpha(accent, 0x00);
            resources["ColAccentDim"] = dim;
            resources["ColAccentGhost"] = WithAlpha(accent, 0x55);
            resources["ColNeonGreen"] = accent;
            resources["ColRiskSafe"] = accent;

            // Solid brushes: replace for future resolutions and mutate the
            // old shared instance so everything already on screen updates.
            ReplaceBrush(resources, "BrushNeonPurple", accent);
            ReplaceBrush(resources, "BrushNeonPink", light);
            ReplaceBrush(resources, "BrushAccentGhost", WithAlpha(accent, 0x55));
            ReplaceBrush(resources, "BrushNeonGreen", accent);
            ReplaceBrush(resources, "BrushRiskSafe", accent);

            // Sidebar edge gradient: transparent -> accent -> transparent.
            // The replacement is built from the new colors directly because
            // dictionary resources can be frozen and resist in-place changes.
            Color edgeMid = WithAlpha(accent, 0x4C);
            ReplaceBrush(resources, "BrushSidebarEdge", b =>
            {
                if (b is LinearGradientBrush g && g.GradientStops.Count == 3)
                {
                    MutateStop(g.GradientStops[1], edgeMid);
                    var clone = new LinearGradientBrush
                    {
                        StartPoint = g.StartPoint,
                        EndPoint = g.EndPoint
                    };
                    clone.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 0));
                    clone.GradientStops.Add(new GradientStop(edgeMid, 0.5));
                    clone.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 1));
                    return clone;
                }
                return null;
            });

            // Window backdrop radial: dim accent glow fading to the dark edges.
            ReplaceBrush(resources, "BrushBackdrop", b =>
            {
                if (b is RadialGradientBrush r && r.GradientStops.Count == 3)
                {
                    Color bg = (Color)resources["ColBg"];
                    Color deep = (Color)resources["ColBgDeep"];
                    MutateStop(r.GradientStops[0], glowBg);
                    MutateStop(r.GradientStops[1], bg);
                    MutateStop(r.GradientStops[2], deep);
                    var clone = new RadialGradientBrush
                    {
                        GradientOrigin = r.GradientOrigin,
                        Center = r.Center,
                        RadiusX = r.RadiusX,
                        RadiusY = r.RadiusY
                    };
                    clone.GradientStops.Add(new GradientStop(glowBg, 0));
                    clone.GradientStops.Add(new GradientStop(bg, 0.6));
                    clone.GradientStops.Add(new GradientStop(deep, 1));
                    return clone;
                }
                return null;
            });

            // Shared glow effects.
            ReplaceEffect(resources, "GlowPurple", accent, blurRadius: 16, opacity: 0.5);
            ReplaceEffect(resources, "GlowPink", light, blurRadius: 18, opacity: 0.55);

            if (persist)
                AppStateService.SetAccentColor(ToHex(accent));

            AccentChanged?.Invoke(accent);
        }

        // ---------------- resource helpers ----------------

        private static void ReplaceBrush(ResourceDictionary resources, string key, Color color)
        {
            if (resources[key] is SolidColorBrush old && !old.IsFrozen)
            {
                old.Color = color;
            }
            else
            {
                resources[key] = new SolidColorBrush(color);
            }
        }

        private static void ReplaceBrush(ResourceDictionary resources, string key,
            Func<Brush, Brush?> rebuild)
        {
            if (resources[key] is Brush old)
            {
                Brush? fresh = rebuild(old);
                if (fresh != null) resources[key] = fresh;
            }
        }

        private static void ReplaceEffect(ResourceDictionary resources, string key,
            Color color, double blurRadius, double opacity)
        {
            var fresh = new DropShadowEffect
            {
                Color = color,
                BlurRadius = blurRadius,
                ShadowDepth = 0,
                Opacity = opacity
            };
            if (resources[key] is DropShadowEffect old)
            {
                try
                {
                    if (!old.IsFrozen) old.Color = color;
                }
                catch { /* frozen - new consumers still get the fresh effect */ }
            }
            resources[key] = fresh;
        }

        private static void MutateSolid(SolidColorBrush brush, Color color)
        {
            try
            {
                if (!brush.IsFrozen) brush.Color = color;
            }
            catch { /* frozen brushes keep their color until recreated */ }
        }

        private static void MutateStop(GradientStop stop, Color color)
        {
            try
            {
                if (!stop.IsFrozen) stop.Color = color;
            }
            catch { /* frozen stops keep their color until recreated */ }
        }

        // ---------------- color math ----------------

        /// <summary>Lightens (amount &gt; 0) or darkens (amount &lt; 0) a color.</summary>
        private static Color Lighten(Color c, double amount)
        {
            ToHsl(c, out double h, out double s, out double l);
            l = Math.Clamp(l + amount, 0.0, 1.0);
            s *= 0.92;
            return FromHsl(h, s, l);
        }

        private static Color Dim(Color c)
        {
            ToHsl(c, out double h, out double s, out double l);
            return FromHsl(h, s * 0.65, Math.Clamp(l * 0.58, 0.05, 0.55));
        }

        private static Color WithHsl(Color c, double? h = null, double? s = null, double? l = null)
        {
            ToHsl(c, out double oh, out double os, out double ol);
            return FromHsl(h ?? oh, s ?? os, l ?? ol);
        }

        private static Color WithAlpha(Color c, byte alpha) =>
            Color.FromArgb(alpha, c.R, c.G, c.B);

        private static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2.0;

            if (max == min)
            {
                h = s = 0.0;
                return;
            }

            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == r) h = ((g - b) / d + (g < b ? 6.0 : 0.0)) * 60.0;
            else if (max == g) h = ((b - r) / d + 2.0) * 60.0;
            else h = ((r - g) / d + 4.0) * 60.0;
        }

        private static Color FromHsl(double h, double s, double l)
        {
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double hp = (h % 360.0 + 360.0) % 360.0 / 60.0;
            double x = c * (1.0 - Math.Abs(hp % 2.0 - 1.0));
            double r1 = 0, g1 = 0, b1 = 0;

            if (hp < 1) { r1 = c; g1 = x; }
            else if (hp < 2) { r1 = x; g1 = c; }
            else if (hp < 3) { g1 = c; b1 = x; }
            else if (hp < 4) { g1 = x; b1 = c; }
            else if (hp < 5) { r1 = x; b1 = c; }
            else { r1 = c; b1 = x; }

            double m = l - c / 2.0;
            return Color.FromRgb(
                (byte)Math.Round(Math.Clamp(r1 + m, 0.0, 1.0) * 255.0),
                (byte)Math.Round(Math.Clamp(g1 + m, 0.0, 1.0) * 255.0),
                (byte)Math.Round(Math.Clamp(b1 + m, 0.0, 1.0) * 255.0));
        }

        // ---------------- hex helpers ----------------

        public static string ToHex(Color c) =>
            $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static bool TryParseHex(string? text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string hex = text.Trim().TrimStart('#');
            if (hex.Length == 8) hex = hex.Substring(2);   // drop the alpha channel
            if (hex.Length != 6) return false;

            foreach (char ch in hex)
                if (!Uri.IsHexDigit(ch)) return false;

            color = Color.FromRgb(
                byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
            return true;
        }
    }
}
