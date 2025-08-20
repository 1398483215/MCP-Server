
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UnityMcpServer.Tools
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        object InputSchema { get; }
        Task<object> ExecuteAsync(JObject args);
    }
}
