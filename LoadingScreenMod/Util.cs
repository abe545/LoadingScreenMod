using System;
using UnityEngine;
using System.Linq;
using System.Reflection;
using ColossalFramework.IO;
using System.IO;

namespace LoadingScreenMod
{
    public static class Util
    {
        public static void DebugPrint(params object[] args)
        {
            string s = string.Format("[LoadingScreen] {0}", " ".OnJoin(args));
            Debug.Log(s);
        }

        public static string OnJoin(this string delim, params object[] args)
        {
            return string.Join(delim, args.Select(o => o?.ToString() ?? "null").ToArray());
        }

        internal static void InvokeVoid(object instance, string method)
        {
            instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
        }

        internal static object Invoke(object instance, string method)
        {
            return instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
        }

        internal static void InvokeVoid(object instance, string method, params object[] args)
        {
            instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
        }

        internal static object Invoke(object instance, string method, params object[] args)
        {
            return instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
        }

        internal static void InvokeStaticVoid(Type type, string method, params object[] args)
        {
            type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        }

        internal static object InvokeStatic(Type type, string method, params object[] args)
        {
            return type.GetMethod(method, BindingFlags.Static| BindingFlags.NonPublic).Invoke(null, args);
        }

        internal static object Get(object instance, string field)
        {
            return instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        }

        internal static object GetStatic(Type type, string field)
        {
            return type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        }

        internal static void Set(object instance, string field, object value)
        {
            instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
        }

        internal static string GetFileName(string fileBody, string extension)
        {
            string name = fileBody + string.Format("-{0:yyyy-MM-dd_HH-mm-ss}." + extension, DateTime.Now);
            return Path.Combine(GetSavePath(), name);
        }

        internal static string GetSavePath()
        {
            try
            {
                string modConfigDir = Path.Combine(DataLocation.localApplicationData, "Report");
                string modDir = Path.Combine(modConfigDir, "LoadingScreenMod");

                if (!Directory.Exists(modDir))
                    Directory.CreateDirectory(modDir);

                return modDir;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return String.Empty;
            }
        }
    }
}
