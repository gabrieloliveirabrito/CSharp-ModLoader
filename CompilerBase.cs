using ModLoader.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModLoader
{
    public interface CompilerBase
    {
        bool Init(ModLoaderConfig config, List<string> assemblies);
        bool Compile(string path, string cachePath, ModData mod, ref byte[] assemblyData);
    }
}
