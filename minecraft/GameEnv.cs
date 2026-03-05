using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BlockByBlock
{
    public static class GameEnv
    {
        public static Assembly CurrentAssembly => Assembly.GetExecutingAssembly();

        public static Stream? GetResourceAsStream(string path)
        {
            string workingDir = Directory.GetCurrentDirectory();
            string filePath = Path.GetFullPath(workingDir + "/" + path).Replace("\\", "/").Replace("\\\\", "/");

            Stream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream;
        }
    }
}
