using System.Security.Cryptography;
using System.Text;

namespace NcmdumpCSharp.Crypto;

/// <summary>
///     AES解密辅助类
/// </summary>
public static class AesHelper
{
    /// <summary>
    ///     AES ECB模式解密
    /// </summary>
    /// <param name="key">密钥</param>
    /// <param name="encryptedData">加密数据</param>
    /// <returns>解密后的数据</returns>
    public static byte[] AesEcbDecrypt(byte[] key, byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;

        using var decryptor = aes.CreateDecryptor();

        return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
    }

    /// <summary>
    ///     AES ECB模式解密字符串
    /// </summary>
    /// <param name="key">密钥</param>
    /// <param name="encryptedString">加密字符串</param>
    /// <returns>解密后的字符串</returns>
    public static string AesEcbDecrypt(byte[] key, string encryptedString)
    {
        byte[] encryptedData = Encoding.UTF8.GetBytes(encryptedString);
        byte[] decryptedData = AesEcbDecrypt(key, encryptedData);

        return Encoding.UTF8.GetString(decryptedData);
    }
}
