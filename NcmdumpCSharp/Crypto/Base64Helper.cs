namespace NcmdumpCSharp.Crypto;

/// <summary>
/// Base64解码辅助类
/// </summary>
public static class Base64Helper
{
    /// <summary>
    /// Base64解码
    /// </summary>
    /// <param name="base64String">Base64字符串</param>
    /// <returns>解码后的字节数组</returns>
    public static byte[] Decode(string base64String)
    {
        return Convert.FromBase64String(base64String);
    }

    /// <summary>
    /// Base64解码为字符串
    /// </summary>
    /// <param name="base64String">Base64字符串</param>
    /// <returns>解码后的字符串</returns>
    public static string DecodeToString(string base64String)
    {
        byte[] bytes = Decode(base64String);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
