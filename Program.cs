using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using ModelContextProtocol.Types;

namespace UnityMcpServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new McpServer(new ServerInfo
            {
                Name = "unity-mcp-server",
                Version = "1.1.0" 
            });

            server.SetCapabilities(new ServerCapabilities
            {
                Tools = new ToolsCapability()
            });

            // 注册 Lua 脚本创建工具
            server.AddTool(new Tool
            {
                Name = "create_lua_script",
                Description = "创建Unity Lua脚本",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        scriptName = new { type = "string", description = "脚本名称 (不含.lua后缀)" }
                    },
                    required = new[] { "scriptName" }
                }
            }, CreateLuaScriptHandler);
            
            // 注册活动奖励补领工具
            server.AddTool(new Tool
            {
                Name = "add_activity_retro_claim",
                Description = "在Lua脚本中添加活动奖励补领功能",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        luaScriptPath = new { type = "string", description = "相对于Unity项目Assets目录的Lua脚本路径 (例如: Resources/Lua/activity.lua)" },
                        activityKey = new { type = "string", description = "活动的唯一标识Key" }
                    },
                    required = new[] { "luaScriptPath", "activityKey" }
                }
            }, AddActivityRetroClaimHandler);

            var transport = new StdioServerTransport();
            await server.StartAsync(transport);
            Console.Error.WriteLine("Unity MCP Server 已启动 (版本 1.1.0)");
            await Task.Delay(Timeout.Infinite);
        }


        private static async Task<ToolResult> CreateLuaScriptHandler(ToolCall call)
        {
            try
            {
                var args = call.Arguments;
                string scriptName = args["scriptName"]?.ToString();

                string projectPath = GetUnityProjectPath();
                // Lua脚本通常放在Resources目录下以便Unity打包
                string scriptsPath = Path.Combine(projectPath, "Assets", "Resources", "Lua");
                Directory.CreateDirectory(scriptsPath);

                string filePath = Path.Combine(scriptsPath, $"{scriptName}.lua");
                await File.WriteAllTextAsync(filePath, $"-- {scriptName}.lua");

                return new ToolResult { Content = new[] { new TextContent { Text = $"成功创建Lua脚本: {filePath}" } } };
            }
            catch (Exception ex)
            {
                return new ToolResult { IsError = true, Error = new ToolError { Message = $"创建Lua脚本失败: {ex.Message}" } };
            }
        }

        private static async Task<ToolResult> AddActivityRetroClaimHandler(ToolCall call)
        {
            const string anchor = "---@Activity Retro Claim Anchor";
            try
            {
                var args = call.Arguments;
                string luaScriptPath = args["luaScriptPath"]?.ToString();
                string activityKey = args["activityKey"]?.ToString();

                string projectPath = GetUnityProjectPath();
                string fullPath = Path.Combine(projectPath, "Assets", luaScriptPath);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"指定的Lua脚本不存在: {fullPath}");
                }

                string content = await File.ReadAllTextAsync(fullPath);

                if (!content.Contains(anchor))
                {
                    throw new InvalidOperationException($"脚本中未找到用于添加代码的锚点: '{anchor}'");
                }

                string template = GetRetroClaimTemplate(activityKey);
                string newContent = content.Replace(anchor, $"{anchor}{template}");

                await File.WriteAllTextAsync(fullPath, newContent);

                return new ToolResult { Content = new[] { new TextContent { Text = $"成功为活动 '{activityKey}' 添加奖励补领功能到: {luaScriptPath}" } } };
            }
            catch (Exception ex)
            {
                return new ToolResult { IsError = true, Error = new ToolError { Message = $"添加功能失败: {ex.Message}" } };
            }
        }

        // --- Helper Methods ---

        private static string GetUnityProjectPath()
        {
            return Environment.GetEnvironmentVariable("UNITY_PROJECT_PATH")
                ?? throw new InvalidOperationException("错误: UNITY_PROJECT_PATH 环境变量未设置。请设置该变量为您的Unity项目根目录。");
        }

        private static string GenerateCSharpScriptContent(string scriptName, string scriptType, string namespaceName)
        {
            return scriptType switch
            {
                "MonoBehaviour" => GenerateMonoBehaviour(scriptName, namespaceName),
                "ScriptableObject" => GenerateScriptableObject(scriptName, namespaceName),
                "Editor" => GenerateEditorScript(scriptName, namespaceName),
                _ => throw new ArgumentException($"不支持的脚本类型: {scriptType}")
            };
        }
        
        private static string GetRetroClaimTemplate(string activityKey)
        {
            return new StringBuilder()
                .AppendLine($"-- Auto-generated by MCP for activity: {activityKey}")
                .AppendLine($"function ActivityRetroClaim_{activityKey}(player)")
                .AppendLine($"    -- 检查活动是否已结束且玩家有资格补领")
                .AppendLine($"    local canClaim = CheckActivityStatus("{activityKey}", player)")
                .AppendLine($"    if not canClaim then")
                .AppendLine($"        return false, "不满足补领条件"")
                .AppendLine($"    end")
                .AppendLine()
                .AppendLine($"    -- 发放奖励")
                .AppendLine($"    local rewards = GetActivityRewards("{activityKey}")")
                .AppendLine($"    GiveRewardsToPlayer(player, rewards)")
                .AppendLine()
                .AppendLine($"    -- 记录补领日志")
                .AppendLine($"    LogRetroClaim(player, "{activityKey}")")
                .AppendLine()
                .AppendLine($"    return true, "奖励补领成功"")
                .AppendLine($"end")
                .ToString();
        }

        private static string GenerateMonoBehaviour(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;")
              .AppendLine();

            bool hasNamespace = !string.IsNullOrEmpty(ns);
            if (hasNamespace) sb.AppendLine($"namespace {ns}{{");

            string indent = hasNamespace ? "    " : "";
            sb.AppendLine($"{indent}public class {name} : MonoBehaviour")
              .AppendLine($"{indent}{{")
              .AppendLine($"{indent}    void Start()")
              .AppendLine($"{indent}    {{")
              .AppendLine($"{indent}        ")
              .AppendLine($"{indent}    }}")
              .AppendLine()
              .AppendLine($"{indent}    void Update()")
              .AppendLine($"{indent}    {{")
              .AppendLine($"{indent}        ")
              .AppendLine($"{indent}    }}")
              .AppendLine($"{indent}}}");

            if (hasNamespace) sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateScriptableObject(string name, string ns)
        {
            // This can also be refactored similarly if more logic is added.
            return $@"using UnityEngine;

[CreateAssetMenu(fileName = ""{name}"", menuName = ""ScriptableObjects/{name}"")]
public class {name} : ScriptableObject
{{
    
}}";
        }
        
        private static string GenerateEditorScript(string name, string ns)
        {
            // Basic Editor script template
            return $@"using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonoBehaviour))] // Change MonoBehaviour to the type you want to inspect
public class {name} : Editor
{{
    public override void OnInspectorGUI()
    {{
        base.OnInspectorGUI();

        // Add custom editor logic here
    }}
}}";
        }
    }
}