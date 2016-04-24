using System;
using System.Collections;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Packaging;
using ColossalFramework.Steamworks;
using UnityEngine;

namespace LoadingScreenMod
{
    /// <summary>
    /// LoadCustomContent coroutine from LoadingManager.
    /// </summary>
    public sealed class AssetLoader
    {
        public static AssetLoader instance;
        internal UsedAssets refs;
        HashSet<string> failedAssets = new HashSet<string>();
        Package.Asset loadedAsset;
        SteamHelper.DLC_BitMask notMask;
        int propsCount, treeCount, buildingsCount, vehicleCount, lastMillis;
        readonly bool loadEnabled = Settings.settings.loadEnabled, loadUsed = Settings.settings.loadUsed, reportAssets = Settings.settings.reportAssets;
        public bool hasStarted, hasFinished;
        internal const int yieldInterval = 200;

        public AssetLoader()
        {
            instance = this;
            hasStarted = hasFinished = false;
        }

        internal void Setup()
        {
            if (reportAssets)
                new AssetReport();
        }

        public void Dispose()
        {
            refs?.Dispose();
            AssetReport.instance?.Dispose();
            failedAssets.Clear();
            instance = null; refs = null; failedAssets = null;
        }

        public IEnumerator LoadCustomContent()
        {
            LoadingManager.instance.m_loadingProfilerMain.BeginLoading("LoadCustomContent");
            LoadingManager.instance.m_loadingProfilerCustomContent.Reset();
            LoadingManager.instance.m_loadingProfilerCustomAsset.Reset();
            LoadingManager.instance.m_loadingProfilerCustomContent.BeginLoading("District Styles");
            LoadingManager.instance.m_loadingProfilerCustomAsset.PauseLoading();
            hasStarted = true;

            int i, j;
            DistrictStyle districtStyle;
            DistrictStyleMetaData districtStyleMetaData;
            List<DistrictStyle> districtStyles = new List<DistrictStyle>();
            HashSet<string> styleBuildings = new HashSet<string>();
            FastList<DistrictStyleMetaData> districtStyleMetaDatas = new FastList<DistrictStyleMetaData>();
            FastList<Package> districtStylePackages = new FastList<Package>();
            Package.Asset europeanStyles = PackageManager.FindAssetByName("System." + DistrictStyle.kEuropeanStyleName);

            if (europeanStyles != null && europeanStyles.isEnabled)
            {
                districtStyle = new DistrictStyle(DistrictStyle.kEuropeanStyleName, true);
                Util.InvokeVoid(LoadingManager.instance, "AddChildrenToBuiltinStyle", GameObject.Find("European Style new"), districtStyle, false);
                Util.InvokeVoid(LoadingManager.instance, "AddChildrenToBuiltinStyle", GameObject.Find("European Style others"), districtStyle, true);
                districtStyles.Add(districtStyle);
            }

            foreach(Package.Asset asset in PackageManager.FilterAssets(UserAssetType.DistrictStyleMetaData))
            {
                try
                {
                    if (asset != null && asset.isEnabled)
                    {
                        districtStyleMetaData = asset.Instantiate<DistrictStyleMetaData>();

                        if (districtStyleMetaData != null && !districtStyleMetaData.builtin)
                        {
                            districtStyleMetaDatas.Add(districtStyleMetaData);
                            districtStylePackages.Add(asset.package);

                            if (districtStyleMetaData.assets != null)
                                for (i = 0; i < districtStyleMetaData.assets.Length; i++)
                                    styleBuildings.Add(districtStyleMetaData.assets[i]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    CODebugBase<LogChannel>.Warn(LogChannel.Modding, string.Concat(new object[] {ex.GetType(), ": Loading custom district style failed[", asset, "]\n", ex.Message}));
                }
            }

            LoadingManager.instance.m_loadingProfilerCustomAsset.ContinueLoading();
            LoadingManager.instance.m_loadingProfilerCustomContent.EndLoading();

            if (loadUsed)
            {
                //while (!Profiling.SimulationPaused())
                //    yield return null;

                refs = new UsedAssets();
                refs.Setup();
            }

            LoadingManager.instance.m_loadingProfilerCustomContent.BeginLoading("Loading Assets First Pass");
            notMask = ~SteamHelper.GetOwnedDLCMask();
            lastMillis = Profiling.Millis;

            // Load custom assets: props, trees, trailers
            foreach (Package.Asset asset in PackageManager.FilterAssets(UserAssetType.CustomAssetMetaData))
                if (asset != null && PropTreeTrailer(asset) && Profiling.Millis - lastMillis > yieldInterval)
                {
                    lastMillis = Profiling.Millis;
                    yield return null;
                }

            LoadingManager.instance.m_loadingProfilerCustomContent.EndLoading();
            LoadingManager.instance.m_loadingProfilerCustomContent.BeginLoading("Loading Assets Second Pass");

            // Load custom assets: buildings and vehicles
            foreach (Package.Asset asset in PackageManager.FilterAssets(UserAssetType.CustomAssetMetaData))
                if (asset != null && BuildingVehicle(asset, styleBuildings.Contains(asset.fullName)) && Profiling.Millis - lastMillis > yieldInterval)
                {
                    lastMillis = Profiling.Millis;
                    yield return null;
                }

            if (loadUsed)
                refs.ReportMissingAssets();

            LoadingManager.instance.m_loadingProfilerCustomContent.EndLoading();
            LoadingManager.instance.m_loadingProfilerCustomContent.BeginLoading("Finalizing District Styles");
            LoadingManager.instance.m_loadingProfilerCustomAsset.PauseLoading();

            for (i = 0; i < districtStyleMetaDatas.m_size; i++)
            {
                try
                {
                    districtStyleMetaData = districtStyleMetaDatas.m_buffer[i];
                    districtStyle = new DistrictStyle(districtStyleMetaData.name, false);

                    if (districtStylePackages.m_buffer[i].GetPublishedFileID() != PublishedFileId.invalid)
                        districtStyle.PackageName = districtStylePackages.m_buffer[i].packageName;

                    if (districtStyleMetaData.assets != null)
                    {
                        for(j = 0; j < districtStyleMetaData.assets.Length; j++)
                        {
                            BuildingInfo bi = PrefabCollection<BuildingInfo>.FindLoaded(districtStyleMetaData.assets[j] + "_Data");

                            if (bi != null)
                            {
                                districtStyle.Add(bi);

                                if (districtStyleMetaData.builtin) // this is always false
                                    bi.m_dontSpawnNormally = !districtStyleMetaData.assetRef.isEnabled;
                            }
                            else
                                CODebugBase<LogChannel>.Warn(LogChannel.Modding, "Warning: Missing asset (" + districtStyleMetaData.assets[i] + ") in style " + districtStyleMetaData.name);
                        }

                        districtStyles.Add(districtStyle);
                    }
                }
                catch (Exception ex)
                {
                    CODebugBase<LogChannel>.Warn(LogChannel.Modding, ex.GetType() + ": Loading district style failed\n" + ex.Message);
                }
            }

            Singleton<DistrictManager>.instance.m_Styles = districtStyles.ToArray();

            if (Singleton<BuildingManager>.exists)
                Singleton<BuildingManager>.instance.InitializeStyleArray(districtStyles.Count);

            LoadingManager.instance.m_loadingProfilerCustomAsset.ContinueLoading();
            LoadingManager.instance.m_loadingProfilerCustomContent.EndLoading();

            if (Singleton<TelemetryManager>.exists)
                Singleton<TelemetryManager>.instance.CustomContentInfo(buildingsCount, propsCount, treeCount, vehicleCount);

            LoadingManager.instance.m_loadingProfilerMain.EndLoading();
            hasFinished = true;
        }

        bool PropTreeTrailer(Package.Asset asset)
        {
            CustomAssetMetaData assetMetaData = null;

            try
            {
                bool wantBecauseEnabled = loadEnabled && IsEnabled(asset);

                if (!wantBecauseEnabled && !(loadUsed && refs.GotPropTreeTrailerPackage(asset.package.packageName)))
                    return false;

                assetMetaData = asset.Instantiate<CustomAssetMetaData>();

                if (assetMetaData.type == CustomAssetMetaData.Type.Building || assetMetaData.type == CustomAssetMetaData.Type.Vehicle ||
                    assetMetaData.type == CustomAssetMetaData.Type.Unknown || (AssetImporterAssetTemplate.GetAssetDLCMask(assetMetaData) & notMask) != 0)
                    return false;

                if (wantBecauseEnabled || loadUsed && refs.GotPropTreeAsset(assetMetaData.assetRef.fullName) ||
                    loadUsed && assetMetaData.type == CustomAssetMetaData.Type.Trailer && refs.GotTrailerAsset(assetMetaData.assetRef.fullName))
                    PropTreeTrailerImpl(assetMetaData.assetRef);
            }
            catch (Exception ex)
            {
                Failed(assetMetaData?.assetRef, ex);
                // CODebugBase<LogChannel>.Warn(LogChannel.Modding, string.Concat(new object[] { ex.GetType(), ": Loading custom asset failed[", asset, "]\n", ex.Message }));
            }

            return true;
        }

        internal void PropTreeTrailerImpl(Package.Asset data)
        {
            try
            {
                string name = AssetName(data.name);
                LoadingManager.instance.m_loadingProfilerCustomAsset.BeginLoading(name);
                // CODebugBase<LogChannel>.Log(LogChannel.Modding, string.Concat("Loading custom asset ", assetMetaData.name, " from ", asset));

                GameObject go = data.Instantiate<GameObject>();
                go.name = data.package.packageName + "." + go.name;
                go.SetActive(false);
                PrefabInfo info = go.GetComponent<PrefabInfo>();
                info.m_isCustomContent = true;

                if (info.m_Atlas != null && info.m_InfoTooltipThumbnail != null && info.m_InfoTooltipThumbnail != string.Empty && info.m_Atlas[info.m_InfoTooltipThumbnail] != null)
                    info.m_InfoTooltipAtlas = info.m_Atlas;

                PropInfo pi = go.GetComponent<PropInfo>();

                if (pi != null)
                {
                    if (pi.m_lodObject != null)
                        pi.m_lodObject.SetActive(false);

                    PrefabCollection<PropInfo>.InitializePrefabs("Custom Assets", pi, null);
                    propsCount++;
                }

                TreeInfo ti = go.GetComponent<TreeInfo>();

                if (ti != null)
                {
                    PrefabCollection<TreeInfo>.InitializePrefabs("Custom Assets", ti, null);
                    treeCount++;
                }

                // Trailers, this way.
                VehicleInfo vi = go.GetComponent<VehicleInfo>();

                if (vi != null)
                {
                    PrefabCollection<VehicleInfo>.InitializePrefabs("Custom Assets", vi, null);

                    if (vi.m_lodObject != null)
                        vi.m_lodObject.SetActive(false);
                }
            }
            finally
            {
                LoadingManager.instance.m_loadingProfilerCustomAsset.EndLoading();
            }
        }

        bool BuildingVehicle(Package.Asset asset, bool includedInStyle)
        {
            CustomAssetMetaData assetMetaData = null;

            try
            {
                bool wantBecauseEnabled = loadEnabled && IsEnabled(asset);

                if (!includedInStyle && !wantBecauseEnabled && !(loadUsed && refs.GotBuildingVehiclePackage(asset.package.packageName)))
                    return false;

                assetMetaData = asset.Instantiate<CustomAssetMetaData>();

                if (assetMetaData.type != CustomAssetMetaData.Type.Building && assetMetaData.type != CustomAssetMetaData.Type.Vehicle &&
                    assetMetaData.type != CustomAssetMetaData.Type.Unknown || (AssetImporterAssetTemplate.GetAssetDLCMask(assetMetaData) & notMask) != 0)
                    return false;

                bool wanted = wantBecauseEnabled || loadUsed && refs.GotBuildingVehicleAsset(assetMetaData.assetRef.fullName);

                if (includedInStyle || wanted)
                    BuildingVehicleImpl(assetMetaData.assetRef, wanted);
            }
            catch (Exception ex)
            {
                Failed(assetMetaData?.assetRef, ex);
                // CODebugBase<LogChannel>.Warn(LogChannel.Modding, string.Concat(new object[] { ex.GetType(), ": Loading custom asset failed:[", asset, "]\n", ex.Message }));
            }

            return true;
        }

        void BuildingVehicleImpl(Package.Asset data, bool wanted)
        {
            try
            {
                string name = AssetName(data.name);
                LoadingManager.instance.m_loadingProfilerCustomAsset.BeginLoading(name);
                // CODebugBase<LogChannel>.Log(LogChannel.Modding, string.Concat("Loading custom asset ", assetMetaData.name, " from ", asset));

                loadedAsset = data;
                GameObject go = data.Instantiate<GameObject>();
                go.name = data.package.packageName + "." + go.name;
                go.SetActive(false);
                PrefabInfo info = go.GetComponent<PrefabInfo>();
                info.m_isCustomContent = true;

                if (info.m_Atlas != null && info.m_InfoTooltipThumbnail != null && info.m_InfoTooltipThumbnail != string.Empty && info.m_Atlas[info.m_InfoTooltipThumbnail] != null)
                    info.m_InfoTooltipAtlas = info.m_Atlas;

                BuildingInfo bi = go.GetComponent<BuildingInfo>();

                if (bi != null)
                {
                    if (bi.m_lodObject != null)
                        bi.m_lodObject.SetActive(false);

                    PrefabCollection<BuildingInfo>.InitializePrefabs("Custom Assets", bi, null);
                    bi.m_dontSpawnNormally = !wanted;
                    buildingsCount++;
                }

                VehicleInfo vi = go.GetComponent<VehicleInfo>();

                if (vi != null)
                {
                    PrefabCollection<VehicleInfo>.InitializePrefabs("Custom Assets", vi, null);

                    if (vi.m_lodObject != null)
                        vi.m_lodObject.SetActive(false);

                    vehicleCount++;
                }
            }
            finally
            {
                loadedAsset = null;
                LoadingManager.instance.m_loadingProfilerCustomAsset.EndLoading();
            }
        }

        // There is an interesting bug in the package manager: secondary CustomAssetMetaDatas in a crp are considered always enabled.
        // As a result, the game loads all vehicle trailers, no matter if they are enabled or not. This is the fix.
        bool IsEnabled(Package.Asset asset)
        {
            if (asset.isMainAsset)
                return asset.isEnabled;

            Package.Asset main = asset.package.Find(asset.package.packageMainAsset);
            return main?.isEnabled ?? false;
        }

        internal static string AssetName(string name_Data) => name_Data.Length > 5 && name_Data.EndsWith("_Data") ? name_Data.Substring(0, name_Data.Length - 5) : name_Data;

        internal void Failed(Package.Asset data, Exception e)
        {
            string name = data?.name;

            if (name != null && failedAssets.Add(data.fullName))
            {
                Util.DebugPrint("Asset failed:", data.fullName);

                if (reportAssets)
                    AssetReport.instance.Failed(data.fullName);

                Profiling.CustomAssetFailed(AssetName(name));
                DualProfilerSource profiler = LoadingScreen.instance.DualSource;
                profiler?.SomeFailed();
            }

            if (e != null)
                UnityEngine.Debug.LogException(e);
        }

        internal void NotFound(string name)
        {
            if (name != null)
            {
                if (reportAssets)
                {
                    if (loadedAsset != null)
                        AssetReport.instance.NotFound(name, loadedAsset);
                    else
                        AssetReport.instance.NotFound(name);
                }

                if (failedAssets.Add(name))
                {
                    Util.DebugPrint("Asset not found:", name);
                    int j = name.IndexOf('.');

                    if (j >= 0 && j < name.Length - 1)
                        name = name.Substring(j + 1);

                    name = AssetName(name);
                    LoadingManager.instance.m_loadingProfilerCustomAsset.BeginLoading(name);
                    Profiling.CustomAssetNotFound(name);
                    LoadingManager.instance.m_loadingProfilerCustomAsset.EndLoading();
                    DualProfilerSource profiler = LoadingScreen.instance.DualSource;
                    profiler?.SomeNotFound();
                }
            }
        }

        internal static bool IsWorkshopPackage(Package package, out ulong id)
        {
            return ulong.TryParse(package.packageName, out id) && id > 999999;
        }

        internal static bool IsWorkshopPackage(string fullName, out ulong id)
        {
            int j = fullName.IndexOf('.');

            if (j <= 0 || j >= fullName.Length - 1)
            {
                id = 0;
                return false;
            }

            string p = fullName.Substring(0, j);
            return ulong.TryParse(p, out id) && id > 999999;
        }

        internal static bool IsPrivatePackage(string fullName)
        {
            ulong id;

            // Private: a local asset created by the player (not published on the workshop).
            // My rationale is the following:
            // 43453453.Name -> Workshop
            // Name.Name     -> Private
            // Name          -> Either an old-format (early 2015) reference, or something from DLC/Deluxe/Pre-order packs.
            //                  If loading is not successful then cannot tell for sure, assumed DLC/Deluxe/Pre-order.

            if (IsWorkshopPackage(fullName, out id))
                return false;
            else
                return fullName.IndexOf('.') >= 0;
        }
    }
}
