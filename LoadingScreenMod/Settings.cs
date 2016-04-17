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

        public int version = 1;
        public bool loadEnabled = true;
        public bool loadUsed = true;
        public bool shareTextures = true;
        public bool shareMaterials = true;
        public bool shareMeshes = true;

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

                s.version = 1;
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
            UIHelper group = helper.AddGroup("Loading options for custom assets") as UIHelper;
            Check(group, "Load enabled assets", "Load the assets enabled in Content Manager", loadEnabled, b => { loadEnabled = b; Save(); });
            Check(group, "Load used assets", "Load the assets you have placed in your city", loadUsed, b => { loadUsed = b; Save(); });
            Check(group, "Share textures", "Replace exact duplicates by references", shareTextures, b => { shareTextures = b; Save(); });
            Check(group, "Share materials", "Replace exact duplicates by references", shareMaterials, b => { shareMaterials = b; Save(); });
            Check(group, "Share meshes", "Replace exact duplicates by references", shareMeshes, b => { shareMeshes = b; Save(); });

            UIComponent panel = group?.self as UIComponent;
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
