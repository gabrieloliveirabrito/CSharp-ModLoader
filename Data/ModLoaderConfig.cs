using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModLoader.Data
{
    public class ModLoaderConfig
    {
        public bool Debug { get; set; }
        public bool UseCache { get; set; }
        public string ModsDirectory { get; set; }
        public List<AssemblyData> Assemblies = new List<AssemblyData>();
    }
}