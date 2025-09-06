using System.CommandLine;
using System.Reflection;
using NcmdumpCSharp.Core;

namespace NcmdumpCSharp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("网易云音乐NCM文件解密工具 - C#版本");

        // 版本选项
        var versionOption = new Option<bool>("--version", "-v")
        {
            Description = "显示版本信息并退出",
        };

        rootCommand.Options.Add(versionOption);

        // 目录选项
        var directoryOption = new Option<string?>("--directory", "-d")
        {
            Description = "处理指定目录下的所有NCM文件",
        };

        rootCommand.Options.Add(directoryOption);

        // 递归选项
        var recursiveOption = new Option<bool>("--recursive", "-r")
        {
            Description = "递归处理子目录",
        };

        rootCommand.Options.Add(recursiveOption);

        // 输出目录选项
        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "指定输出目录",
        };

        rootCommand.Options.Add(outputOption);

        // 文件参数
        var filesArgument = new Argument<string[]>("files")
        {
            Description = "要处理的NCM文件",
            Arity = ArgumentArity.ZeroOrMore,
        };

        rootCommand.Arguments.Add(filesArgument);

        rootCommand.SetAction(parseResult =>
        {
            if (parseResult.GetValue(versionOption))
            {
                var asm = Assembly.GetExecutingAssembly();

                string ver = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                 ?? asm.GetName().Version?.ToString()
                 ?? "unknown";

                Console.WriteLine(ver);

                return 0;
            }

            string? directory = parseResult.GetValue(directoryOption);
            bool recursive = parseResult.GetValue(recursiveOption);
            string? output = parseResult.GetValue(outputOption);
            string[] files = parseResult.GetValue(filesArgument) ?? [];

            ProcessFiles(directory, recursive, output, files);

            return 0;
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static void ProcessFiles(string? directory, bool recursive, string? output, string[] files)
    {
        // === 1. 参数校验 ===
        if (string.IsNullOrEmpty(directory) && files.Length == 0)
        {
            Console.WriteLine("错误: 请指定要处理的文件或目录");
            Console.WriteLine("使用 --help 查看帮助信息");

            return;
        }

        if (recursive && string.IsNullOrEmpty(directory))
        {
            Console.WriteLine("错误: -r 选项需要配合 -d 选项使用");

            return;
        }

        // === 2. 输出目录准备 ===
        string? outputDir = null;

        if (!string.IsNullOrWhiteSpace(output))
        {
            if (File.Exists(output))
            {
                Console.WriteLine($"错误: '{output}' 不是一个有效的目录");

                return;
            }

            Directory.CreateDirectory(output);
            outputDir = output;
        }

        // === 3. 收集所有待处理文件 ===
        var filesToProcess = CollectFiles(directory, recursive, files).ToList();

        if (filesToProcess.Count == 0)
        {
            Console.WriteLine("未找到任何 .ncm 文件");

            return;
        }

        // === 4. 逐个处理文件 ===
        foreach ((string filePath, string? relativeToBase) in filesToProcess)
        {
            string? targetOutputDir = null;

            if (outputDir != null && relativeToBase != null)
            {
                targetOutputDir = Path.Combine(outputDir, Path.GetDirectoryName(relativeToBase) ?? "");
                Directory.CreateDirectory(targetOutputDir);
            }

            ProcessSingleFile(filePath, targetOutputDir);
        }
    }

    private static IEnumerable<(string FilePath, string? RelativePath)> CollectFiles(
        string? directory,
        bool recursive,
        string[] files
        )
    {
        var list = new List<(string, string?)>();

        // 处理命令行传入的文件
        foreach (string file in files)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"警告: 文件 '{file}' 不存在，跳过");

                continue;
            }

            if (file.EndsWith(".ncm", StringComparison.OrdinalIgnoreCase))
            {
                list.Add((file, null)); // 无相对路径
            }
        }

        // 处理目录中的文件
        if (string.IsNullOrEmpty(directory))
            return list;

        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"错误: 目录 '{directory}' 不存在");

            return list;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] ncmFiles = Directory.GetFiles(directory, "*.ncm", searchOption);

        list.AddRange(from file in ncmFiles let relativePath = Path.GetRelativePath(directory, file) select (file, relativePath));

        return list;
    }

    private static void ProcessSingleFile(string filePath, string? outputDir)
    {
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
