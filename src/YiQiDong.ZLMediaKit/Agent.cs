using System.Diagnostics;
using System.Runtime.InteropServices;
using YiQiDong.ZLMediaKit.Functions;
using YiQiDong.Core;
using YiQiDong.Core.Utils;
using YiQiDong.Protocol.V1.Model;
using YiQiDong.Agent;
using Mono.Unix.Native;
using SharpCompress.Archives;
using Mono.Unix;

namespace YiQiDong.ZLMediaKit
{
    public class Agent : AbstractAgent
    {
        public static Agent Instance { get; private set; }

        public Process Process { get; set; }
        private CancellationTokenSource cts;

        public Agent()
        {
            Instance = this;
        }

        private string getZLMediaKitBinCompressFile()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X64:
                        return "win-x64.7z";
                    default:
                        outputNotSupportOsAndArchitecture();
                        return null;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X64:
                        return "linux-x64.7z";
                    case Architecture.Arm64:
                        return "linux-arm64.7z";
                    default:
                        outputNotSupportOsAndArchitecture();
                        return null;
                }
            }
            else
            {
                outputNotSupportOsAndArchitecture();
                return null;
            }
        }

        private string getZLMediaKitExecuteFile()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "MediaServer.exe";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "MediaServer";
            }
            else
            {
                return "MediaServer";
            }
        }

        public override void Init()
        {
            base.Init();
            if (AgentContext.IsContainerRuning)
            {
                var imageFolder = AgentContext.Container.ImageFolder;
                var containerFolder = AgentContext.Container.ContainerFolder;

                AddFunction(new Config("配置修改", imageFolder, containerFolder), false);
                AddFunction(new Config("配置查看", imageFolder, containerFolder), true);

                AddFunction(new AdvancedConfig(containerFolder));

                //检查复制ini文件
                FileSystemUtils.CopyFile(Path.Combine(imageFolder, Config.CONFIG_FILE), containerFolder);
            }
        }

        public override void Start()
        {
            base.Start();
            cts = new();
            Task.Run(() =>
            {
                try
                {
                    innnerStart();
                }
                catch (Exception ex)
                {
                    AgentContext.LogError($"启动容器时失败，原因：{ex}");
                }
            });
        }

        private void outputNotSupportOsAndArchitecture()
        {
            AgentContext.LogWarn($"不支持的操作系统[{RuntimeInformation.OSDescription}]+平台架构[{RuntimeInformation.OSArchitecture}]。");
        }

        private void innnerStart()
        {
            if (Process != null)
                return;
            if (AgentContext.Container == null || !AgentContext.Container.AutoStart)
                return;

            var imageFolder = AgentContext.Container.ImageFolder;
            var containerFolder = AgentContext.Container.ContainerFolder;
            var binCompressFile = getZLMediaKitBinCompressFile();
            var executeFile = getZLMediaKitExecuteFile();

            //检查复制可执行文件
            var containerExeFile = Path.Combine(containerFolder, executeFile);
            var imageBinCompressFile = Path.Combine(imageFolder, binCompressFile);
            if (!File.Exists(imageBinCompressFile))
                AgentContext.LogWarn($"未找到二进制压缩文件[{imageBinCompressFile}]");
            //如果容器目录中可执行文件不存在，或者文件修改时间与二进制压缩文件不一致，则重新解压
            if (File.Exists(imageBinCompressFile))
                if (!File.Exists(containerExeFile) || File.GetLastWriteTime(containerExeFile) != File.GetLastWriteTime(imageBinCompressFile))
                {
                    AgentContext.LogInfo($"正在解压二进制压缩文件[{imageBinCompressFile}]...");
                    using (var archive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(imageBinCompressFile))
                        archive.WriteToDirectory(
                            containerFolder,
                            new SharpCompress.Common.ExtractionOptions()
                            {
                                Overwrite = true
                            });
                    File.SetLastWriteTime(containerExeFile, File.GetLastWriteTime(imageBinCompressFile));
                    //如果当前是在Linux系统上
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        //如果文件没有可执行权限，则添加可执行权限
                        var fileInfo = new UnixFileInfo(Path.Combine(containerFolder, executeFile));
                        if (!fileInfo.FileAccessPermissions.HasFlag(FileAccessPermissions.UserExecute))
                            fileInfo.FileAccessPermissions |= FileAccessPermissions.UserExecute;
                    }
                }
            var process_filename = Path.Combine(containerFolder, getZLMediaKitExecuteFile());
            AgentContext.LogInfo("Process Filename：" + process_filename);

            ProcessStartInfo psi = new ProcessStartInfo(process_filename);
            //如果当前是在Linux系统上，则增加so文件搜索路径环境变量
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                psi.Environment["LD_LIBRARY_PATH"] = containerFolder;
            }
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = containerFolder;
            Process = Process.Start(psi);
            Process.EnableRaisingEvents = true;
            Process.OutputDataReceived += Process_OutputDataReceived;
            Process.ErrorDataReceived += Process_ErrorDataReceived;
            Process.BeginOutputReadLine();
            Process.BeginErrorReadLine();
            AgentContext.LogInfo($"进程[Id:{Process.Id},Name:{Process.ProcessName}]已经启动。");
            Process.Exited += Process_Exited;
        }

        private LogLevel lastLogLevel = LogLevel.Info;
        private void handleOutput(string line)
        {
            var logLevel = lastLogLevel;
            var logContent = line;

            //找到第一个空格
            var index_1 = line.IndexOf(" ");
            if (index_1 > 0)
            {
                //找到第二个空格
                var index_2 = line.IndexOf(" ", index_1 + 1);
                if (index_2 > 0)
                {
                    var logLevelIndex = index_2 + 1;
                    var index_3 = line.IndexOf(" ", logLevelIndex);
                    if (index_3 > 0)
                    {
                        var hasLogLevel = false;
                        switch (line.Substring(logLevelIndex, index_3 - logLevelIndex))
                        {
                            case "T":
                                logLevel = LogLevel.Trace;
                                hasLogLevel = true;
                                break;
                            case "D":
                                logLevel = LogLevel.Debug;
                                hasLogLevel = true;
                                break;
                            case "I":
                                logLevel = LogLevel.Info;
                                hasLogLevel = true;
                                break;
                            case "W":
                                logLevel = LogLevel.Warn;
                                hasLogLevel = true;
                                break;
                            case "E":
                                logLevel = LogLevel.Error;
                                hasLogLevel = true;
                                break;
                        }
                        if (hasLogLevel)
                        {
                            lastLogLevel = logLevel;
                            logContent = line.Substring(index_3 + 1);
                        }
                    }
                }
            }
            AgentContext.Log(logLevel, logContent);
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            handleOutput(e.Data);
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            handleOutput(e.Data);
        }

        private void delayStart(CancellationToken token)
        {
            Task.Delay(5000, token).ContinueWith(t =>
            {
                if (t.IsCanceled)
                    return;
                innnerStart();
            });
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            var process = (Process)sender;
            AgentContext.LogInfo($"进程[Id:{process.Id},Name:{process.ProcessName}]已经退出，退出码：{process.ExitCode}。");
            Process = null;
            if (cts.IsCancellationRequested)
                return;
            delayStart(cts.Token);
        }

        public override void Stop()
        {
            cts?.Cancel();
            Process?.Kill(true);
            Process = null;
            base.Stop();
        }
    }
}
