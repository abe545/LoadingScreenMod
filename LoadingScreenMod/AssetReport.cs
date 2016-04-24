using System;
using System.Collections.Generic;
using System.IO;
using ColossalFramework.Packaging;

namespace LoadingScreenMod
{
    internal sealed class AssetReport
    {
        internal static AssetReport instance;
        List<string> failed = new List<string>();
        Dictionary<string, HashSet<Package.Asset>> notFound = new Dictionary<string, HashSet<Package.Asset>>();
        StreamWriter w;
        const string steamid = @"<a href=""https://steamcommunity.com/sharedfiles/filedetails/?id=";

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

        internal void NotFound(string name)
        {
            if (!notFound.ContainsKey(name))
                notFound[name] = null;
        }

        internal void NotFound(string name, Package.Asset referencedBy)
        {
            HashSet<Package.Asset> set;

            if (notFound.TryGetValue(name, out set) && set != null)
                set.Add(referencedBy);
            else
            {
                set = new HashSet<Package.Asset>();
                set.Add(referencedBy);
                notFound[name] = set;
            }
        }

        internal void Save()
        {
            try
            {
                w = new StreamWriter(Util.GetFileName(AssetLoader.AssetName(LevelLoader.instance.cityName) + "-AssetsReport", "htm"));
                w.WriteLine(@"<!DOCTYPE html><html><head><meta charset=""UTF-8""><title>Assets Report</title><style>");
                w.WriteLine(@"* {font-family: sans-serif;}");
                w.WriteLine(@".my {display: -webkit-flex; display: flex;}");
                w.WriteLine(@".my div {min-width: 30%; margin: 4px 4px 4px 20px;}");
                w.WriteLine(@"</style></head><body>");

                H1(AssetLoader.AssetName(LevelLoader.instance.cityName));
                Para("To stop saving these files, disable the option \"Save assets report\" in Loading Screen Mod.");
                Para("You can safely delete this file. No-one reads it except you.");

                Save("Assets that failed to load", failed);
                Save("Assets that were not found", notFound);

                if (Settings.settings.loadUsed)
                {
                    H1("The following custom assets were used in this city when it was saved");

                    UsedAssets refs = AssetLoader.instance.refs;
                    Save("Buildings", new List<string>(refs.Buildings));
                    Save("Props", new List<string>(refs.Props));
                    Save("Trees", new List<string>(refs.Trees));
                    Save("Vehicles", new List<string>(refs.Vehicles));
                }
                else
                {
                    H1("Used assets");
                    Para("To also list the custom assets used in this city, enable the option \"Load used assets\" in Loading Screen Mod.");
                }

                w.WriteLine(@"</body></html>");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
            finally
            {
                w?.Dispose();
                w = null;
            }
        }

        void Save(string heading, List<string> lines)
        {
            H2(heading);
            lines.Sort();

            foreach (var s in lines)
                Para(Ref(s));
        }

        void Save(string heading, Dictionary<string, HashSet<Package.Asset>> lines)
        {
            H2(heading);
            List<string> keys = new List<string>(lines.Keys);
            keys.Sort();

            foreach (var key in keys)
            {
                HashSet<Package.Asset> set = lines[key];
                string refkey = Ref(key);

                if (set == null)
                    Para(refkey);
                else
                {
                    string s = string.Concat(refkey, "</div><div>Required in:");
                    ulong id;
                    bool fromWorkshop = false;

                    foreach(Package.Asset asset in set)
                    {
                        s = string.Concat(s, " ", Ref(asset));
                        fromWorkshop = fromWorkshop || AssetLoader.IsWorkshopPackage(asset.package, out id);
                    }

                    if (fromWorkshop && !AssetLoader.IsWorkshopPackage(key, out id))
                    {
                        if (AssetLoader.IsPrivatePackage(key))
                            s = string.Concat(s, " <b>Workshop asset requires private content, seems like asset bug?</b>");
                        else
                            s = string.Concat(s, " <b>Workshop asset requires DLC/Deluxe/Pre-order content?</b>");
                    }

                    Para(s);
                }
            }
        }

        void Para(string line) => w.WriteLine(string.Concat("<div class=\"my\"><div>", line, "</div></div>"));
        void H1(string line) => w.WriteLine(string.Concat("<h1>", line, "</h1>"));
        void H2(string line) => w.WriteLine(string.Concat("<h2>", line, "</h2>"));

        string Ref(Package.Asset asset)
        {
            ulong id;

            if (AssetLoader.IsWorkshopPackage(asset.package, out id))
                return string.Concat(steamid, id.ToString(), "\">", asset.fullName, "</a>");
            else
                return asset.fullName;
        }

        string Ref(string fullName)
        {
            ulong id;

            if (AssetLoader.IsWorkshopPackage(fullName, out id))
                return string.Concat(steamid, id.ToString(), "\">", fullName, "</a>");
            else
                return fullName;
        }
    }
}
