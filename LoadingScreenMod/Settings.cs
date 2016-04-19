using System;
using System.IO;
using System.Xml.Serialization;
using ColossalFramework.UI;
using ICities;

namespace LoadingScreenMod
{
    public class Settings
    {
        const string FILENAME = "LoadingScreenMod.xml";

        public int version = 2;
        public bool loadEnabled = true;
        public bool loadUsed = true;
        public bool shareTextures = true;
        public bool shareMaterials = true;
        public bool shareMeshes = true;
        public bool reportAssets = false;

        static Settings singleton;

        public static Settings settings
        {
            get
            {
                if (singleton == null)
                    singleton = Load();

                return singleton;
            }
        }

        Settings() { }

        static Settings Load()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                Settings s;

                using (StreamReader reader = new StreamReader(FILENAME))
                    s = (Settings) serializer.Deserialize(reader);

                s.version = 2;
                return s;
            }
            catch (Exception) { }

            return new Settings();
        }

        public void Save()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Settings));

                using (StreamWriter writer = new StreamWriter(FILENAME))
                    serializer.Serialize(writer, this);
            }
            catch (Exception e)
            {
                Util.DebugPrint("Settings.Save");
                UnityEngine.Debug.LogException(e);
            }
        }

        internal void OnSettingsUI(UIHelperBase helper)
        {
            UIHelper group1 = helper.AddGroup("Loading options for custom assets") as UIHelper;
            Check(group1, "Load enabled assets", "Load the assets enabled in Content Manager", loadEnabled, b => { loadEnabled = b; Save(); });
            Check(group1, "Load used assets", "Load the assets you have placed in your city", loadUsed, b => { loadUsed = b; Save(); });
            Check(group1, "Share textures", "Replace exact duplicates by references", shareTextures, b => { shareTextures = b; Save(); });
            Check(group1, "Share materials", "Replace exact duplicates by references", shareMaterials, b => { shareMaterials = b; Save(); });
            Check(group1, "Share meshes", "Replace exact duplicates by references", shareMeshes, b => { shareMeshes = b; Save(); });

            UIHelper group2 = helper.AddGroup("Reports") as UIHelper;
            Check(group2, "Save assets report", "Save a report of missing, failed and used assets in " + Util.GetSavePath(), reportAssets, b => { reportAssets = b; Save(); });

            UIComponent panel = group1?.self as UIComponent;
            UILabel groupLabel = panel?.parent?.Find<UILabel>("Label");

            if (groupLabel != null)
                groupLabel.tooltip = "Custom means workshop assets and assets created by yourself";
        }

        void Check(UIHelper group, string text, string tooltip, bool enabled, OnCheckChanged action)
        {
            try
            {
                UIComponent check = group.AddCheckbox(text, enabled, action) as UIComponent;
                check.tooltip = tooltip;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }
    }
}
