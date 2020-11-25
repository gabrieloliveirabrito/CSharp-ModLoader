using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using HarmonyLib;
using System.Reflection;
using ModLoader.Data;
using System.Threading;

namespace ModLoader
{
    public abstract class ModBase
    {
        public const BindingFlags PrivateStaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        public const BindingFlags PublicStaticFlags = BindingFlags.Public | BindingFlags.Static;
        public const BindingFlags PrivateInstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        public const BindingFlags PublicInstanceFlags = BindingFlags.Public | BindingFlags.Instance;

        private static ILog log;
        protected static ILog Log
        {
            get
            {
                if (log == null)
                    log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
                return log;
            }
        }

        protected internal Assembly Assembly { get; internal set; }
        protected internal Harmony Harmony { get; internal set; }
        protected internal ModLoaderConfig Configuration { get; internal set; }
        private Dictionary<string, Timer> timers;

        public ModData ModData { get; internal set; }

        public abstract bool Init();
        public virtual bool Stop()
        {
            if (timers != null)
            {
                foreach (var timer in timers.Values)
                {
                    timer.Change(-1, -1);
                    timer.Dispose();
                }
                timers.Clear();
            }
            return true;
        }

        public bool Inject(MethodInfo original, MethodInfo prefix = null, MethodInfo postfix = null)
        {
            try
            {
                if(original == null)
                {
                    Log.Error("Nenhum método para realizar o patch foi informado!");
                    return false;
                }
                if(prefix == null && postfix == null)
                {
                    Log.Error("Nenhum método prefixo ou sufixo foi informado!");
                    return false;
                }

                if (prefix != null && !prefix.IsStatic)
                {
                    Log.Error("O patch de prefixo não é um método estático!");
                    return false;
                }

                if (postfix != null && !postfix.IsStatic)
                {
                    Log.Error("O patch de sufixo não é um método estático!");
                    return false;
                }

                if (Configuration.Debug.Inject)
                {
                    if (prefix != null && postfix != null)
                        Log.InfoFormat("Injecting prefix {0} and postfix {1} to target {2}", GetClassMethodName(prefix), GetClassMethodName(postfix), GetClassMethodName(original));
                    else if (prefix != null)
                        Log.InfoFormat("Injecting prefix {0} to target {1}", GetClassMethodName(prefix), GetClassMethodName(original));
                    else
                        Log.InfoFormat("Injecting postfix {0} to target {1}", GetClassMethodName(postfix), GetClassMethodName(original));
                }
                HarmonyMethod harmonyPrefix = prefix == null ? null : new HarmonyMethod(prefix);
                HarmonyMethod harmonyPostfix = postfix == null ? null : new HarmonyMethod(postfix);

                Harmony.Patch(original, harmonyPrefix, harmonyPostfix);
                return true;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
                return false;
            }
        }

        #region Mod Tools
        public void CreateTimer(string id, int interval, Action<object> callBack, object state = null)
        {
            if (timers == null)
                timers = new Dictionary<string, Timer>();
            else if (timers.ContainsKey(id))
                throw new Exception("The timer already exists!");

            if (Configuration.Debug.Timer)
                log.InfoFormat("Creating Timer {0} for mod {1}", id, ModData.Name);

            var timer = new Timer(new TimerCallback(s => TimerCallback(callBack, s)), state, interval, interval);
            timers.Add(id, timer);
        }

        private void TimerCallback(Action<object> callback, object state)
        {
            if (ModLoader.Loaded && ModLoader.AllowTimers)
                callback(state);
        }

        public Timer GetTimer(string id)
        {
            Timer timer;
            if (timers.TryGetValue(id, out timer))
                return timer;
            else
                return null;
        }
        #endregion

        #region Reflection Tools
        public static string GetClassMethodName(MethodInfo method)
        {
            return $"{method.DeclaringType.Name}.{method.Name}";
        }

        /// <summary>
        /// Search for assembly name in CurrentDomain and retrieve type with full name
        /// if searchTypes = true, search by single type name on all assembly types
        /// </summary>
        /// <param name="assemblyName">Single name of assembly</param>
        /// <param name="typeName">Full name of assembly, single if searchTypes = true</param>
        /// <param name="searchTypes">True = search for single type name, False = Get type with namespace + type name</param>
        /// <returns></returns>
        public static Type GetDomainType(string assemblyName, string typeName, bool searchTypes = false)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null) return null;

            if (searchTypes)
                return assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
            else
                return assembly.GetType(typeName);
        }

        public static MethodInfo GetTypeMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            return AccessTools.Method(type, methodName, parameterTypes);
        }

        public static MethodInfo GetTypeMethod(Type type, string methodName, Type[] genericTypes = null, params Type[] parameterTypes)
        {
            return AccessTools.Method(type, methodName, parameterTypes, genericTypes);
        }

        public static MethodInfo GetTypeMethod<T>(string methodName, params Type[] parameterTypes)
        {
            return GetTypeMethod(typeof(T), methodName, parameterTypes);
        }

        public static MethodInfo GetTypeMethod<T>(string methodName, Type[] genericTypes = null, params Type[] parameterTypes)
        {
            return GetTypeMethod(typeof(T), methodName, parameterTypes, genericTypes);
        }

        public MethodInfo GetModMethod(string methodName, Type[] genericTypes = null, params Type[] parameterTypes)
        {
            return GetTypeMethod(GetType(), methodName, parameterTypes, genericTypes);
        }

        public MethodInfo GetModMethod(string methodName, params Type[] parameterTypes)
        {
            return GetTypeMethod(GetType(), methodName, parameterTypes);
        }

        public void InvokeMethod(MethodInfo method, object target, params object[] parameters)
        {
            method.Invoke(target, parameters);
        }

        public TReturn InvokeMethod<TReturn>(MethodInfo method, object target, params object[] parameters)
        {
            object result = method.Invoke(target, parameters);
            return (TReturn)result;
        }
        #endregion

        #region Types Extension
        public static Type[] Types<T1, T2>()
        {
            return new[] { typeof(T1), typeof(T2) };
        }

        public static Type[] Types<T1, T2, T3>()
        {
            return new[] { typeof(T1), typeof(T2), typeof(T3) };
        }

        public static Type[] Types<T1, T2, T3, T4>()
        {
            return new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };
        }

        public static Type[] Types<T1, T2, T3, T4, T5>()
        {
            return new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) };
        }
        #endregion
    }
}