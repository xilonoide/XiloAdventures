using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace XiloAdventures.Engine;

public static class CryptoUtil
{
    // Clave/IV fijos solo para ejemplo. En una app real deberían protegerse mejor.
    private static readonly byte[] Key =
        Encoding.UTF8.GetBytes("XiloAdv-Key-1234XiloAdv-Key-1234"); // 32 bytes
    private static readonly byte[] Iv =
        Encoding.UTF8.GetBytes("XiloAdv-IV-12345"); // 16 bytes

    public static void EncryptToFile(string path, string plainText, string extension)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = Iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var filename = Path.GetFileNameWithoutExtension(path);
        var newPath = Path.Combine(Path.GetDirectoryName(path)!, filename + "." + extension);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var crypto = new CryptoStream(fs, aes.CreateEncryptor(), CryptoStreamMode.Write);
        using var sw = new StreamWriter(crypto, Encoding.UTF8);
        sw.Write(plainText);
    }

    public static string DecryptFromFile(string path)
    {
        var bytes = File.ReadAllBytes(path);

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
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
            // Compatibilidad con ficheros antiguos en texto plano
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
