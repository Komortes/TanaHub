using System.Security.Cryptography;
using System.Text;

namespace TanaHub.Infrastructure.Settings;

/// <summary>
/// Encrypts secrets at rest using a key file stored alongside settings, so tokens are not
/// readable as plain text if the settings file leaks (backup, sync, casual disk access).
/// This is not a substitute for OS keychain integration: anyone with access to both files
/// under the same OS user account can still decrypt the value.
/// </summary>
internal static class LocalSecretProtector
{
    private const string Prefix = "enc:v1:";
    private const int KeySizeBytes = 32;

    public static string Protect(string plaintext, string keyPath)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        var key = GetOrCreateKey(keyPath);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];

        using (var aes = new AesGcm(key, tag.Length))
        {
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        var payload = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length + tag.Length, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public static string Unprotect(string value, string keyPath)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Empty, or legacy plaintext written before encryption was introduced.
            return value;
        }

        try
        {
            var key = GetOrCreateKey(keyPath);
            var payload = Convert.FromBase64String(value[Prefix.Length..]);

            var nonceSize = AesGcm.NonceByteSizes.MaxSize;
            var tagSize = AesGcm.TagByteSizes.MaxSize;
            if (payload.Length < nonceSize + tagSize)
            {
                return string.Empty;
            }

            var nonce = payload.AsSpan(0, nonceSize);
            var tag = payload.AsSpan(nonceSize, tagSize);
            var cipherBytes = payload.AsSpan(nonceSize + tagSize);
            var plainBytes = new byte[cipherBytes.Length];

            using (var aes = new AesGcm(key, tagSize))
            {
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return string.Empty;
        }
    }

    private static byte[] GetOrCreateKey(string keyPath)
    {
        var directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(keyPath))
        {
            var existing = File.ReadAllBytes(keyPath);
            if (existing.Length == KeySizeBytes)
            {
                return existing;
            }
        }

        var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
        File.WriteAllBytes(keyPath, key);
        TryRestrictPermissions(keyPath);
        return key;
    }

    private static void TryRestrictPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
        }
    }
}
