using System.Reflection;
using Microsoft.Win32;

namespace ChlorideTweaks.Models
{
    /// <summary>
    /// One registry modification applied directly with Microsoft.Win32.RegistryKey
    /// (no reg.exe import, no .reg file on disk). A set operation writes Value
    /// with the given kind; the delete variants remove a value or a whole key.
    /// The "{APPEXE}" placeholder inside string values expands to the running
    /// OptiPulse.exe path at execution time.
    /// </summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class RegistryChange
    {
        public RegistryHive Hive { get; init; }

        public string KeyPath { get; init; } = "";

        /// <summary>Value name; null means the key's default value.</summary>
        public string? ValueName { get; init; }

        public object? Value { get; init; }

        public RegistryValueKind Kind { get; init; } = RegistryValueKind.DWord;

        /// <summary>Removes the whole key tree instead of writing a value.</summary>
        public bool DeleteKey { get; init; }

        /// <summary>Removes the named value instead of writing it.</summary>
        public bool DeleteValue { get; init; }

        public static RegistryChange Set(RegistryHive hive, string keyPath, string? valueName,
            object value, RegistryValueKind kind) => new()
        {
            Hive = hive,
            KeyPath = keyPath,
            ValueName = valueName,
            Value = value,
            Kind = kind
        };

        public static RegistryChange RemoveValue(RegistryHive hive, string keyPath, string valueName) => new()
        {
            Hive = hive,
            KeyPath = keyPath,
            ValueName = valueName,
            DeleteValue = true
        };

        public static RegistryChange RemoveKey(RegistryHive hive, string keyPath) => new()
        {
            Hive = hive,
            KeyPath = keyPath,
            DeleteKey = true
        };
    }
}
