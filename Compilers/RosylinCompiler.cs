using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;

namespace ModLoader.Compilers
{
    using Data;
    using System.CodeDom.Compiler;
    using System.IO;

    public class RoslynCompiler : CompilerBase
    {
        private static ILog log = LogManager.GetLogger("RoslynCompiler");
        private CSharpCodeProvider provider;
        private CompilerParameters compilerParameters;
        private ModLoaderConfig configuration;

        public bool Init(ModLoaderConfig config, List<string> assemblies)
        {
            try
            {
                configuration = config;

                var roslynPath = Path.Combine(Environment.CurrentDirectory, "roslyn", "csc.exe");
                provider = new CSharpCodeProvider(new ProviderOptions(roslynPath, 600));

                //Setup variables
                compilerParameters = new CompilerParameters();
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

                var result = provider.CompileAssemblyFromFile(compilerParameters, files);
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

                assemblyData = File.ReadAllBytes(result.PathToAssembly);
                return true;
            }
            catch(Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }
    }
}
