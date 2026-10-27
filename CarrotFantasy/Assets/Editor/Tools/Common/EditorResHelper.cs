using System.Collections.Generic;
using System.IO;

namespace CarrotFantasy.EditorTools
{
    public static class EditorResHelper
    {
        /// <summary>
        /// 获取文件夹内所有的预制跟场景路径
        /// </summary>
        public static List<string> GetPrefabsAndScenes(string srcPath)
        {
            var paths = new List<string>();
            CollectAllFiles(paths, srcPath);

            var files = new List<string>();
            foreach (string str in paths)
            {
                if (str.EndsWith(".prefab") || str.EndsWith(".unity"))
                {
                    files.Add(str);
                }
            }

            return files;
        }

        /// <summary>
        /// 获取文件夹内所有资源路径（可选递归）
        /// </summary>
        public static List<string> GetAllResourcePath(string srcPath, bool subDire)
        {
            var paths = new List<string>();
            string[] files = Directory.GetFiles(srcPath);
            foreach (string str in files)
            {
                if (str.EndsWith(".meta"))
                {
                    continue;
                }

                paths.Add(str);
            }

            if (subDire)
            {
                foreach (string subPath in Directory.GetDirectories(srcPath))
                {
                    paths.AddRange(GetAllResourcePath(subPath, true));
                }
            }

            return paths;
        }

        private static void CollectAllFiles(List<string> results, string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(directory))
            {
                results.Add(file);
            }

            foreach (string sub in Directory.GetDirectories(directory))
            {
                CollectAllFiles(results, sub);
            }
        }
    }
}
