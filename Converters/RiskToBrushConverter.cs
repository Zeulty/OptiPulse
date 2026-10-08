using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ChlorideTweaks.Models;

namespace ChlorideTweaks.Converters
{
    /// <summary>Maps a RiskLevel to the matching neon brush (green/yellow/red).</summary>
    public class RiskToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Safe = Freeze("#33E39A");
        private static readonly SolidColorBrush Moderate = Freeze("#FFC53D");
        private static readonly SolidColorBrush High = Freeze("#F0286F");

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is RiskLevel risk ? risk switch
            {
                RiskLevel.Safe => Safe,
                RiskLevel.Moderate => Moderate,
                _ => High
            } : Safe;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static SolidColorBrush Freeze(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
