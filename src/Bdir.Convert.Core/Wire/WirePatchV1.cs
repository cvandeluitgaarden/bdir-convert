using System.Text.Json.Serialization;

namespace Bdir.Convert.Core.Wire;

/// <summary>
/// Patch wire form (RFC-ish):
/// { "v":1, "h":"...", "ha":"sha256", "ops":[ ... ] }
/// </summary>
public sealed class WirePatchV1
{
    [JsonPropertyName("v")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("h")]
    public string PageHash { get; init; } = "";

    // Optional in RFC; defaulting rule is handled at load time.
    [JsonPropertyName("ha")]
    public string? HashAlgorithm { get; init; }

    [JsonPropertyName("ops")]
    public List<WirePatchOp> Ops { get; set; } = [];
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(WireReplaceOp), "replace")]
[JsonDerivedType(typeof(WireDeleteOp), "delete")]
[JsonDerivedType(typeof(WireInsertAfterOp), "insert_after")]
[JsonDerivedType(typeof(WireSuggestOp), "suggest")]
public abstract class WirePatchOp
{
    [JsonPropertyName("block_id")]
    public string BlockId { get; init; } = "";
}

public sealed class WireReplaceOp : WirePatchOp
{
    [JsonPropertyName("before")]
    public string Before { get; init; } = "";

    [JsonPropertyName("after")]
    public string After { get; init; } = "";

    [JsonPropertyName("occurrence")]
    public int? Occurrence { get; init; }
}

public sealed class WireDeleteOp : WirePatchOp
{
    [JsonPropertyName("before")]
    public string Before { get; init; } = "";

    [JsonPropertyName("occurrence")]
    public int? Occurrence { get; init; }
}

public sealed class WireInsertAfterOp : WirePatchOp
{
    [JsonPropertyName("new_block_id")]
    public string NewBlockId { get; init; } = "";

    [JsonPropertyName("kind_code")]
    public int KindCode { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

public sealed class WireSuggestOp : WirePatchOp
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("severity")]
    public string? Severity { get; init; }
}
