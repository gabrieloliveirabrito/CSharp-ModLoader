using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ModLoader.Data
{
    public class AssemblyData
    {
        public List<string> Local { get; set; }
        public List<string> System { get; set; }

        public bool GetFullPath(string name, ref string fullPath)
        {
            fullPath = Path.Combine(Environment.CurrentDirectory, name);
            return File.Exists(fullPath);
        }
    }
}