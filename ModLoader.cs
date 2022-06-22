using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CSharp;
using log4net;
using Newtonsoft.Json;
using HarmonyLib;


namespace ModLoader
{
    using Data;
    using Enums;
    using Compilers;

    public class ModLoader
    {
        private static ILog log = null;
        private static ModLoaderConfig configuration = null;
        private static List<string> assemblies = null;
        private static Dictionary<string, CompilerBase> compilers = new Dictionary<string, CompilerBase>();
        private static Dictionary<string, ModData> modDatas = null;
        private static Dictionary<string, ModBase> mods = null;
        private static CompilerBase compiler = null;
        private static string modsCacheDirectory = null;
        private static int warningCount = 0;

        public static bool Loaded { get; private set; }
        public static bool AllowTimers { get; set; } = true;

        public static bool RegisterCompiler<TCompiler>(string name)
            where TCompiler : CompilerBase
        {
            if (compilers.ContainsKey(name))
                return false;

            var compiler = Activator.CreateInstance<TCompiler>();
            if (compiler == null)
                return false;

            compilers[name] = compiler;
            return true;
        }

        private static bool FetchCompiler()
        {
            return compilers.TryGetValue(configuration.Compiler, out compiler);
        }

        public static bool Init()
        {
            var start = DateTime.Now;
            try
            {
                //Init variables
                log = LogManager.GetLogger("ModLoader");
                compilers = new Dictionary<string, CompilerBase>();
                modDatas = new Dictionary<string, ModData>();
                mods = new Dictionary<string, ModBase>();
                assemblies = new List<string>();

                RegisterCompiler<LegacyCompiler>("Legacy");
                RegisterCompiler<RoslynCompiler>("Roslyn");

                if (!InitPartial("ModLoader Configuration", LoadConfig))
                    return false;
                else if (!InitPartial("Compiler Set", FetchCompiler))
                    return false;
                else if (!InitPartial("ModLoader Compiler", () => compiler.Init(configuration, assemblies)))
                    return false;
                
                //Harmony.DEBUG = configuration.Debug;

                string modsDirectory = configuration.ModsDirectory;

                if (!Directory.Exists(modsDirectory))
                {
                    log.Warn("ModsDirectory path dont exists, using default path...");
                    modsDirectory = Path.Combine(Environment.CurrentDirectory, "Mods");
                }
                else if (modsDirectory == null)
                    modsDirectory = Path.Combine(Environment.CurrentDirectory, "Mods");

                modsDirectory = Path.GetFullPath(modsDirectory);
                if (configuration.Debug.Init)
                    log.InfoFormat("Loading mods from: {0}", modsDirectory);

                if (!Directory.Exists(modsDirectory))
                    Directory.CreateDirectory(modsDirectory);

                modsCacheDirectory = Path.Combine(Environment.CurrentDirectory, "ModsCache");
                if (!Directory.Exists(modsCacheDirectory))
                {
                    Directory.CreateDirectory(modsCacheDirectory);
                }
                else if (!configuration.UseCache)
                {
                    log.WarnFormat("UseCache = false, deleting the mods cache...");
                    Directory.Delete(modsCacheDirectory, true);
                    Directory.CreateDirectory(modsCacheDirectory);
                }

                warningCount = 0;
                foreach (string modDirectory in Directory.GetDirectories(modsDirectory, "*.*", SearchOption.AllDirectories))
                {
                    string dataPath = Path.Combine(modDirectory, "mod.json");

                    if (File.Exists(dataPath))
                    {
                        string modData = File.ReadAllText(dataPath);
                        ModData data = JsonConvert.DeserializeObject<ModData>(modData);
                        if (data.Name == null)
                            data.Name = data.ID;

                        modDatas.Add(data.ID, data);

                        string modPath = Path.Combine(modsDirectory, modDirectory, data.Filename);
                        if (!File.Exists(modPath))
                        {
                            log.ErrorFormat("Mod {0} file not found!", modPath);
                            return false;
                        }

                        byte[] assemblyData = null;
                        if (data.Type == ModType.Script)
                        {
                            if (!LoadFromCache(modPath, data, ref assemblyData))
                            {
                                if (configuration.Debug.Cache)
                                    log.InfoFormat("Cache of mod {0} not exists or mod updated, compiling scripts..", data.ID);

                                if (!CompileScript(modDirectory, data, ref assemblyData))
                                {
                                    log.ErrorFormat("Failed to compile mod script {0}", data.Name);
                                    return false;
                                }
                            }
                            else if (configuration.Debug.Cache)
                            {
                                log.InfoFormat("Loading mod {0} from cache..", data.ID);
                            }
                        }
                        else if (data.Type == ModType.DLL)
                        {
                            if (configuration.Debug.Cache)
                                log.InfoFormat("Loading mod {0} from compiled assembly..", data.ID);
                            assemblyData = File.ReadAllBytes(modPath);
                        }

                        Assembly modAssembly = AppDomain.CurrentDomain.Load(assemblyData);
                        Type mainType = modAssembly.GetTypes().FirstOrDefault(t => t.Name == data.MainClass);

                        if(mainType == null)
                        {
                            log.ErrorFormat("Mod {0} MainClass {1} don't exists!", data.Name, data.MainClass);
                            return false;
                        }
                        else if (mainType.BaseType != typeof(ModBase))
                        {
                            log.ErrorFormat("Mod {0} MainClass {1} not extends from type ModBase!", data.Name, data.MainClass);
                            return false;
                        }

                        ModBase mod = Activator.CreateInstance(mainType) as ModBase;
                        mod.Harmony = new Harmony(data.ID);
                        mod.ModData = data;
                        mod.Configuration = configuration;
                        mod.Assembly = modAssembly;

                        if (!InitPartial(data.Name, mod.Init))
                        {
                            log.ErrorFormat("Failed to load mod {0}!", data.Name);
                            return false;
                        }

                        mods.Add(data.ID, mod);
                    }
                }

                string[] cacheMods = Directory.GetDirectories(modsCacheDirectory, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string cacheModPath in cacheMods)
                {
                    string modID = Path.GetFileName(cacheModPath);
                    if (!mods.ContainsKey(modID))
                    {
                        log.WarnFormat("Deleting {0} cache of removed mod!", modID);
                        Directory.Delete(cacheModPath, true);
                    }
                }

                GC.Collect(GC.MaxGeneration);

                var end = DateTime.Now;
                var elapsed = (end - start).TotalMilliseconds;

                log.InfoFormat("ModLoader loaded {0} mods, with {1}ms and {2} warnings", mods.Count, elapsed, warningCount);

                Loaded = true;
                return true;
            }
            catch (Exception ex)
            {
                log.Fatal(ex);
                return false;
            }
        }

        public static bool Stop()
        {
            try
            {
                log.Warn("Unloading all mods...");
                foreach (ModBase mod in mods.Values)
                {
                    if (!mod.Stop())
                        return false;

                    mod.Harmony.UnpatchAll();
                }

                mods.Clear();
                Loaded = false;
                return true;
            }
            catch (Exception ex)
            {
                log.Fatal(ex);
                return false;
            }
        }

        public static bool Reload()
        {
            return InitPartial("Reloading ModLoader", () => Stop() && Init());
        }

        private static bool InitPartial(string desc, Func<bool> partial)
        {
            bool result = partial();
            if (result)
                log.InfoFormat("{0}: {1}", desc, result);
            else
                log.ErrorFormat("{0}: {1}", desc, result);
            return result;
        }

        private static void CreateConfig(string path)
        {
            try
            {
                var config = new ModLoaderConfig();
                config.UseCache = false;
                config.Debug = new DebugConfig() { "None" };
                config.ModsDirectory = "./mods/";
                config.Assemblies = new AssemblyData();

                config.Assemblies.System.AddRange(new[] { "System.dll", "System.Linq.dll", "System.Configuration.dll" });
                config.Assemblies.Local.AddRange(new[] { "0Harmony.dll", "Newtonsoft.Json.dll", "log4net.dll" });

                var json = JsonConvert.SerializeObject(config);
                if (File.Exists(path)) File.Delete(path);

                using (var writer = File.CreateText(path))
                    writer.Write(json);
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }
        }

        private static bool LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(Environment.CurrentDirectory, "modloader.json");
                if(!File.Exists(configPath))
                {
                    CreateConfig(configPath);
                    return false;
                }

                string jsonData = File.ReadAllText(configPath);
                configuration = JsonConvert.DeserializeObject<ModLoaderConfig>(jsonData);

                assemblies.AddRange(configuration.Assemblies.System);
                foreach (string name in configuration.Assemblies.Local)
                {
                    string assemblyPath = "";
                    if (!configuration.Assemblies.GetFullPath(name, ref assemblyPath))
                    {
                        log.ErrorFormat("Failed to load assembly {0}!", name);
                        return false;
                    }

                    assemblies.Add(assemblyPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Fatal(ex);
                return false;
            }
        }

        private static bool LoadFromCache(string modPath, ModData data, ref byte[] assemblyData)
        {
            string cachePath = Path.Combine(modsCacheDirectory, data.ID);
            if (!Directory.Exists(cachePath))
                Directory.CreateDirectory(cachePath);

            string cacheChecksum = "";
            string checksumPath = Path.Combine(cachePath, "checksum.hash");
            if (File.Exists(checksumPath))
                cacheChecksum = File.ReadAllText(checksumPath);

            List<byte> scriptData = new List<byte>();

            string[] files = Directory.GetFiles(Path.GetDirectoryName(modPath), "*.*", SearchOption.AllDirectories);
            foreach (string filename in files)
            {
                scriptData.AddRange(File.ReadAllBytes(filename));
            }

            using (var md5 = MD5.Create())
            {
                string checksum = BitConverter.ToString(md5.ComputeHash(scriptData.ToArray())).Replace("-", "");
                if (cacheChecksum != checksum)
                {
                    File.Delete(checksumPath);
                    using (var stream = File.CreateText(checksumPath))
                    {
                        stream.Write(checksum);
                    }

                    return false;
                }
            }

            string assemblyPath = Path.Combine(cachePath, $"{data.ID}.dll");
            if (!File.Exists(assemblyPath))
                return false;
            else if (!File.Exists(Path.ChangeExtension(assemblyPath, "pdb")))
                return false;

            assemblyData = File.ReadAllBytes(assemblyPath);
            return true;
        }

        private static bool CompileScript(string modPath, ModData data, ref byte[] modAssemblyData)
        {
           return compiler.Compile(modPath, modsCacheDirectory, data, ref modAssemblyData);
        }
    }
}