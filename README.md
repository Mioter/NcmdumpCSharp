# NcmdumpCSharp

网易云音乐NCM文件解密工具 - C#版本

## 简介

这是一个用原生C#重写的网易云音乐NCM文件解密工具，完全实现了原C++版本的所有功能。

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

- .NET 8.0 或更高版本
- Windows 10+ / Linux / macOS

## 安装

### 从源码编译

```bash
# 克隆项目
git clone <repository-url>
cd NcmdumpCSharp

# 编译项目
dotnet build -c Release

# 发布
dotnet publish -c Release -r win-x64 --self-contained true
```

### 下载预编译版本

从 [Releases](https://github.com/Mioter/NcmdumpCSharp/releases) 页面下载对应平台的预编译版本，不过并没有预编译的版本...

## 使用方法

### 命令行工具

注意 : `ncmdump-csharp`请替换为实际可执行文件名！

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

处理指定目录下的所有NCM文件：
```bash
ncmdump-csharp -d /path/to/music/folder
```

递归处理目录及其子目录：
```bash
ncmdump-csharp -d /path/to/music/folder -r
```

指定输出目录：
```bash
ncmdump-csharp file1.ncm file2.ncm -o /path/to/output
```

组合使用：
```bash
ncmdump-csharp -d /path/to/music/folder -r -o /path/to/output
```

### 作为类库使用

```csharp
using NcmdumpCSharp.Core;

// 创建解密器实例
using var crypt = new NeteaseCrypt("path/to/file.ncm");

// 解密并保存文件
crypt.Dump("output/directory");

// 修复元数据
crypt.FixMetadata();

// 获取输出文件路径
string outputPath = crypt.DumpFilePath;
```

## 项目结构

```
NcmdumpCSharp/
├── Core/
│   └── NeteaseCrypt.cs          # 核心解密类
├── Crypto/
│   ├── AesHelper.cs             # AES解密辅助类
│   └── Base64Helper.cs          # Base64解码辅助类
├── Models/
│   └── NeteaseMusicMetadata.cs  # 音乐元数据模型
├── Program.cs                   # 主程序入口
├── NcmdumpCSharp.csproj         # 项目文件
└── README.md                    # 说明文档
```

## 技术实现

### 核心算法

1. **文件格式验证**：检查NCM文件头（0x4e455443 和 0x4d414446）
2. **密钥提取**：从文件头部提取加密的密钥数据
3. **AES解密**：使用固定密钥对密钥数据进行AES-ECB解密
4. **RC4密钥盒**：构建RC4密钥盒用于音频数据解密
5. **元数据解析**：解析JSON格式的音乐元数据
6. **音频解密**：使用RC4算法解密音频数据
7. **格式识别**：根据文件头自动识别输出格式（MP3/FLAC）
8. **元数据修复**：使用atldotnet库修复音频文件元数据

### 依赖库

- **z440.atl.core**：音频文件元数据处理
- **System.Text.Json**：JSON数据解析
- **System.CommandLine**：命令行参数解析
- **System.Security.Cryptography**：AES加密算法

## 与原版对比

| 特性    | C++原版 | C#版本 |
|-------|-------|------|
| 跨平台支持 | ✅     | ✅    |
| 性能    | 高     | 中等   |
| 开发难度  | 高     | 低    |
| 维护性   | 中等    | 高    |
| 依赖管理  | 复杂    | 简单   |
| 部署便利性 | 复杂    | 简单   |

## 许可证

本项目采用与原版相同的许可证。详见 [LICENSE](LICENSE) 文件。

## 贡献

欢迎提交Issue和Pull Request！

## 致谢

- 感谢原C++版本的开发者 [taurusxin/ncmdump](https://github.com/taurusxin/ncmdump)
- 感谢 [atldotnet](https://github.com/Zeugma440/atldotnet) 项目提供的音频元数据处理功能 
