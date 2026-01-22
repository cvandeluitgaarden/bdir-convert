using System.Text.Json;

namespace Bdir.Convert.Core.Wire;

public static class WireJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Canonical JSON serialization (parse + reserialize) for stable golden files.
    /// </summary>
    public static string SerializeCanonical<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, Options);
    }
}
