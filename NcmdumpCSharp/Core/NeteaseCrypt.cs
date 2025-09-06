using System.Text;
using ATL;
using NcmdumpCSharp.Crypto;
using NcmdumpCSharp.Models;

namespace NcmdumpCSharp.Core;

/// <summary>
///     网易云音乐NCM文件解密器
/// </summary>
public class NeteaseCrypt : IDisposable
{
    // 固定的密钥
    private static readonly byte[] _coreKey = "hzHRAmso5kInbaxW"u8.ToArray();
    private static readonly byte[] _modifyKey = "#14ljk_!\\]&0U<'("u8.ToArray();
    private static readonly byte[] _pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string _filePath;
    private readonly byte[] _keyBox = new byte[256];
    private FileStream? _fileStream;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="filePath">NCM文件路径</param>
    public NeteaseCrypt(string filePath)
    {
        _filePath = filePath;
        Initialize();
    }

    /// <summary>
    ///     解析自 NCM 的元数据对象（标题、艺术家、专辑、格式等）。
    ///     初始化时从 NCM 头读取；在解密首个音频块时如未确定 Format 将补全。
    /// </summary>
    public NeteaseMusicMetadata? Metadata { get; private set; }

    /// <summary>
    ///     专辑封面二进制数据（JPEG 或 PNG），可在 <see cref="FixMetadata" /> 时写入音频标签。
    /// </summary>
    public byte[]? ImageData { get; private set; }

    /// <summary>
    ///     获取输出文件路径
    /// </summary>
    public string DumpFilePath { get; private set; } = string.Empty;

    /// <summary>
    ///     释放资源
    /// </summary>
    public void Dispose()
    {
        _fileStream?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     初始化解密器
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

            // 跳过"163 key(Don'\''t modify):"
            string swapModifyData = Encoding.UTF8.GetString(modifyData, 22, modifyData.Length - 22);

            // Base64解码
            byte[] modifyOutData = Base64Helper.Decode(swapModifyData);

            // AES解密
            byte[] modifyDecryptData = AesHelper.AesEcbDecrypt(_modifyKey, modifyOutData);

            // 跳过"music:"
            string jsonData = Encoding.UTF8.GetString(modifyDecryptData, 6, modifyDecryptData.Length - 6);

            Metadata = NeteaseMusicMetadata.FromJson(jsonData);
        }

        // 跳过CRC32和图片版本
        _fileStream.Seek(5, SeekOrigin.Current);

        // 读取封面长度
        int coverFrameLength = ReadInt32();
        int imageLength = ReadInt32();

        if (imageLength > 0)
        {
            ImageData = new byte[imageLength];
            ReadBytes(ImageData, 0, imageLength);
        }

        // 跳过剩余的封面数据
        _fileStream.Seek(coverFrameLength - imageLength, SeekOrigin.Current);
    }

    /// <summary>
    ///     检查是否为NCM文件
    /// </summary>
    private bool IsNcmFile()
    {
        int header1 = ReadInt32();
        int header2 = ReadInt32();

        return header1 == 0x4e455443 && header2 == 0x4d414446;
    }

    /// <summary>
    ///     构建密钥盒
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
    ///     读取4字节整数
    /// </summary>
    private int ReadInt32()
    {
        byte[] buffer = new byte[4];
        ReadBytes(buffer, 0, 4);

        return BitConverter.ToInt32(buffer, 0);
    }

    /// <summary>
    ///     读取字节数组
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
    ///     获取MIME类型
    /// </summary>
    public static string GetMimeType(byte[] data)
    {
        if (data.Length >= 8 && data.Take(8).SequenceEqual(_pngHeader))
        {
            return "image/png";
        }

        return "image/jpeg";
    }

    /// <summary>
    ///     使用密钥盒对缓冲区执行 RC4 异或解密，并推进解密位置。
    /// </summary>
    /// <param name="buffer">就地解密的数据缓冲区</param>
    /// <param name="position">文件内偏移位置（将被按已处理字节数递增）</param>
    private void Rc4Xor(Span<byte> buffer, ref long position)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            int j = (int)(position + i + 1 & 0xff);
            buffer[i] ^= _keyBox[_keyBox[j] + _keyBox[_keyBox[j] + j & 0xff] & 0xff];
        }

        position += buffer.Length;
    }

    /// <summary>
    ///     从首块数据前缀判断音频格式（mp3/flac），无法识别时返回 null。
    /// </summary>
    /// <param name="buffer">首块数据的只读切片</param>
    /// <returns>文件扩展名（不含点），或 null</returns>
    private static string? DetectFormat(ReadOnlySpan<byte> buffer)
    {
        return buffer.Length switch
        {
            // ID3 -> MP3
            >= 3 when buffer[0] == 0x49 && buffer[1] == 0x44 && buffer[2] == 0x33 => "mp3",

            // fLaC -> FLAC
            >= 4 when buffer[0] == 0x66 && buffer[1] == 0x4C && buffer[2] == 0x61 && buffer[3] == 0x43 => "flac",
            _ => null,
        };
    }

    /// <summary>
    ///     准备基础输出路径（不含扩展名），用于 Dump/DumpAsync。
    /// </summary>
    /// <param name="outputDir">可选的输出目录；为空时与源文件同目录</param>
    private void PrepareDumpBasePath(string? outputDir)
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            DumpFilePath = Path.ChangeExtension(_filePath, null);
        }
        else
        {
            string fileName = Path.GetFileNameWithoutExtension(_filePath);
            DumpFilePath = Path.Combine(outputDir, fileName);
        }
    }

    /// <summary>
    ///     对数据块做 RC4 解密，并在首个数据块时检测音频格式（设置 <see cref="NeteaseMusicMetadata.Format" />）。
    /// </summary>
    /// <param name="span">待就地解密的数据块</param>
    /// <param name="position">文件内偏移位置（将被按已处理字节数递增）</param>
    /// <param name="firstChunk">是否为首个数据块（调用内维护并在首次后置为 false）</param>
    private void DecryptAndMaybeDetectFormat(Span<byte> span, ref long position, ref bool firstChunk)
    {
        Rc4Xor(span, ref position);

        if (!firstChunk)
            return;

        if (string.IsNullOrEmpty(Metadata?.Format))
        {
            Metadata ??= new NeteaseMusicMetadata();
            Metadata.Format = DetectFormat(span) ?? Metadata.Format;
        }

        firstChunk = false;
    }

    /// <summary>
    ///     基于首块数据确定最终输出路径（含扩展名）并创建输出流。
    /// </summary>
    /// <param name="firstChunk">首块数据，用于格式检测</param>
    /// <param name="useAsync">是否以异步写入模式打开文件流</param>
    /// <returns>已创建的文件写入流</returns>
    private FileStream CreateOutputStreamForFirstChunk(ReadOnlySpan<byte> firstChunk, bool useAsync)
    {
        string? fmt = DetectFormat(firstChunk);
        DumpFilePath = Path.ChangeExtension(DumpFilePath, fmt ?? "flac");

        string? outputDir2 = Path.GetDirectoryName(DumpFilePath);

        if (!string.IsNullOrEmpty(outputDir2) && !Directory.Exists(outputDir2))
        {
            Directory.CreateDirectory(outputDir2);
        }

        return new FileStream(DumpFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 0x8000, useAsync);
    }

    /// <summary>
    ///     解密并将音频写入到文件（同步）。
    /// </summary>
    /// <param name="outputDir">可选的输出目录；为空时默认写入到源文件同目录</param>
    public void Dump(string? outputDir = null)
    {
        PrepareDumpBasePath(outputDir);

        byte[] buffer = new byte[0x8000];
        FileStream? outputStream = null;
        long position = 0;
        bool firstChunk = true;

        try
        {
            int bytesRead;

            while (_fileStream != null && (bytesRead = _fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                var span = buffer.AsSpan(0, bytesRead);
                DecryptAndMaybeDetectFormat(span, ref position, ref firstChunk);

                outputStream ??= CreateOutputStreamForFirstChunk(span, false);

                outputStream.Write(span);
            }
        }
        finally
        {
            outputStream?.Close();
        }
    }

    /// <summary>
    ///     修复输出音频文件的元数据（标题/艺术家/专辑/封面）。
    /// </summary>
    /// <exception cref="InvalidOperationException">当输出文件不存在或路径为空时抛出</exception>
    public void FixMetadata()
    {
        if (string.IsNullOrEmpty(DumpFilePath) || !File.Exists(DumpFilePath))
        {
            throw new InvalidOperationException("输出文件不存在");
        }

        try
        {
            var tag = new Track(DumpFilePath);

            if (Metadata == null)
                return;

            tag.Title = Metadata.Name;
            tag.Artist = Metadata.Artist;
            tag.Album = Metadata.Album;

            // 添加封面图片
            if (ImageData is { Length: > 0 })
            {
                var picture = PictureInfo.fromBinaryData(ImageData, PictureInfo.PIC_TYPE.Front);
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
    ///     解密音频数据到内存流（同步）。
    /// </summary>
    /// <returns>包含解密后音频数据的内存流（Position 已重置为0）；当文件流未初始化时返回 null</returns>
    public MemoryStream? DumpToStream()
    {
        if (_fileStream == null)
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        byte[] buffer = new byte[0x8000];
        int bytesRead;
        bool firstChunk = true;
        long position = 0;

        while ((bytesRead = _fileStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            var span = buffer.AsSpan(0, bytesRead);
            DecryptAndMaybeDetectFormat(span, ref position, ref firstChunk);
            memoryStream.Write(span);
        }

        memoryStream.Position = 0;

        return memoryStream;
    }

    /// <summary>
    ///     解密音频数据到内存流（异步）。
    /// </summary>
    /// <returns>包含解密后音频数据的内存流（Position 已重置为0）；当文件流未初始化时返回 null</returns>
    public async Task<MemoryStream?> DumpToStreamAsync()
    {
        if (_fileStream == null)
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        byte[] buffer = new byte[0x8000];
        int bytesRead;
        bool firstChunk = true;
        long position = 0;

        while ((bytesRead = await _fileStream.ReadAsync(buffer)) > 0)
        {
            var span = buffer.AsSpan(0, bytesRead);
            DecryptAndMaybeDetectFormat(span, ref position, ref firstChunk);
            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
        }

        memoryStream.Position = 0;

        return memoryStream;
    }

    /// <summary>
    ///     解密并将音频写入到文件（异步）。
    /// </summary>
    /// <param name="outputDir">可选的输出目录；为空时默认写入到源文件同目录</param>
    public async Task DumpAsync(string? outputDir = null)
    {
        PrepareDumpBasePath(outputDir);

        byte[] buffer = new byte[0x8000];
        FileStream? outputStream = null;
        long position = 0;
        bool firstChunk = true;

        try
        {
            int bytesRead;

            while (_fileStream != null && (bytesRead = await _fileStream.ReadAsync(buffer)) > 0)
            {
                var span = buffer.AsSpan(0, bytesRead);
                DecryptAndMaybeDetectFormat(span, ref position, ref firstChunk);

                outputStream ??= CreateOutputStreamForFirstChunk(span, true);

                await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            }
        }
        finally
        {
            if (outputStream is not null)
            {
                await outputStream.FlushAsync();
                outputStream.Close();
            }
        }
    }
}
