using System;

namespace Bdir.Convert.Html.Tests.TestSupport;

internal static class ConsoleStyle
{
    private static bool NoColor =>
        string.Equals(Environment.GetEnvironmentVariable("BDIR_NO_COLOR"), "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("BDIR_NO_COLOR"), "true", StringComparison.OrdinalIgnoreCase);

    private static bool UseAnsi =>
        !NoColor &&
        !Console.IsOutputRedirected &&
        // Windows 10+ supports ANSI in modern terminals; GH Actions is fine too.
        (OperatingSystem.IsWindowsVersionAtLeast(10) || !OperatingSystem.IsWindows());

    private const string Reset  = "\u001b[0m";
    private const string Green  = "\u001b[32m";
    private const string Red    = "\u001b[31m";
    private const string Yellow = "\u001b[33m";
    private const string Dim    = "\u001b[2m";

    public static string GreenText(string s)  => UseAnsi ? $"{Green}{s}{Reset}" : s;
    public static string RedText(string s)    => UseAnsi ? $"{Red}{s}{Reset}" : s;
    public static string YellowText(string s) => UseAnsi ? $"{Yellow}{s}{Reset}" : s;
    public static string DimText(string s)    => UseAnsi ? $"{Dim}{s}{Reset}" : s;
}
