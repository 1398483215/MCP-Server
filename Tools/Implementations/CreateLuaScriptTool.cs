using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityMcpServer.Utils;

namespace UnityMcpServer.Tools.Implementations
{
    public class CreateLuaScriptTool : ITool
    {
        public string Name => "create_lua_script";
        public string Description => "创建Unity Lua脚本";

        public object InputSchema => new
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
        };

        public async Task<object> ExecuteAsync(JObject args)
        {
            string scriptName = args?["scriptName"]?.ToString();
            if (string.IsNullOrEmpty(scriptName))
            {
                throw new ArgumentNullException("scriptName", "脚本名称不能为空");
            }

            string projectPath = UnityProjectFinder.GetUnityProjectPath();
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
    }
}
