
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UnityMcpServer.Tools
{
    public class ToolManager
    {
        private readonly Dictionary<string, ITool> _tools;

        public ToolManager()
        {
            _tools = new Dictionary<string, ITool>();
            RegisterTools();
        }

        private void RegisterTools()
        {
            var toolTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(ITool)) && t.GetConstructor(Type.EmptyTypes) != null);

            foreach (var type in toolTypes)
            {
                var tool = (ITool)Activator.CreateInstance(type);
                _tools[tool.Name] = tool;
            }
        }

        public IEnumerable<ITool> ListTools()
        {
            return _tools.Values;
        }

        public async Task<object> CallToolAsync(string toolName, JObject args)
        {
            if (_tools.TryGetValue(toolName, out var tool))
            {
                return await tool.ExecuteAsync(args);
            }
            throw new Exception($"未知工具: {toolName}");
        }
    }
}
