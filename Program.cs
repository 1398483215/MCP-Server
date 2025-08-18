using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMcpServer
{
    // Define a concrete class for tool information
    public class ToolInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("inputSchema")]
        public object InputSchema { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            
            var server = new SimpleMcpServer();
            await server.Start();
        }
    }
    
    public class SimpleMcpServer
    {
        public async Task Start()
        {
            string line;
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                try
                {
                    var request = JObject.Parse(line);
                    await HandleRequest(request);
                }
                catch (Exception ex)
                {
                    await SendError($"解析请求失败: {ex.Message}");
                }
            }
        }
        
        private async Task HandleRequest(JObject request)
        {
            string method = request["method"]?.ToString();
            var id = request["id"];
            
            switch (method)
            {
                case "initialize":
                    await SendResponse(new
                    {
                        jsonrpc = "2.0",
                        id = id,
                        result = new
                        {
                            protocolVersion = "2024-11-05",
                            capabilities = new { tools = new { } },
                            serverInfo = new
                            {
                                name = "unity-mcp-server-lua",
                                version = "2.2.0" // Final version
                            }
                        }
                    });
                    break;
                    
                case "tools/list":
                    await SendResponse(new
                    {
                        jsonrpc = "2.0",
                        id = id,
                        result = new
                        {
                            // Use the concrete ToolInfo class here
                            tools = new ToolInfo[]
                            {
                                new ToolInfo
                                {
                                    Name = "create_lua_script",
                                    Description = "创建Unity Lua脚本",
                                    InputSchema = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            scriptName = new
                                            {
                                                type = "string",
                                                description = "脚本名称 (不含.lua后缀)"
                                            }
                                        },
                                        required = new[] { "scriptName" }
                                    }
                                },
                                new ToolInfo
                                {
                                    Name = "add_activity_retro_claim",
                                    Description = "在配置文件中添加一个通用的活动奖励补领功能",
                                    InputSchema = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            activityKey = new
                                            {
                                                type = "string",
                                                description = "活动Key (例如：MyNewActivity)"
                                            },
                                            rewardType = new
                                            {
                                                type = "string",
                                                description = "奖励类型 (例如：MyNewActivityRewardType)"
                                            },
                                            multipleLanguageKey = new
                                            {
                                                type = "string",
                                                description = "多语言Key (例如：MyNewActivity)"
                                            }
                                        },
                                        required = new[] { "activityKey", "rewardType", "multipleLanguageKey" }
                                    }
                                }
                            }
                        }
                    });
                    break;
                    
                case "tools/call":
                    await HandleToolCall(request, id);
                    break;
            }
        }
        
        private async Task HandleToolCall(JObject request, JToken id)
        {
            var toolName = request["params"]?["name"]?.ToString();
            var arguments = request["params"]?["arguments"] as JObject;
            
            try
            {
                object result = toolName switch
                {
                    "create_lua_script" => await CreateLuaScript(arguments),
                    "add_activity_retro_claim" => await AddActivityRetroClaim(arguments),
                    _ => throw new Exception($"未知工具: {toolName}")
                };
                
                await SendResponse(new
                {
                    jsonrpc = "2.0",
                    id = id,
                    result = result
                });
            }
            catch (Exception ex)
            {
                await SendError($"工具调用失败: {ex.Message}", id);
            }
        }
        
        private async Task<object> CreateLuaScript(JObject args)
        {
            string scriptName = args?["scriptName"]?.ToString();
            if (string.IsNullOrEmpty(scriptName))
            {
                throw new ArgumentNullException("scriptName", "脚本名称不能为空");
            }

            string projectPath = GetUnityProjectPath();
            string scriptsPath = Path.Combine(projectPath, "Assets", "Resources", "Lua");
            Directory.CreateDirectory(scriptsPath);

            string filePath = Path.Combine(scriptsPath, $"{scriptName}.lua");
            await File.WriteAllTextAsync(filePath, $"-- {scriptName}.lua\n\n---@Activity Retro Claim Anchor\n");

            return new
            {
                content = new[]
                {
                    new { type = "text", text = $"成功创建Lua脚本: {filePath}" }
                }
            };
        }
        
        private async Task<object> AddActivityRetroClaim(JObject args)
        {
            string activityKey = args?["activityKey"]?.ToString();
            string rewardType = args?["rewardType"]?.ToString();
            string multipleLanguageKey = args?["multipleLanguageKey"]?.ToString();

            if (string.IsNullOrEmpty(activityKey) || string.IsNullOrEmpty(rewardType) || string.IsNullOrEmpty(multipleLanguageKey))
            {
                throw new ArgumentException("Missing required arguments for AddActivityRetroClaim.");
            }

            string unityProjectPath = GetUnityProjectPath();
            string popupSeqConfigPath = Path.Combine(unityProjectPath, "Assets", "HotAssets", "LuaScript", "Config", "PopupSeqConfig.lua");
            string popupFunConfigPath = Path.Combine(unityProjectPath, "Assets", "HotAssets", "LuaScript", "Config", "PopupFunConfig.lua");

            // 1. Modify PopupSeqConfig.lua
            string seqConfigContent = await File.ReadAllTextAsync(popupSeqConfigPath);
            string newSeqEntry = GetPopupSeqConfigEntry(activityKey);
            string seqAnchor = "        --淘汰赛补领";
            if (!seqConfigContent.Contains(seqAnchor))
            {
                throw new InvalidOperationException($"在 {popupSeqConfigPath} 中未找到锚点: '{seqAnchor}'");
            }
            string updatedSeqConfigContent = seqConfigContent.Replace(seqAnchor, newSeqEntry + "\n" + seqAnchor);
            await File.WriteAllTextAsync(popupSeqConfigPath, updatedSeqConfigContent);

            // 2. Modify PopupFunConfig.lua
            string funConfigContent = await File.ReadAllTextAsync(popupFunConfigPath);
            string newFunEntries = GetPopupFunConfigEntries(activityKey, rewardType, multipleLanguageKey);
            string funAnchor = "-- 淘汰赛补领弹窗";
            if (!funConfigContent.Contains(funAnchor))
            {
                throw new InvalidOperationException($"在 {popupFunConfigPath} 中未找到锚点: '{funAnchor}'");
            }
            string updatedFunConfigContent = funConfigContent.Replace(funAnchor, newFunEntries + "\n" + funAnchor);
            await File.WriteAllTextAsync(popupFunConfigPath, updatedFunConfigContent);

            return new
            {
                content = new[]
                {
                    new { type = "text", text = $"成功为活动 '{activityKey}' 添加通用奖励补领功能。" }
                }
            };
        }

        private string GetPopupSeqConfigEntry(string activityKey)
        {
            var sb = new StringBuilder();
            sb.AppendLine("    {");
            sb.AppendLine($"        --{activityKey}补领");
            sb.AppendLine($"        [\"key\"] = \"{activityKey}CompensationView\",");
            sb.AppendLine("        [\"daily\"] = false,");
            sb.AppendLine("        [\"downloadKey\"] = DlcNames.Base.PopTipView,");
            sb.AppendLine($"        [\"func\"] = PopupFunConfig.CheckPushNotReceiving{activityKey}RewardView,");
            sb.AppendLine($"        [\"currentNeed\"] = PopupFunConfig.need{activityKey}Reward,");
            sb.AppendLine("    },");
            return sb.ToString();
        }

        private string GetPopupFunConfigEntries(string activityKey, string rewardType, string multipleLanguageKey)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- {activityKey}补领弹窗");
            sb.AppendLine($"function PopupFunConfig.CheckPushNotReceiving{activityKey}RewardView(queueName)");
            sb.AppendLine($"    PopupFunConfig.CheckPushCommonNotReceivingRewardView(\"{activityKey}CompensationView\", \"{rewardType}\", \"{multipleLanguageKey}\", queueName)");
            sb.AppendLine("end");
            sb.AppendLine();
            sb.AppendLine($"function PopupFunConfig.need{activityKey}Reward()");
            sb.AppendLine($"    return PopupFunConfig.NeedNotReceivingReward(\"{rewardType}\")");
            sb.AppendLine("end");
            sb.AppendLine();
            return sb.ToString();
        }
        
        private string GetUnityProjectPath()
        {
            // 首先尝试从环境变量获取Unity项目路径
            string envPath = Environment.GetEnvironmentVariable("UNITY_PROJECT_PATH");
            if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            {
                string assetsPath = Path.Combine(envPath, "Assets");
                if (Directory.Exists(assetsPath))
                {
                    return envPath;
                }
            }
    
            // 如果环境变量不可用，则回退到原来的搜索方法
            string currentDir = Directory.GetCurrentDirectory();
            DirectoryInfo dir = new DirectoryInfo(currentDir);
            // Search up the directory tree to find the project root (which contains the Assets folder)
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }
    
            if (dir == null)
            {
                // If we can't find it, we'll have to throw an exception because we can't proceed.
                throw new DirectoryNotFoundException($"Could not find the Unity project root. Environment variable UNITY_PROJECT_PATH: '{envPath}', Current directory: '{currentDir}'");
            }
    
            return dir.FullName;
        }

        private async Task SendResponse(object response)
        {
            string json = JsonConvert.SerializeObject(response, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            await Console.Out.WriteLineAsync(json);
            await Console.Out.FlushAsync();
        }
        
        private async Task SendError(string message, JToken id = null)
        {
            await SendResponse(new
            {
                jsonrpc = "2.0",
                id = id,
                error = new
                {
                    code = -32603,
                    message = message
                }
            });
        }
    }
}
