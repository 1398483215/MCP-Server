
using System;
using System.IO;

namespace UnityMcpServer.Utils
{
    public static class UnityProjectFinder
    {
        public static string GetUnityProjectPath()
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
            // 向上搜索目录树以查找项目根目录(包含Assets文件夹)
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }
    
            if (dir == null)
            {
                // 如果找不到，则必须抛出异常，因为无法继续。
                throw new DirectoryNotFoundException($"Could not find the Unity project root. Environment variable UNITY_PROJECT_PATH: '{envPath}', Current directory: '{currentDir}'");
            }
    
            return dir.FullName;
        }
    }
}
