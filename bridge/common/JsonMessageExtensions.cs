using System.Text.Json;
using BelgianEid.Models;

namespace BelgianEid.Bridge.Common;

/// <summary>
/// Extension methods for <see cref="JsonElement"/> that provide safe, intent-revealing
/// property accessors for incoming Native Messaging messages.
/// </summary>
internal static class JsonMessageExtensions
{
    // ── String helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves a required string property from a JSON message.
    /// </summary>
    /// <exception cref="BridgeRequestException">
    /// Thrown when the property is absent or is not a JSON string.
    /// </exception>
    public static string RequireString(this JsonElement message, string property)
    {
        if (!message.TryGetProperty(property, out var prop) || prop.ValueKind != JsonValueKind.String)
            throw new BridgeRequestException($"Missing or invalid property '{property}' in request.");

        return prop.GetString()!;
    }

    /// <summary>
    /// Retrieves an optional boolean property, returning <paramref name="defaultValue"/> when absent.
    /// </summary>
    public static bool GetBooleanOrDefault(this JsonElement message, string property, bool defaultValue = false) =>
        message.TryGetProperty(property, out var prop) ? prop.GetBoolean() : defaultValue;

    /// <summary>
    /// Retrieves an optional string property, returning <c>null</c> when absent.
    /// </summary>
    public static string? GetStringOrNull(this JsonElement message, string property) =>
        message.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    // ── Domain helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a required <c>kind</c> field into an <see cref="EidCertificateKind"/>.
    /// Accepted values (case-insensitive): <c>authentication</c>, <c>signature</c>,
    /// <c>intermediateCA</c> / <c>citizenCA</c>, <c>root</c>.
    /// </summary>
    /// <exception cref="BridgeRequestException">Thrown when the value is absent or unrecognised.</exception>
    public static EidCertificateKind RequireCertificateKind(this JsonElement message, string property)
    {
        var value = message.RequireString(property);

        return value.ToLowerInvariant() switch
        {
            "authentication" => EidCertificateKind.Authentication,
            "signature" => EidCertificateKind.Signature,
            "intermediateca" or "citizenca" or "intermediate" => EidCertificateKind.IntermediateCa,
            "root" => EidCertificateKind.Root,
            _ => throw new BridgeRequestException(
                $"Unknown certificate kind '{value}'. " +
                "Valid values: authentication, signature, intermediateCA, root.")
        };
    }

    /// <summary>
    /// Parses an optional <c>algorithm</c> field into an <see cref="EidHashAlgorithm"/>.
    /// Returns <paramref name="defaultValue"/> when the field is absent or unrecognised.
    /// Accepted values (case-insensitive): <c>sha256</c>, <c>sha384</c>, <c>sha512</c>, <c>sha1</c>.
    /// </summary>
    public static EidHashAlgorithm GetHashAlgorithmOrDefault(
        this JsonElement message,
        string property,
        EidHashAlgorithm defaultValue = EidHashAlgorithm.Sha256)
    {
        if (!message.TryGetProperty(property, out var prop) || prop.ValueKind != JsonValueKind.String)
            return defaultValue;

        return prop.GetString()?.ToLowerInvariant() switch
        {
            "sha256" => EidHashAlgorithm.Sha256,
            "sha384" => EidHashAlgorithm.Sha384,
            "sha512" => EidHashAlgorithm.Sha512,
            "sha1" => EidHashAlgorithm.Sha1,
            _ => defaultValue,
        };
    }
}
