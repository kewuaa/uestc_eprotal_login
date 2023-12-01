using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

public static class CryptoLib {
    static private byte[] IV = Encoding.UTF8.GetBytes("fBB8WZr28H3t7NGt");
    static private string PAD = "KbN6ApjTFGswB8x3AdKPJ6NKMHJsbfZQ76cXGYdkhcGjxcjnQhXAAE2rrMndEheW";

    static public string Encrypt(string s, string key) {
        using var aes = Aes.Create();
        aes.IV = IV;
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var data = Encoding.UTF8.GetBytes(PAD + s);
        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.FlushFinalBlock();
        return Convert.ToBase64String(ms.ToArray());
    }
}
