using System.Security.Cryptography;
using BelgianEid.Models;
using BelgianEid.Utilities;

namespace BelgianEid.Utilities;

/// <summary>
/// Hashing utilities and ASN.1 DigestInfo prefix construction,
/// used by the signature and authentication services.
/// </summary>
internal static class SigningUtils
{
    /// <summary>Computes the hash of the data using the requested algorithm.</summary>
    public static byte[] ComputeHash(byte[] data, EidHashAlgorithm algorithm) => algorithm switch
    {
        EidHashAlgorithm.Sha256 => SHA256.HashData(data),
        EidHashAlgorithm.Sha384 => SHA384.HashData(data),
        EidHashAlgorithm.Sha512 => SHA512.HashData(data),
        EidHashAlgorithm.Sha1   => SHA1.HashData(data),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
    };

    /// <summary>Concatenates the ASN.1 DigestInfo prefix and the hash.</summary>
    public static byte[] BuildDigestInfo(byte[] hash, EidHashAlgorithm algorithm)
    {
        byte[] prefix = DigestInfoPrefixes.For(algorithm);
        var digestInfo = new byte[prefix.Length + hash.Length];
        Buffer.BlockCopy(prefix, 0, digestInfo, 0, prefix.Length);
        Buffer.BlockCopy(hash, 0, digestInfo, prefix.Length, hash.Length);
        return digestInfo;
    }

    /// <summary>Verifies that the hash size matches the declared algorithm.</summary>
    /// <exception cref="ArgumentException">Incorrect size.</exception>
    public static void EnsureHashLength(byte[] hash, EidHashAlgorithm algorithm)
    {
        int expected = algorithm switch
        {
            EidHashAlgorithm.Sha256 => 32,
            EidHashAlgorithm.Sha384 => 48,
            EidHashAlgorithm.Sha512 => 64,
            EidHashAlgorithm.Sha1   => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        if (hash.Length != expected)
            throw new ArgumentException(
                $"The provided hash is {hash.Length} byte(s) but " +
                $"{algorithm} expects {expected}.", nameof(hash));
    }
}
