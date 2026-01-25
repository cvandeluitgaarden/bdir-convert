using System.Security.Cryptography;
using System.Text;

namespace Bdir.Convert.Core.Hashing;

/// <summary>
/// Deterministic hashing utilities shared by extractors and patch application.
/// Mirrors the RFC baseline: UTF-8 bytes of NFC-normalized text.
/// </summary>
public static class TextHash
{
    public static string HashUtf8Hex(string algorithm, string text, bool normalizeNfc)
    {
        // Baseline: sha256 only (matches current extractor support).
        var canonical = NormalizeUnicode(text ?? string.Empty, normalizeNfc);
        var bytes = Encoding.UTF8.GetBytes(canonical);

        return (algorithm ?? "sha256").ToLowerInvariant() switch
        {
            "sha256" => Sha256Hex(bytes),
            _ => throw new NotSupportedException($"Unsupported hash_algorithm: '{algorithm}'")
        };
    }

    public static string NormalizeUnicode(string s, bool nfc)
        => nfc ? (s ?? string.Empty).Normalize(NormalizationForm.FormC) : (s ?? string.Empty);

    private static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return System.Convert.ToHexString(hash).ToLowerInvariant();
    }
}
