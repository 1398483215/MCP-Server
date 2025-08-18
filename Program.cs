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
                                    Description = "在Lua脚本中添加活动奖励补领功能",
                                    InputSchema = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            luaScriptPath = new
                                            {
                                                type = "string",
                                                description = "Lua脚本路径"
                                            },
                                            activityKey = new
                                            {
                                                type = "string",
                                                description = "活动Key"
                                            }
                                        },
                                        required = new[] { "luaScriptPath", "activityKey" }
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
            const string anchor = "---@Activity Retro Claim Anchor";
            string luaScriptPath = args?["luaScriptPath"]?.ToString();
            string activityKey = args?["activityKey"]?.ToString();

            if (string.IsNullOrEmpty(luaScriptPath)) throw new ArgumentNullException("luaScriptPath");
            if (string.IsNullOrEmpty(activityKey)) throw new ArgumentNullException("activityKey");

            string projectPath = GetUnityProjectPath();
            string fullPath = Path.Combine(projectPath, "Assets", luaScriptPath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("指定的Lua脚本不存在: " + fullPath);
            }

            string content = await File.ReadAllTextAsync(fullPath);

            if (!content.Contains(anchor))
            {
                throw new InvalidOperationException("脚本中未找到用于添加代码的锚点: '" + anchor + "'");
            }

            string template = GetRetroClaimTemplate(activityKey);
            string newContent = content.Replace(anchor, template + "\n" + anchor);

            await File.WriteAllTextAsync(fullPath, newContent);
            
            return new
            {
                content = new[]
                {
                    new { type = "text", text = $"成功为活动 '{activityKey}' 添加奖励补领功能到: {luaScriptPath}" }
                }
            };
        }
        
        private string GetUnityProjectPath()
        {
            return Environment.GetEnvironmentVariable("UNITY_PROJECT_PATH")
                ?? throw new InvalidOperationException("UNITY_PROJECT_PATH 未设置");
        }

        private string GetRetroClaimTemplate(string activityKey)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- Auto-generated by MCP for activity: " + activityKey);
            sb.AppendLine("function ActivityRetroClaim_" + activityKey + "(player)");
            sb.AppendLine("    -- 检查活动是否已结束且玩家有资格补领");
            sb.AppendLine("    local canClaim = CheckActivityStatus(\"" + activityKey + "\", player)");
            sb.AppendLine("    if not canClaim then");
            sb.AppendLine("        return false, \"不满足补领条件\"");
            sb.AppendLine("    end");
            sb.AppendLine();
            sb.AppendLine("    -- 发放奖励");
            sb.AppendLine("    local rewards = GetActivityRewards(\"" + activityKey + "\")");
            sb.AppendLine("    GiveRewardsToPlayer(player, rewards)");
            sb.AppendLine();
            sb.AppendLine("    -- 记录补领日志");
            sb.AppendLine("    LogRetroClaim(player, \"" + activityKey + "\")");
            sb.AppendLine();
            sb.AppendLine("    return true, \"奖励补领成功\"");
            sb.AppendLine("end");
            return sb.ToString();
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