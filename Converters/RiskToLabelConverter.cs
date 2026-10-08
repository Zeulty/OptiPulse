using System;
using System.Globalization;
using System.Windows.Data;
using ChlorideTweaks.Models;
using ChlorideTweaks.Services;

namespace ChlorideTweaks.Converters
{
    /// <summary>Maps a RiskLevel to a short human-readable label.</summary>
    public class RiskToLabelConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is RiskLevel risk ? LocalizationService.L(risk switch
            {
                RiskLevel.Moderate => "RiskMod",
                RiskLevel.High => "RiskHigh",
                _ => "RiskSafe"
            }) : LocalizationService.L("RiskSafe");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
