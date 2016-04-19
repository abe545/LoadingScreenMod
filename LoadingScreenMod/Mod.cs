using ICities;
using ColossalFramework;

namespace LoadingScreenMod
{
    public sealed class Mod : IUserMod, ILoadingExtension
    {
        static bool created = false;
        public string Name => "Loading Screen Mod";
        public string Description => "with new loading options for custom assets";

        public void OnEnabled() => Create();
        public void OnCreated(ILoading loading) => Create();
        public void OnDisabled() => Stopping();
        public void OnSettingsUI(UIHelperBase helper) => Settings.settings.OnSettingsUI(helper);
        public void OnLevelUnloading() { }
        public void OnReleased() { }

        public void OnLevelLoaded(LoadMode mode)
        {
            if (LevelLoader.instance.activated)
            {
                if (Settings.settings.reportAssets)
                    AssetReport.instance.Save();

                Singleton<LoadingManager>.instance.LoadingAnimationComponent.enabled = false;
            }
        }

        void Create()
        {
            if (!created)
            {
                Stopping();
                new LevelLoader().Deploy();
                created = true;
            }
        }

        void Stopping()
        {
            LevelLoader.instance?.Dispose();
            created = false;
        }
    }
}
