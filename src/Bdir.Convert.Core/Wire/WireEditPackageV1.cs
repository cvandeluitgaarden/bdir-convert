using System.Text.Json.Serialization;
using Bdir.Convert.Core.Models;

namespace Bdir.Convert.Core.Wire;

/// <summary>
/// Edit Packet wire form (RFC-ish):
/// { "v":1, "h":"...", "ha":"sha256", "b":[ ["block_id", kind_code, "text_hash", "text"], ... ] }
/// </summary>
public sealed class WireEditPacketV1
{
    [JsonPropertyName("v")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("h")]
    public string PageHash { get; init; } = "";

    [JsonPropertyName("ha")]
    public string HashAlgorithm { get; init; } = "sha256";

    [JsonPropertyName("b")]
    public List<object[]> Blocks { get; init; } = [];

    public static WireEditPacketV1 From(BdirDocument doc)
        => new()
        {
            Version = doc.Version,
            PageHash = doc.PageHash,
            HashAlgorithm = doc.HashAlgorithm,
            Blocks = [.. doc.Blocks.Select(ToTuple)]
        };

    private static object[] ToTuple(BdirBlock block)
        => [block.BlockId, block.KindCode, block.TextHash, block.Text];
}
