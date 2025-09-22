using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Security.Cryptography;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int version = 1;

    // Scene
    public string sceneName;

    // Player xform
    public float px, py, pz;
    public float rx, ry, rz, rw;

    // Player health
    public float currentHP;
    public float maxHP;

    // Meta
    public int score;
    public int lives;
}

public static class SaveSystem
{
    // ---- FILE ----
    public static string SavePath =>
        Path.Combine(Application.persistentDataPath, "savegame.dat");

    // ---- KEYING (demo only: replace these with your own random values) ----
    // 32 bytes key (AES-256) and 16 bytes IV. Change for your project.
    static readonly byte[] Key = new byte[32]
    {
        0x41,0xC9,0x32,0x10,0x5B,0x6E,0xA7,0x20,0x3F,0x84,0x19,0x50,0x2A,0x66,0x77,0x90,
        0x13,0x44,0xBE,0x2C,0xDF,0x91,0xA4,0xCC,0x08,0x1D,0xAB,0xFE,0x61,0x22,0x37,0x9A
    };
    static readonly byte[] IV = new byte[16]
    {
        0x9E,0x02,0x33,0x71,0xC4,0x5A,0x11,0x8B,0x0D,0x49,0x6C,0xD2,0xE7,0x58,0xA0,0x0F
    };

    // ---- API ----
    public static void Save(SaveData data)
    {
        try
        {
            // 1) XML -> string
            var xml = ToXml(data);

            // 2) Encrypt -> bytes
            var bytes = EncryptStringToBytes_Aes(xml, Key, IV);

            // 3) Write
            File.WriteAllBytes(SavePath, bytes);
#if UNITY_EDITOR
            Debug.Log($"[SaveSystem] Saved to {SavePath}");
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Save failed: {ex}");
        }
    }

    public static bool TryLoad(out SaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(SavePath)) return false;

            // 1) Read
            var bytes = File.ReadAllBytes(SavePath);

            // 2) Decrypt -> xml
            var xml = DecryptStringFromBytes_Aes(bytes, Key, IV);

            // 3) xml -> object
            data = FromXml<SaveData>(xml);

            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Load failed: {ex}");
            return false;
        }
    }

    // ---- XML helpers ----
    static string ToXml<T>(T obj)
    {
        var ser = new XmlSerializer(typeof(T));
        using var sw = new StringWriter();
        ser.Serialize(sw, obj);
        return sw.ToString();
    }

    static T FromXml<T>(string xml)
    {
        var ser = new XmlSerializer(typeof(T));
        using var sr = new StringReader(xml);
        return (T)ser.Deserialize(sr);
    }

    // ---- AES helpers ----
    static byte[] EncryptStringToBytes_Aes(string plainText, byte[] key, byte[] iv)
    {
        using Aes aesAlg = Aes.Create();
        aesAlg.Key = key;
        aesAlg.IV = iv;
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        using var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using var msEncrypt = new MemoryStream();
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt, Encoding.UTF8))
        {
            swEncrypt.Write(plainText);
        }
        return msEncrypt.ToArray();
    }

    static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] key, byte[] iv)
    {
        using Aes aesAlg = Aes.Create();
        aesAlg.Key = key;
        aesAlg.IV = iv;
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
        using var msDecrypt = new MemoryStream(cipherText);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8);
        return srDecrypt.ReadToEnd();
    }
}
