using System;
using System.Text;
using System.Threading.Tasks;

namespace UnityMcpServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            
            var server = new McpServer.McpServer();
            await server.Start();
        }
    }
}