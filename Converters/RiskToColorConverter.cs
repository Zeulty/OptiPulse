using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ChlorideTweaks.Models;

namespace ChlorideTweaks.Converters
{
    /// <summary>Maps a RiskLevel to the matching glow color.</summary>
    public class RiskToColorConverter : IValueConverter
    {
        private static readonly Color Safe = (Color)ColorConverter.ConvertFromString("#33E39A");
        private static readonly Color Moderate = (Color)ColorConverter.ConvertFromString("#FFC53D");
        private static readonly Color High = (Color)ColorConverter.ConvertFromString("#F0286F");

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is RiskLevel risk ? risk switch
            {
                RiskLevel.Safe => Safe,
                RiskLevel.Moderate => Moderate,
                _ => High
            } : Safe;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
