using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModLoader.Data
{
    public class ModLoaderConfig
    {
        public DebugConfig Debug { get; set; }
        public bool UseCache { get; set; }
        public string Compiler { get; set; }
        public string ModsDirectory { get; set; }
        public AssemblyData Assemblies = new AssemblyData();
    }
}