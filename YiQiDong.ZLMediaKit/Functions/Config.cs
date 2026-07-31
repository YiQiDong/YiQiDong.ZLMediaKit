using Quick.Fields;
using YiQiDong.Protocol.V1.Model;
using YiQiDong.Core;
using IniParser.Model;
using YiQiDong.Agent;
using Quick.Utils;

namespace YiQiDong.ZLMediaKit.Functions
{
    class Config : AbstractFunction
    {
        public const string CONFIG_FILE = "config.ini";
        public static Config Instance { get; private set; }

        private string name;
        public override string Name => name;
        private IniData iniData = null;
        private string containerConfigFile;
        private IniParser.FileIniDataParser iniDataParser = new IniParser.FileIniDataParser();

        public void RefreshProperties()
        {
            if (File.Exists(containerConfigFile))
            {
                iniData = iniDataParser.ReadFile(containerConfigFile);
            }
        }

        public Config(string name, string imageFolder, string containerFolder)
        {
            Instance = this;
            this.name = name;

            containerConfigFile = Path.Combine(containerFolder, CONFIG_FILE);
            if (!File.Exists(containerConfigFile))
            {
                var folder = Path.GetDirectoryName(containerConfigFile);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                var imageConfigFile = Path.Combine(imageFolder, CONFIG_FILE);
                if (File.Exists(imageConfigFile))
                    File.Copy(imageConfigFile, containerConfigFile, true);
            }
            RefreshProperties();
        }


        private List<FieldForGet> innerGet(FunctionRequest request, bool isReadOnly = false)
        {
            List<FieldForGet> list = new List<FieldForGet>();
            if (!File.Exists(containerConfigFile))
            {
                list.Add(new FieldForGet() { Name = "失败", Description = $"配置文件[{CONFIG_FILE}]不存在！", Input_ReadOnly = true, Type = FieldType.Alert });
                return list;
            }
            RefreshProperties();

            var tmpKey = "general.mediaServerId";
            if (iniData["general"].ContainsKey("mediaServerId"))
                list.Add(new FieldForGet()
                {
                    Id = tmpKey,
                    Name = "媒体服务器编号",
                    Type = FieldType.InputText,
                    Input_ReadOnly = isReadOnly,
                    Value = request == null ? iniData["general"]["mediaServerId"] : request.GetFieldValue(tmpKey),
                    Input_AllowBlank = false
                });

            tmpKey = "http.port";
            if (iniData["http"].ContainsKey("port"))
                list.Add(new FieldForGet()
                {
                    Id = tmpKey,
                    Name = "HTTP监听端口",
                    Type = FieldType.InputNumber,
                    Input_ReadOnly = isReadOnly,
                    Value = request == null ? iniData["http"]["port"] : request.GetFieldValue(tmpKey),
                    Input_AllowBlank = false,
                    Description = "HTTP监听端口，默认为8180"
                });

            tmpKey = "http.charSet";
            if (iniData["http"].ContainsKey("charSet"))
                list.Add(new FieldForGet()
                {
                    Id = tmpKey,
                    Name = "HTTP字符集",
                    Type = FieldType.InputSelect,
                    Input_ReadOnly = isReadOnly,
                    InputSelect_Options = new Dictionary<string, string>()
                    {
                        ["gb2312"] = "GB2312",
                        ["utf-8"] = "UTF-8"
                    },
                    Value = request == null ? iniData["http"]["charSet"] : request.GetFieldValue(tmpKey),
                    Input_AllowBlank = false,
                    Description = "Windows上使用GB2312，Linux系统上使用UTF-8"
                });

            tmpKey = "api.secret";
            if (iniData["api"].ContainsKey("secret"))
                list.Add(new FieldForGet()
                {
                    Id = tmpKey,
                    Name = "API密码",
                    Type = FieldType.InputText,
                    Input_ReadOnly = isReadOnly,
                    Value = request == null ? iniData["api"]["secret"] : request.GetFieldValue(tmpKey),
                    Input_AllowBlank = false,
                    Description = "API密码，默认为:035c73f7-bb6b-4889-a715-d9eb2d1925cc"
                });

            tmpKey = "rtp_proxy.port";
            if (iniData["rtp_proxy"].ContainsKey("port"))
                list.Add(new FieldForGet()
                {
                    Id = tmpKey,
                    Name = "RTP代理监听端口",
                    Type = FieldType.InputNumber,
                    Input_ReadOnly = isReadOnly,
                    Value = request == null ? iniData["rtp_proxy"]["port"] : request.GetFieldValue(tmpKey),
                    Input_AllowBlank = false,
                    Description = "RTP代理监听端口，默认为10000"
                });

            tmpKey = "rtsp.port";
            if (iniData["rtsp"].ContainsKey("port"))
                list.Add(new FieldForGet()
                {
                    Id = tmpKey,
                    Name = "RTSP监听端口",
                    Type = FieldType.InputNumber,
                    Input_ReadOnly = isReadOnly,
                    Value = request == null ? iniData["rtsp"]["port"] : request.GetFieldValue(tmpKey),
                    Input_AllowBlank = false,
                    Description = "RTSP监听端口，默认为554"
                });

            tmpKey = "hook.enable";
            if (iniData["hook"].ContainsKey("enable"))
                list.Add(new FieldForGet()
                {
                    Id = tmpKey,
                    Name = "鉴权",
                    Type = FieldType.InputSelect,
                    Input_ReadOnly = isReadOnly,
                    InputSelect_Options = new Dictionary<string, string>()
                    {
                        ["0"] = "禁用",
                        ["1"] = "启用"
                    },
                    Value = request == null ? iniData["hook"]["enable"] : request.GetFieldValue(tmpKey),
                    Input_AllowBlank = false,
                    Description = "是否启用hook事件，启用后，推拉流都将进行鉴权"
                });

            tmpKey = "hook.url";
            if (iniData["hook"].ContainsKey("on_flow_report"))
            {
                var url = iniData["hook"]["on_flow_report"].Replace("/on_flow_report", string.Empty);
                list.Add(new FieldForGet()
                {
                    Id = tmpKey,
                    Name = "鉴权URL",
                    Type = FieldType.InputText,
                    Input_ReadOnly = isReadOnly,
                    Value = request == null ? url : request.GetFieldValue(tmpKey),
                    Input_AllowBlank = false
                });
            }
            return list;
        }

        public override FieldForGet[] Execute(FunctionRequest request)
        {
            if(request == null)
                return Get();
            return Post(request);
        }

        public FieldForGet[] Get()
        {
            var isReadOnly = AgentContext.Container.AutoStart;
            var list = innerGet(null, isReadOnly);
            if (!isReadOnly)
                addSaveButton(list);
            return list.ToArray();
        }

        public FieldForGet[] Post(FunctionRequest request)
        {
            var list = innerGet(request);
            if (request.IsFieldIdsMatch("Save"))
            {
                try
                {
                    Save(request.Fields);
                    list.Add(new FieldForGet()
                    {
                        Name = "保存成功",
                        Description = $"配置文件[{CONFIG_FILE}]保存成功！",
                        Type = FieldType.MessageBox
                    });
                }
                catch (Exception ex)
                {
                    list.Add(new FieldForGet()
                    {
                        Name = "错误",
                        Description = ex.Message,
                        Type = FieldType.Alert,
                        Input_ReadOnly = true
                    });
                }
                addSaveButton(list);
            }
            return list.ToArray();
        }

        public void Save(FieldForPost[] fields)
        {
            if (!File.Exists(containerConfigFile))
                throw new IOException($"配置文件[{CONFIG_FILE}]不存在！");

            try
            {
                if (fields == null)
                    throw new ArgumentNullException(nameof(fields));
                foreach (var field in fields)
                {
                    if (string.IsNullOrEmpty(field.Id))
                        continue;
                    var strs = field.Id.Split('.');
                    if (strs.Length <= 1)
                        continue;
                    var sectionName = strs[0];
                    var keyName = strs[1];
                    var section = iniData[sectionName];
                    if (field.Id == "hook.url")
                    {
                        var baseUrl = field.Value;
                        section["on_flow_report"] = $"{baseUrl}/on_flow_report";
                        section["on_http_access"] = $"{baseUrl}/on_http_access";
                        section["on_play"] = $"{baseUrl}/on_play";
                        section["on_publish"] = $"{baseUrl}/on_publish";
                        section["on_record_mp4"] = $"{baseUrl}/on_record_mp4";
                        section["on_record_ts"] = $"{baseUrl}/on_record_ts";
                        section["on_rtsp_auth"] = $"{baseUrl}/on_rtsp_auth";
                        section["on_rtsp_realm"] = $"{baseUrl}/on_rtsp_realm";
                        section["on_shell_login"] = $"{baseUrl}/on_shell_login";
                        section["on_stream_changed"] = $"{baseUrl}/on_stream_changed";
                        section["on_stream_none_reader"] = $"{baseUrl}/on_stream_none_reader";
                        section["on_stream_not_found"] = $"{baseUrl}/on_stream_not_found";
                        section["on_server_started"] = $"{baseUrl}/on_server_started";
                        section["on_server_keepalive"] = $"{baseUrl}/on_server_keepalive";
                    }
                    else
                    {
                        if (!section.ContainsKey(keyName))
                            throw new ApplicationException($"Section[{sectionName}] not exist key[{keyName}].");
                        section[keyName] = field.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException(ExceptionUtils.GetExceptionString(ex));
            }
            iniDataParser.WriteFile(containerConfigFile, iniData);
            //将等号两边的空格去掉，不然ZLMediaKit会把空格当作配置的值
            var fileContent = File.ReadAllText(containerConfigFile);
            fileContent = fileContent.Replace(" = ", "=");
            File.WriteAllText(containerConfigFile, fileContent);

            //保存成功后重新加载配置文件
            RefreshProperties();
        }

        private void addSaveButton(List<FieldForGet> list)
        {
            list.Add(new FieldForGet() { Id = "Save", Name = "保存", Type = FieldType.Button });
        }
    }
}
