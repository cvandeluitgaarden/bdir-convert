using System.Text.Json;

namespace Bdir.Convert.Html.Tests.TestSupport;

internal static class GoldenTestHelpers
{
    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    internal static string FindProjectDirectory(string csprojName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, csprojName);
            if (File.Exists(candidate))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate {csprojName} above {AppContext.BaseDirectory}");
    }

    internal static string SerializeCanonical<T>(T value)
        => JsonAssert.Normalize(JsonSerializer.Serialize(value, JsonOpts));
}
