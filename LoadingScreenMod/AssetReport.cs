using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework.Packaging;

namespace LoadingScreenMod
{
    internal sealed class AssetReport
    {
        internal static AssetReport instance;
        List<string> failed = new List<string>(), notFound = new List<string>();

        internal AssetReport()
        {
            instance = this;
        }

        internal void Dispose()
        {
            failed.Clear(); notFound.Clear();
            instance = null; failed = null; notFound = null;
        }

        internal void Failed(string name) => failed.Add(name);
        internal void NotFound(string name) => notFound.Add(name);

        internal void NotFound(string name, Package.Asset referencedBy)
        {
            try
            {
                string msg = string.Concat(name, "\t#referenced by ", referencedBy.fullName);

                if (IsWorkshopPackage(referencedBy.package) && !IsWorkshopPackage(name))
                    msg = string.Concat(msg, ", which looks like an asset bug (workshop asset references private asset)");

                NotFound(msg);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                NotFound(name);
            }
        }

        internal void Save()
        {
            try
            {
                StringBuilder b = new StringBuilder(4096);
                b.AppendLine("#" + LevelLoader.instance.cityName);
                b.AppendLine("#To stop saving these files, disable the option \"Save assets report\" in Loading Screen Mod.");
                b.AppendLine("#You can safely delete this file. No-one reads it except you.");

                Save("Assets that failed to load", failed, b);
                Save("Assets that were not found", notFound, b);

                if (Settings.settings.loadUsed)
                {
                    b.AppendLine();
                    b.AppendLine("#The following custom assets were used in this city when it was saved:");

                    UsedAssets refs = AssetLoader.instance.refs;
                    Save("Buildings", new List<string>(refs.Buildings), b);
                    Save("Props", new List<string>(refs.Props), b);
                    Save("Trees", new List<string>(refs.Trees), b);
                    Save("Vehicles", new List<string>(refs.Vehicles), b);
                }
                else
                {
                    b.AppendLine();
                    b.AppendLine("#To also list the custom assets used in this city, enable the option \"Load used assets\" in Loading Screen Mod.");
                }

                Util.SaveFile(AssetLoader.AssetName(LevelLoader.instance.cityName) + "-AssetsReport", b);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        void Save(string heading, List<string> lines, StringBuilder b)
        {
            lines.Sort();
            b.AppendLine();
            b.AppendLine("#" + heading);

            foreach (var s in lines)
                b.AppendLine(s);
        }

        bool IsWorkshopPackage(Package package)
        {
            ulong value;
            return ulong.TryParse(package.packageName, out value) && value > 999999;
        }

        bool IsWorkshopPackage(string fullName)
        {
            int j = fullName.IndexOf('.');

            if (j <= 0 || j >= fullName.Length - 1)
                return false;

            string p = fullName.Substring(0, j);
            ulong value;
            return ulong.TryParse(p, out value) && value > 999999;
        }
    }
}
