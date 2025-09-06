# NcmdumpCSharp

网易云音乐NCM文件解密工具 - C#版本

## 简介

这是一个用原生C#重写的网易云音乐NCM文件解密工具，完全实现了原C++版本的所有功能，并在易用性与可维护性上做了改进（见下文“近期改进”）。

## 功能特性

- ✅ 支持NCM文件格式识别和验证
- ✅ AES-ECB解密算法实现
- ✅ RC4密钥盒解密算法实现
- ✅ 自动识别输出格式（MP3/FLAC）
- ✅ 元数据解析和修复（标题、艺术家、专辑等）
- ✅ 专辑封面提取和嵌入
- ✅ 批量处理支持
- ✅ 递归目录处理
- ✅ 跨平台支持（Windows、Linux、macOS）

## 系统要求

- .NET 8.0 或更高版本（建议 .NET 9）
- Windows 10+ / Linux / macOS

## 安装

### 从源码编译

```bash
# 克隆项目
git clone <repository-url>
cd NcmdumpCSharp

# 编译项目
dotnet build -c Release

# 发布（可选）
dotnet publish -c Release -r win-x64 --self-contained true
```

### 下载预编译版本

从 [Releases](https://github.com/Mioter/NcmdumpCSharp/releases) 页面下载对应平台的预编译版本（如有）。

## 使用方法

### 命令行工具

注意: `ncmdump-csharp` 请替换为实际可执行文件名。

显示帮助信息：

```bash
ncmdump-csharp --help
```

显示版本信息：

```bash
ncmdump-csharp --version
```

处理单个或多个文件：

```bash
ncmdump-csharp file1.ncm file2.ncm file3.ncm
```

处理指定目录下的所有 NCM 文件：

```bash
ncmdump-csharp -d /path/to/music/folder
```

递归处理目录及其子目录：

```bash
ncmdump-csharp -d /path/to/music/folder -r
```

指定输出目录（可选）：

```bash
ncmdump-csharp file1.ncm file2.ncm -o /path/to/output
```

组合使用：

```bash
ncmdump-csharp -d /path/to/music/folder -r -o /path/to/output
```

### 作为类库使用

同步解密到文件：

```csharp
using NcmdumpCSharp.Core;

using var crypt = new NeteaseCrypt("path/to/file.ncm");
crypt.Dump("output/directory");
crypt.FixMetadata();
Console.WriteLine(crypt.DumpFilePath);
```

异步解密到文件：

```csharp
using NcmdumpCSharp.Core;

using var crypt = new NeteaseCrypt("path/to/file.ncm");
await crypt.DumpAsync("output/directory");
crypt.FixMetadata();
Console.WriteLine(crypt.DumpFilePath);
```

解密到内存流：

```csharp
using NcmdumpCSharp.Core;

using var crypt = new NeteaseCrypt("path/to/file.ncm");
using var ms = crypt.DumpToStream(); // 同步
// or
using var ms2 = await crypt.DumpToStreamAsync(); // 异步
```

## 项目结构

```
NcmdumpCSharp/
├── Core/
│   └── NeteaseCrypt.cs          # 核心解密类
├── Crypto/
│   ├── AesHelper.cs             # AES 解密辅助类
│   └── Base64Helper.cs          # Base64 解码辅助类
├── Models/
│   └── NeteaseMusicMetadata.cs  # 音乐元数据模型
├── Program.cs                   # 主程序入口（CLI）
├── NcmdumpCSharp.csproj         # 项目文件
└── README.md                    # 说明文档
```

## 技术实现

1. 文件格式验证：检查 NCM 魔数
2. 密钥提取与 AES-ECB 解密
3. 构建 RC4 密钥盒并异或解密音频数据
4. 首块数据识别输出格式（mp3/flac），默认 flac
5. 解析元数据并在 FixMetadata 中写入标签与封面

依赖库：

- z440.atl.core：音频文件元数据处理
- System.Text.Json：JSON 数据解析
- System.CommandLine：命令行参数解析
- System.Security.Cryptography：AES 加密算法

## 近期改进

- CLI 易用性
    - 新增 `--version`/`-v` 显示版本
    - `-o/--output` 变为可选，未指定时输出到源文件同目录
- 核心逻辑
    - 提取公共方法：PrepareDumpBasePath、DecryptAndMaybeDetectFormat、CreateOutputStreamForFirstChunk，减少重复
    - 修复异步写入参数类型，统一使用 `buffer.AsMemory(0, bytesRead)`
- 代码质量
    - 完善 XML 文档注释，补充参数、异常与返回值说明

## 许可证

本项目采用与原版相同的许可证。详见 [LICENSE](LICENSE) 文件。

## 贡献

欢迎提交 Issue 和 Pull Request！

## 致谢

- 感谢原C++版本的开发者 [taurusxin/ncmdump](https://github.com/taurusxin/ncmdump)
- 感谢 [atldotnet](https://github.com/Zeugma440/atldotnet) 项目提供的音频元数据处理功能 
