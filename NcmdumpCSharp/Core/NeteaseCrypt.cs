using System.Text;
using ATL;
using NcmdumpCSharp.Crypto;
using NcmdumpCSharp.Models;

namespace NcmdumpCSharp.Core;

/// <summary>
/// 网易云音乐NCM文件解密器
/// </summary>
public class NeteaseCrypt : IDisposable
{
    // 固定的密钥
    private static readonly byte[] _coreKey =
    "hzHRAmso5kInbaxW"u8.ToArray();
    private static readonly byte[] _modifyKey =
    "#14ljk_!\\]&0U<\'("u8.ToArray();
    private static readonly byte[] _pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string _filePath;
    private FileStream? _fileStream;
    private readonly byte[] _keyBox = new byte[256];
    private NeteaseMusicMetadata? _metadata;
    private byte[]? _imageData;
    private string _dumpFilePath = string.Empty;

    public NeteaseMusicMetadata? Metadata => _metadata;
    public byte[]? ImageData => _imageData;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePath">NCM文件路径</param>
    public NeteaseCrypt(string filePath)
    {
        _filePath = filePath;
        Initialize();
    }

    /// <summary>
    /// 获取输出文件路径
    /// </summary>
    public string DumpFilePath => _dumpFilePath;

    /// <summary>
    /// 初始化解密器
    /// </summary>
    private void Initialize()
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"文件不存在: {_filePath}");
        }

        _fileStream = File.OpenRead(_filePath);

        if (!IsNcmFile())
        {
            throw new InvalidOperationException("不是有效的网易云音乐NCM文件");
        }

        // 跳过文件头
        _fileStream.Seek(2, SeekOrigin.Current);

        // 读取密钥数据长度
        int keyDataLength = ReadInt32();
        if (keyDataLength <= 0)
        {
            throw new InvalidOperationException("损坏的NCM文件");
        }

        // 读取密钥数据
        byte[] keyData = new byte[keyDataLength];
        ReadBytes(keyData, 0, keyDataLength);

        // 异或解密
        for (int i = 0; i < keyDataLength; i++)
        {
            keyData[i] ^= 0x64;
        }

        // AES解密
        byte[] decryptedKeyData = AesHelper.AesEcbDecrypt(_coreKey, keyData);

        // 构建密钥盒
        BuildKeyBox(decryptedKeyData, 17, decryptedKeyData.Length - 17);

        // 读取元数据长度
        int metadataLength = ReadInt32();

        if (metadataLength > 0)
        {
            // 读取元数据
            byte[] modifyData = new byte[metadataLength];
            ReadBytes(modifyData, 0, metadataLength);

            // 异或解密
            for (int i = 0; i < metadataLength; i++)
            {
                modifyData[i] ^= 0x63;
            }

            // 跳过"163 key(Don't modify):"
            string swapModifyData = Encoding.UTF8.GetString(modifyData, 22, modifyData.Length - 22);

            // Base64解码
            byte[] modifyOutData = Base64Helper.Decode(swapModifyData);

            // AES解密
            byte[] modifyDecryptData = AesHelper.AesEcbDecrypt(_modifyKey, modifyOutData);

            // 跳过"music:"
            string jsonData = Encoding.UTF8.GetString(
                modifyDecryptData,
                6,
                modifyDecryptData.Length - 6
            );

            _metadata = NeteaseMusicMetadata.FromJson(jsonData);
        }

        // 跳过CRC32和图片版本
        _fileStream.Seek(5, SeekOrigin.Current);

        // 读取封面长度
        int coverFrameLength = ReadInt32();
        int imageLength = ReadInt32();

        if (imageLength > 0)
        {
            _imageData = new byte[imageLength];
            ReadBytes(_imageData, 0, imageLength);
        }

        // 跳过剩余的封面数据
        _fileStream.Seek(coverFrameLength - imageLength, SeekOrigin.Current);
    }

    /// <summary>
    /// 检查是否为NCM文件
    /// </summary>
    private bool IsNcmFile()
    {
        int header1 = ReadInt32();
        int header2 = ReadInt32();

        return header1 == 0x4e455443 && header2 == 0x4d414446;
    }

    /// <summary>
    /// 构建密钥盒
    /// </summary>
    private void BuildKeyBox(byte[] key, int keyOffset, int keyLength)
    {
        // 初始化密钥盒
        for (int i = 0; i < 256; i++)
        {
            _keyBox[i] = (byte)i;
        }

        byte lastByte = 0;
        byte keyOffset2 = 0;

        for (int i = 0; i < 256; i++)
        {
            byte swap = _keyBox[i];
            byte c = (byte)(swap + lastByte + key[keyOffset + keyOffset2] & 0xff);
            keyOffset2++;
            if (keyOffset2 >= keyLength)
                keyOffset2 = 0;

            _keyBox[i] = _keyBox[c];
            _keyBox[c] = swap;
            lastByte = c;
        }
    }

    /// <summary>
    /// 读取4字节整数
    /// </summary>
    private int ReadInt32()
    {
        byte[] buffer = new byte[4];
        ReadBytes(buffer, 0, 4);
        return BitConverter.ToInt32(buffer, 0);
    }

    /// <summary>
    /// 读取字节数组
    /// </summary>
    private void ReadBytes(byte[] buffer, int offset, int count)
    {
        if (_fileStream == null)
            throw new InvalidOperationException("文件流未初始化");

        int bytesRead = _fileStream.Read(buffer, offset, count);
        if (bytesRead != count)
        {
            throw new InvalidOperationException("读取文件失败");
        }
    }

    /// <summary>
    /// 获取MIME类型
    /// </summary>
    private static string GetMimeType(byte[] data)
    {
        if (data.Length >= 8 && data.Take(8).SequenceEqual(_pngHeader))
        {
            return "image/png";
        }
        return "image/jpeg";
    }

    /// <summary>
    /// 解密并保存文件
    /// </summary>
    /// <param name="outputDir">输出目录</param>
    public void Dump(string outputDir = "")
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            _dumpFilePath = Path.ChangeExtension(_filePath, null);
        }
        else
        {
            string fileName = Path.GetFileNameWithoutExtension(_filePath);
            _dumpFilePath = Path.Combine(outputDir, fileName);
        }

        byte[] buffer = new byte[0x8000];
        FileStream? outputStream = null;
        long position = 0;

        try
        {
            while (_fileStream!.Position < _fileStream.Length)
            {
                int bytesRead = _fileStream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                // RC4解密
                for (int i = 0; i < bytesRead; i++)
                {
                    int j = (int)(position + i + 1 & 0xff);
                    buffer[i] ^= _keyBox[_keyBox[j] + _keyBox[_keyBox[j] + j & 0xff] & 0xff];
                }

                // 确定文件格式
                if (outputStream == null)
                {
                    if (buffer[0] == 0x49 && buffer[1] == 0x44 && buffer[2] == 0x33)
                    {
                        _dumpFilePath = Path.ChangeExtension(_dumpFilePath, "mp3");
                    }
                    else
                    {
                        _dumpFilePath = Path.ChangeExtension(_dumpFilePath, "flac");
                    }

                    // 确保输出目录存在
                    string? outputDir2 = Path.GetDirectoryName(_dumpFilePath);
                    if (!string.IsNullOrEmpty(outputDir2) && !Directory.Exists(outputDir2))
                    {
                        Directory.CreateDirectory(outputDir2);
                    }

                    outputStream = File.Create(_dumpFilePath);
                }

                outputStream.Write(buffer, 0, bytesRead);
                position += bytesRead;
            }
        }
        finally
        {
            outputStream?.Close();
        }
    }

    /// <summary>
    /// 修复元数据
    /// </summary>
    public void FixMetadata()
    {
        if (string.IsNullOrEmpty(_dumpFilePath) || !File.Exists(_dumpFilePath))
        {
            throw new InvalidOperationException("输出文件不存在");
        }

        try
        {
            var tag = new Track(_dumpFilePath);

            if (_metadata == null)
                return;
            tag.Title = _metadata.Name;
            tag.Artist = _metadata.Artist;
            tag.Album = _metadata.Album;

            // 添加封面图片
            if (_imageData is { Length: > 0 })
            {
                var picture = PictureInfo.fromBinaryData(_imageData, PictureInfo.PIC_TYPE.Front);
                tag.EmbeddedPictures.Clear(); // 可选：清空已有封面
                tag.EmbeddedPictures.Add(picture);
            }

            tag.Save();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[警告] 修复元数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解密音频数据到内存流
    /// </summary>
    /// <returns>包含音频流、元数据、封面图片和格式的元组</returns>
    public async Task<(MemoryStream? AudioStream, string? Format)> DumpToStreamAsync()
    {
        if (_fileStream == null)
        {
            return (null, null);
        }

        var memoryStream = new MemoryStream();
        byte[] buffer = new byte[0x8000];
        int bytesRead;
        string? format = _metadata?.Format;
        bool firstChunk = true;
        long position = 0;

        while ((bytesRead = await _fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            // RC4解密
            for (int i = 0; i < bytesRead; i++)
            {
                int j = (int)(position + i + 1 & 0xff);
                buffer[i] ^= _keyBox[_keyBox[j] + _keyBox[_keyBox[j] + j & 0xff] & 0xff];
            }
            
            if (firstChunk)
            {
                if (string.IsNullOrEmpty(format))
                {
                    if (bytesRead >= 4 && buffer[0] == 0x66 && buffer[1] == 0x4C && buffer[2] == 0x61 && buffer[3] == 0x43) // fLaC
                    {
                        format = "flac";
                    }
                    else if (bytesRead >= 3 && buffer[0] == 0x49 && buffer[1] == 0x44 && buffer[2] == 0x33) // ID3
                    {
                        format = "mp3";
                    }
                }
                firstChunk = false;
            }

            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            position += bytesRead;
        }

        memoryStream.Position = 0;
        return (memoryStream, format);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _fileStream?.Dispose();
    }
}
