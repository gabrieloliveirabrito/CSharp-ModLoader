using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ModLoader.Data
{
    public class AssemblyData
    {
        public string Local { get; set; }
        public string System { get; set; }

        public bool GetFullPath(ref string fullPath)
        {
            if (System != null)
            {
                fullPath = System;
                return true;
            }

            fullPath = Path.Combine(Environment.CurrentDirectory, Local);
            return File.Exists(fullPath);
        }
    }
}