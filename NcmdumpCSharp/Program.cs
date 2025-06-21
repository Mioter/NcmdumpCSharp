using System.CommandLine;
using NcmdumpCSharp.Core;

namespace NcmdumpCSharp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("网易云音乐NCM文件解密工具 - C#版本");

        // 目录选项
        var directoryOption = new Option<string?>(
            aliases: ["-d", "--directory"],
            description: "处理指定目录下的所有NCM文件"
        );
        rootCommand.AddOption(directoryOption);

        // 递归选项
        var recursiveOption = new Option<bool>(
            aliases: ["-r", "--recursive"],
            description: "递归处理子目录"
        );
        rootCommand.AddOption(recursiveOption);

        // 输出目录选项
        var outputOption = new Option<string?>(
            aliases: ["-o", "--output"],
            description: "指定输出目录"
        );
        rootCommand.AddOption(outputOption);

        // 文件参数
        var filesArgument = new Argument<string[]>("files", "要处理的NCM文件")
        {
            Arity = ArgumentArity.ZeroOrMore,
        };
        rootCommand.AddArgument(filesArgument);

        rootCommand.SetHandler(
            ProcessFiles,
            directoryOption,
            recursiveOption,
            outputOption,
            filesArgument
        );

        return await rootCommand.InvokeAsync(args);
    }

    static void ProcessFiles(string? directory, bool recursive, string? output, string[] files)
    {
        // 检查参数
        if (string.IsNullOrEmpty(directory) && (files.Length == 0))
        {
            Console.WriteLine("错误: 请指定要处理的文件或目录");
            Console.WriteLine("使用 --help 查看帮助信息");
            return;
        }

        // 检查递归选项
        if (recursive && string.IsNullOrEmpty(directory))
        {
            Console.WriteLine("错误: -r 选项需要配合 -d 选项使用");
            return;
        }

        // 验证输出目录
        string outputDir = string.Empty;
        bool outputDirSpecified = !string.IsNullOrEmpty(output);

        if (outputDirSpecified)
        {
            outputDir = output!;
            if (File.Exists(outputDir))
            {
                Console.WriteLine($"错误: '{outputDir}' 不是一个有效的目录");
                return;
            }
            Directory.CreateDirectory(outputDir);
        }

        try
        {
            // 处理目录
            if (!string.IsNullOrEmpty(directory))
            {
                if (!Directory.Exists(directory))
                {
                    Console.WriteLine($"错误: 目录 '{directory}' 不存在");
                    return;
                }

                if (recursive)
                {
                    // 递归处理
                    string[] allFiles = Directory.GetFiles(
                        directory,
                        "*.ncm",
                        SearchOption.AllDirectories
                    );
                    foreach (string file in allFiles)
                    {
                        string relativePath = Path.GetRelativePath(directory, file);
                        string? targetDir = outputDirSpecified
                            ? Path.GetDirectoryName(Path.Combine(outputDir, relativePath))
                            : Path.GetDirectoryName(file);

                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        ProcessSingleFile(file, targetDir ?? string.Empty);
                    }
                }
                else
                {
                    // 处理当前目录
                    string[] ncmFiles = Directory.GetFiles(directory, "*.ncm");
                    foreach (string file in ncmFiles)
                    {
                        ProcessSingleFile(file, outputDir);
                    }
                }
            }
            else
            {
                // 处理单个文件
                foreach (string file in files)
                {
                    if (!File.Exists(file))
                    {
                        Console.WriteLine($"错误: 文件 '{file}' 不存在");
                        continue;
                    }

                    ProcessSingleFile(file, outputDir);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理过程中发生错误: {ex.Message}");
        }
    }

    static void ProcessSingleFile(string filePath, string outputDir)
    {
        // 跳过非NCM文件
        if (!filePath.EndsWith(".ncm", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using var crypt = new NeteaseCrypt(filePath);
            crypt.Dump(outputDir);
            crypt.FixMetadata();

            Console.WriteLine($"[完成] '{filePath}' -> '{crypt.DumpFilePath}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 处理文件 '{filePath}' 时发生异常: {ex.Message}");
        }
    }
}
