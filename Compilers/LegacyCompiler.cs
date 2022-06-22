using log4net;
using ModLoader.Data;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModLoader.Compilers
{
    public class LegacyCompiler : CompilerBase
    {
        private static ILog log = LogManager.GetLogger("LegacyCompiler");
        private ModLoaderConfig configuration;
        private CodeDomProvider csharpProvider;
        private CompilerParameters compilerParameters;

        public bool Init(ModLoaderConfig config, List<string> assemblies)
        {
            configuration = config;

            try
            {
                compilerParameters = new CompilerParameters();
                csharpProvider = CodeDomProvider.CreateProvider("CSharp");

                //Setup variables
                compilerParameters.GenerateInMemory = false;
                compilerParameters.GenerateExecutable = false;
                compilerParameters.ReferencedAssemblies.Add(typeof(ModLoader).Assembly.Location);
                compilerParameters.ReferencedAssemblies.AddRange(assemblies.ToArray());
                compilerParameters.IncludeDebugInformation = config.Debug.Compilation;
                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }

        public bool Compile(string path, string cachePath, ModData mod, ref byte[] assemblyData)
        {
            try
            {
                string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);

                compilerParameters.OutputAssembly = Path.Combine(cachePath, mod.ID, $"{mod.ID}.dll");
                CompilerResults result = csharpProvider.CompileAssemblyFromFile(compilerParameters, files);

                int errorCount = 0, warningCount = 0;
                if (result.Errors.HasErrors)
                {
                    foreach (CompilerError error in result.Errors)
                    {
                        if (error.IsWarning)
                        {
                            if (configuration.Debug.Compilation)
                            {
                                log.WarnFormat("{0}:{1} Column {2}", error.FileName, error.Line, error.Column);
                                log.WarnFormat("Error {0}: {1}", error.ErrorNumber, error.ErrorText);
                            }
                            warningCount++;
                        }
                        else
                        {
                            log.ErrorFormat("{0}:{1} Column {2}", error.FileName, error.Line, error.Column);
                            log.ErrorFormat("Error {0}: {1}", error.ErrorNumber, error.ErrorText);
                            errorCount++;
                        }
                    }
                }

                if (errorCount > 0)
                    return false;

                assemblyData = File.ReadAllBytes(compilerParameters.OutputAssembly);
                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }
    }
}
