
using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityMcpServer.Tools;

namespace UnityMcpServer.McpServer
{
    public class McpServer
    {
        private readonly ToolManager _toolManager;

        public McpServer()
        {
            _toolManager = new ToolManager();
        }

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
                    await HandleInitialize(id);
                    break;

                case "tools/list":
                    await HandleToolsList(id);
                    break;

                case "tools/call":
                    await HandleToolCall(request, id);
                    break;
            }
        }

        private async Task HandleInitialize(JToken id)
        {
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
                        version = "2.2.0" // 最终版本
                    }
                }
            });
        }

        private async Task HandleToolsList(JToken id)
        {
            var tools = _toolManager.ListTools().Select(t => new { name = t.Name, description = t.Description, inputSchema = t.InputSchema });
            await SendResponse(new
            {
                jsonrpc = "2.0",
                id = id,
                result = new { tools = tools }
            });
        }

        private async Task HandleToolCall(JObject request, JToken id)
        {
            var toolName = request["params"]?["name"]?.ToString();
            var arguments = request["params"]?["arguments"] as JObject;

            try
            {
                var result = await _toolManager.CallToolAsync(toolName, arguments);
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
