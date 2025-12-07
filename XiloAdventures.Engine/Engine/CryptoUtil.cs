using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace XiloAdventures.Engine;

public static class CryptoUtil
{
    // Clave/IV por defecto. En una app real deberían protegerse mejor.
    public const string DefaultKeyString = "XiloAdv-Key-1234XiloAdv-Key-1234"; // 32 chars
    private static readonly byte[] DefaultKey = Encoding.UTF8.GetBytes(DefaultKeyString); // 32 bytes
    private static readonly byte[] Iv =
        Encoding.UTF8.GetBytes("XiloAdv-IV-12345"); // 16 bytes

    public static void EncryptToFile(string path, string plainText, string extension, string? customKey = null, bool encryptIfEmpty = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var filename = Path.GetFileNameWithoutExtension(path);
        var newPath = Path.Combine(Path.GetDirectoryName(path)!, filename + "." + extension);

        if (!encryptIfEmpty && string.IsNullOrWhiteSpace(customKey))
        {
            File.WriteAllText(newPath, plainText, Encoding.UTF8);
            return;
        }

        using var aes = Aes.Create();
        aes.Key = GetKey(customKey);
        aes.IV = Iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var crypto = new CryptoStream(fs, aes.CreateEncryptor(), CryptoStreamMode.Write);
        using var sw = new StreamWriter(crypto, Encoding.UTF8);
        sw.Write(plainText);
    }

    public static string DecryptFromFile(string path, string? customKey = null, bool throwOnError = false)
    {
        var bytes = File.ReadAllBytes(path);

        try
        {
            using var aes = Aes.Create();
            aes.Key = GetKey(customKey);
            aes.IV = Iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream(bytes);
            using var crypto = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(crypto, Encoding.UTF8);
            return sr.ReadToEnd();
        }
        catch
        {
            if (throwOnError)
                throw;

            // Compatibilidad con ficheros antiguos en texto plano
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static byte[] GetKey(string? customKey)
    {
        if (string.IsNullOrWhiteSpace(customKey))
            return DefaultKey;

        var trimmed = customKey.Trim();
        if (trimmed.Length == 8)
        {
            trimmed = trimmed + new string('X', 24);
        }

        var keyBytes = Encoding.UTF8.GetBytes(trimmed);
        if (keyBytes.Length != 32)
            throw new ArgumentException("La clave de cifrado debe ser de 8 caracteres.");

        return keyBytes;
    }
}
