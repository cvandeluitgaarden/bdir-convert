using System.Text.Json.Serialization;
using Bdir.Convert.Core.Models;

namespace Bdir.Convert.Html.Tests.TestSupport;

internal sealed class WireBdirDocument
{
    [JsonPropertyName("v")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("ha")]
    public string HashAlgorithm { get; init; } = "sha256";

    [JsonPropertyName("h")]
    public string PageHash { get; init; } = "";

    [JsonPropertyName("b")]
    public List<WireBlockTuple> Blocks { get; init; } = [];

    internal static WireBdirDocument From(BdirDocument doc)
    {
        return new WireBdirDocument
        {
            Version = doc.Version,
            HashAlgorithm = doc.HashAlgorithm,
            PageHash = doc.PageHash,
            Blocks = [.. doc.Blocks.Select(WireBlockTuple.From)]
        };
    }
}

internal sealed class WireBlockTuple
{
    // Edit Packet tuple: [block_id, kind_code, text_hash, text]
    // We store it as an array-like object for readability + schema alignment.

    [JsonPropertyName("block_id")]
    public string BlockId { get; init; } = "";

    [JsonPropertyName("kind_code")]
    public int KindCode { get; init; }

    [JsonPropertyName("text_hash")]
    public string TextHash { get; init; } = "";

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    internal static WireBlockTuple From(BdirBlock b)
        => new()
        {
            BlockId = b.BlockId,
            KindCode = b.KindCode,
            TextHash = b.TextHash,
            Text = b.Text
        };
}
