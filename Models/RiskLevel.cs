using System.Reflection;

namespace ChlorideTweaks.Models
{
    /// <summary>
    /// How much a tweak can affect the system:
    /// Safe = no meaningful downsides; Moderate = real trade-offs or disabled
    /// features; High = can destabilize the PC or reduce security.
    /// </summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum RiskLevel
    {
        Safe,
        Moderate,
        High
    }
}
