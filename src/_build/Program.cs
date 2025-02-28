using Quick.Build;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using System;
using System.IO;
using System.Linq;

string ZLMEDIAKIT_RESOURCE_FOLDER = "resource/ZLMediaKit";

//版本号
var buildVersion = DateTime.Now.ToString("yyyyMMddHHmmss");
var buildTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
var version = "7.0-c1b8a48c";
var productDir = "YiQiDong.ZLMediaKit";

//准备目录变量
var appFolder = QbFolder.GetAppFolder();
if (appFolder == Environment.CurrentDirectory)
    Environment.CurrentDirectory = Path.GetFullPath("../../../../../");
var baseFolder = Environment.CurrentDirectory;
var outFolder = Path.GetFullPath("bin");
if (!Directory.Exists(outFolder))
    Directory.CreateDirectory(outFolder);
var productName = QbJson.ReadString(Path.Combine($"src/{productDir}/YiQiDong.Image.json"), "Name");

//开始
Console.WriteLine("----------------------------------");
Console.WriteLine($"  欢迎使用[{productName}]发布脚本");
Console.WriteLine("----------------------------------");
Console.WriteLine();
Console.WriteLine("请选择编译架构(一个都不勾选代表全选)：");
var resourceDirInfo = new DirectoryInfo("resource/ZLMediaKit");
var allArchs = resourceDirInfo.GetFiles("*.7z").Select(t => Path.GetFileNameWithoutExtension(t.Name)).ToArray();
var selectArchs = QbSelect.MultiSelect(allArchs.ToDictionary(t => t, t => t).ToArray(), selectedForegroundColor: ConsoleColor.Green);
if (selectArchs == null || selectArchs.Length == 0)
    selectArchs = allArchs;

foreach (var rid in selectArchs)
{
    var publishFolder = $"src/{productDir}/bin/Release/{rid}/publish";
    Console.WriteLine($"开始编译[{rid}]...");
    Console.WriteLine("正在删除Release目录...");
    //先删除Release目录
    QbFolder.DeleteFolders("src", "Release", SearchOption.AllDirectories);

    if (!Directory.Exists(publishFolder))
        Directory.CreateDirectory(publishFolder);

    Console.WriteLine($"正在发布{productDir}项目...");
    QbCommand.Run("dotnet", $"publish src/{productDir} -c Release -r {rid} --self-contained -p:PublishTrimmed=true");
    //复制文件
    QbFile.CopyFiles($"src/{productDir}", publishFolder, "YiQiDong.Image.*", true);
    //修改容器信息文件中的版本号
    QbJson.WriteString(Path.Combine(publishFolder, "YiQiDong.Image.json"), "Version", version);
    //修改Agent的值
    if (rid.StartsWith("win-"))
        QbJson.WriteString(Path.Combine(publishFolder, "YiQiDong.Image.json"), "AgentExecute", $"{productDir}.exe");
    else
        QbJson.WriteString(Path.Combine(publishFolder, "YiQiDong.Image.json"), "AgentExecute", productDir);
    QbJson.Write(Path.Combine(publishFolder, "YiQiDong.Image.json"), "Platform", new string[] { rid });
    QbJson.Write(Path.Combine(publishFolder, "YiQiDong.Image.json"), "BuildTime", buildTime);

    Console.WriteLine($"正在制作弈启动镜像[{rid}]...");
    var outFile = Path.Combine(outFolder, $"{productName}-{version}-{rid}_{buildVersion}.ymg");
    //再删除ymg文件
    QbFile.Delete(outFile);
    using (var archive = ZipArchive.Create())
    {
        archive.AddAllFromDirectory(publishFolder);
        var binCompressFile = rid + ".7z";
        archive.AddEntry(binCompressFile, Path.Combine(ZLMEDIAKIT_RESOURCE_FOLDER, binCompressFile));
        archive.SaveTo(outFile, CompressionType.LZMA);
    }
}
Console.WriteLine("完成");
//打开窗口
QbGui.OpenFolder("bin");