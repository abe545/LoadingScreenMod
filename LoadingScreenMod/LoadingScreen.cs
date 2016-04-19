﻿using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;
using System.Reflection;
using System;
using System.Collections.Generic;

namespace LoadingScreenMod
{
    public sealed class LoadingScreen : DetourUtility
    {
        public static LoadingScreen instance;
        const float rotationSpeed = 100f, animationScale = 0.2f, meshInterval = AssetLoader.yieldInterval / 1050f, progressInterval = 0.55f;

        float timer, progress, meshTime, progressTime;
        int meshUpdates;
        internal readonly float meshWidth = Screen.width / 3, meshHeight = 3 * Screen.height / 4;

        // Image
        Mesh imageMesh;
        Material imageMaterial;
        float imageScale;
        bool imageLoaded;

        // Animations
        Mesh animationMesh;
        Material animationMaterial;
        Material barBGMaterial;
        Material barFGMaterial;
        bool animationLoaded;

        // Text background
        Mesh bgMesh = CreateQuads();
        Material bgMaterial = CreateMaterial(new Color(0f, 0f, 0f, 0.6f));
        bool bgLoaded;

        // Text
        internal UIFont uifont;
        Material textMaterial;
        Text[] texts;
        bool fontLoaded;

        readonly FieldInfo targetProgressField = typeof(LoadingAnimation).GetField("m_targetProgress", DetourUtility.FLAGS);
        readonly LoadingAnimation la = Singleton<LoadingManager>.instance.LoadingAnimationComponent;
        readonly Camera camera;

        internal SimpleProfilerSource SimulationSource => texts != null && texts.Length >= 3 ? texts[2].source as SimpleProfilerSource : null;
        internal DualProfilerSource DualSource => texts != null && texts.Length >= 1 ? texts[0].source as DualProfilerSource : null;

        public LoadingScreen()
        {
            instance = this;
            init(typeof(LoadingAnimation), "SetImage");
            init(typeof(LoadingAnimation), "SetText");
            init(typeof(LoadingAnimation), "OnEnable");
            init(typeof(LoadingAnimation), "OnDisable");
            init(typeof(LoadingAnimation), "Update");
            init(typeof(LoadingAnimation), "OnPostRender");
            this.camera = la.GetComponent<Camera>();
            this.bgLoaded = bgMesh != null && bgMaterial != null;
        }

        internal void Setup()
        {
            UIFontManager.callbackRequestCharacterInfo = (UIFontManager.CallbackRequestCharacterInfo) Delegate.Combine(UIFontManager.callbackRequestCharacterInfo,
                new UIFontManager.CallbackRequestCharacterInfo(instance.RequestCharacterInfo));
            Font.textureRebuilt += new Action<Font>(instance.FontTextureRebuilt);

            animationMesh = (Mesh) Util.Get(la, "m_animationMesh");
            animationMaterial = (Material) Util.Get(la, "m_animationMaterial");
            barBGMaterial = (Material) Util.Get(la, "m_barBGMaterial");
            barFGMaterial = (Material) Util.Get(la, "m_barFGMaterial");
            animationLoaded = (bool) Util.Get(la, "m_animationLoaded");
            Deploy();
            SetFont();
        }

        internal override void Dispose()
        {
            UIFontManager.callbackRequestCharacterInfo = (UIFontManager.CallbackRequestCharacterInfo) Delegate.Remove(UIFontManager.callbackRequestCharacterInfo,
                new UIFontManager.CallbackRequestCharacterInfo(instance.RequestCharacterInfo));
            Font.textureRebuilt -= new Action<Font>(instance.FontTextureRebuilt);

            Revert();
            base.Dispose();

            if (imageMaterial != null)
                UnityEngine.Object.Destroy(imageMaterial);

            foreach (Text t in texts)
                t.Dispose();

            if (textMaterial != null)
                UnityEngine.Object.Destroy(textMaterial);

            if (bgMesh != null)
                UnityEngine.Object.Destroy(bgMesh);

            if (bgMaterial != null)
                UnityEngine.Object.Destroy(bgMaterial);

            instance = null;
            imageMesh = null; imageMaterial = null; animationMesh = null; animationMaterial = null; barBGMaterial = null; barFGMaterial = null; textMaterial = null; bgMesh = null; bgMaterial = null;
            imageLoaded = animationLoaded = fontLoaded = bgLoaded = false;
            AssetLoader.instance?.Dispose();
            Sharing.instance?.Dispose();
            Profiling.Stop();
        }

        public void SetImage(Mesh mesh, Material material, float scale, bool showAnimation)
        {
            LoadingScreen inst = instance;

            if (inst.imageMaterial != null)
                UnityEngine.Object.Destroy(inst.imageMaterial);

            inst.imageMesh = mesh;
            inst.imageMaterial = new Material(material);
            inst.imageScale = scale;
            inst.imageLoaded = true;
        }

        public void SetText(UIFont font, Color color, float size, string title, string text) { }

        public void SetFont()
        {
            try
            {
                uifont = UIView.GetAView().defaultFont;
                textMaterial = new Material(uifont.material);
                UIFontManager.Invalidate(uifont);

                List<Text> list = new List<Text>(5);
                list.Add(new Text(new Vector3(-1.2f,   0.7f, 10f), new DualProfilerSource("Scenes and Assets:", 36)));
                list.Add(new Text(new Vector3(-0.35f, -0.52f, 10f), new SimpleProfilerSource("Main:", LoadingManager.instance.m_loadingProfilerMain)));
                list.Add(new Text(new Vector3(-0.35f, -0.62f, 10f), new SimpleProfilerSource("Simulation:", LoadingManager.instance.m_loadingProfilerSimulation)));
                list.Add(new Text(new Vector3(-0.1f,   0.44f, 10f), new TimeSource(), 1.5f));

                if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                    list.Add(new Text(new Vector3(-0.1f, 0.32f, 10f), new MemorySource(), 1.5f));

                texts = list.ToArray();
                fontLoaded = uifont != null;
            }
            catch (Exception e)
            {
                Util.DebugPrint("Font setup failed");
                UnityEngine.Debug.LogException(e);
            }
        }

        void RequestCharacterInfo()
        {
            UIDynamicFont uIDynamicFont = uifont as UIDynamicFont;

            if (uIDynamicFont == null || !UIFontManager.IsDirty(uIDynamicFont))
                return;

            uIDynamicFont.AddCharacterRequest("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890.-:", 1, FontStyle.Normal);
        }

        void FontTextureRebuilt(Font font)
        {
            if (uifont != null && font == uifont.baseFont)
            {
                meshTime = -1; // force update

                foreach (Text text in texts)
                    text.Clear();
            }
        }

        void OnEnable()
        {
            instance.camera.enabled = true;
            instance.camera.clearFlags = CameraClearFlags.Color;
        }

        void OnDisable()
        {
            instance.camera.enabled = false;
            instance.Dispose();
        }

        void Progress()
        {
            float targetProgress = (float) targetProgressField.GetValue(la);

            if (targetProgress >= 0f)
                progress += (Mathf.Clamp01(targetProgress + 0.05f) - progress) * 0.25f;
        }

        void Update()
        {
            LoadingScreen inst = instance;
            float f = Mathf.Min(0.125f, Time.deltaTime);
            inst.timer += f;
            float now = Time.time;

            if (now - inst.progressTime >= progressInterval)
            {
                inst.progressTime = now;
                inst.Progress();
            }
        }

        void OnPostRender()
        {
            LoadingScreen inst = instance;

            if (inst.imageLoaded)
            {
                Texture2D texture2D = inst.imageMaterial.mainTexture as Texture2D;
                float num = texture2D != null ? (float) texture2D.width / (float) texture2D.height : 1f;
                float num2 = 2f * inst.imageScale;

                if (inst.imageMaterial.SetPass(0))
                    Graphics.DrawMeshNow(inst.imageMesh, Matrix4x4.TRS(new Vector3(0f, 0f, 10f), Quaternion.identity, new Vector3(num2 * num, num2, num2)));
            }

            if (inst.animationLoaded)
            {
                Quaternion q = Quaternion.AngleAxis(inst.timer * rotationSpeed, Vector3.back);
                inst.animationMaterial.color = new Color(0.5f, 0.75f, 1.0f, 1f);
                Mesh amesh = inst.animationMesh;

                if (inst.animationMaterial.SetPass(0))
                    Graphics.DrawMeshNow(amesh, Matrix4x4.TRS(new Vector3(0f, 0f, 10f), q, new Vector3(animationScale, animationScale, animationScale)));

                Vector3 pos = new Vector3(0f, -0.15f, 10f);
                Vector3 s = new Vector3(animationScale * 2f, animationScale * 0.125f, animationScale);
                inst.barBGMaterial.color = new Color(1f, 1f, 1f, 1f);

                if (inst.barBGMaterial.SetPass(0))
                    Graphics.DrawMeshNow(amesh, Matrix4x4.TRS(pos, Quaternion.identity, s));

                s.x *= 0.9875f; s.y *= 0.8f;
                pos.x -= s.x * (1f - inst.progress) * 0.5f;
                s.x *= inst.progress;
                inst.barFGMaterial.color = new Color(1f, 1f, 1f, 1f);

                if (inst.barFGMaterial.SetPass(0))
                    Graphics.DrawMeshNow(amesh, Matrix4x4.TRS(pos, Quaternion.identity, s));
            }

            if (inst.imageLoaded && inst.fontLoaded)
            {
                if (inst.bgLoaded && inst.bgMaterial.SetPass(0))
                    Graphics.DrawMeshNow(inst.bgMesh, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one));

                float now = Time.time;

                if (now - inst.meshTime >= meshInterval || inst.meshUpdates < 3)
                {
                    inst.meshTime = now;
                    inst.meshUpdates++;

                    foreach (Text text in inst.texts)
                        text.UpdateText();
                }

                if (inst.textMaterial.SetPass(0))
                    foreach (Text text in inst.texts)
                        Graphics.DrawMeshNow(text.mesh, Matrix4x4.TRS(text.pos, Quaternion.identity, text.Scale));
            }
        }

        static Mesh CreateQuads()
        {
            List<Vector3> vertices = new List<Vector3>(12);
            List<int> triangles = new List<int>(18);
            CreateQuad(-1.25f,  0.75f, 0.75f, 1.50f, vertices, triangles);
            CreateQuad(-0.40f, -0.47f, 0.75f, 0.28f, vertices, triangles);
            CreateQuad(-0.15f,  0.49f, 0.30f, 0.27f, vertices, triangles);
            Mesh mesh = new Mesh();
            mesh.name = "BG Quads";
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            return mesh;
        }

        static void CreateQuad(float x, float y, float w, float h, List<Vector3> vertices, List<int> triangles)
        {
            int n = vertices.Count;
            vertices.Add(new Vector3(x, y - h, 10f));
            vertices.Add(new Vector3(x + w, y - h, 10f));
            vertices.Add(new Vector3(x, y, 10f));
            vertices.Add(new Vector3(x + w, y, 10f));
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
            triangles.Add(n + 2); triangles.Add(n + 3); triangles.Add(n + 1);
        }

        static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Custom/Loading/AlphaBlend");

            return new Material(shader)
            {
                name = "BG Material",
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    sealed class Text
    {
        internal readonly Vector3 pos;
        internal readonly Source source;
        readonly float scale;
        string text = string.Empty;
        internal Mesh mesh = new Mesh();
        internal Vector3 Scale => new Vector3(scale, scale, scale);
        const float baseScale = 0.002083333f;

        internal Text(Vector3 pos, Source source, float scaleFactor = 1f)
        {
            this.pos = pos;
            this.source = source;
            this.scale = baseScale * scaleFactor;
        }

        internal void Clear() => text = String.Empty;

        internal void Dispose()
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);

            mesh = null;
        }

        internal void UpdateText()
        {
            string s = source.CreateText();

            if (s != null && s != text)
            {
                text = s;
                GenerateMesh();
            }
        }

        bool GenerateMesh()
        {
            UIFont uifont = LoadingScreen.instance.uifont;

            if (uifont == null)
                return false;

            long t = Profiling.Ticks;
            UIFontRenderer uiFontRenderer = uifont.ObtainRenderer();
            UIRenderData uiRenderData = UIRenderData.Obtain();

            try
            {
                mesh.Clear();
                uiFontRenderer.defaultColor = Color.white;
                uiFontRenderer.textScale = 1f;
                uiFontRenderer.pixelRatio = 1f;
                uiFontRenderer.processMarkup = true;
                uiFontRenderer.multiLine = true;
                uiFontRenderer.maxSize = new Vector2(LoadingScreen.instance.meshWidth, LoadingScreen.instance.meshHeight);
                uiFontRenderer.shadow = false;
                uiFontRenderer.Render(text, uiRenderData);

                mesh.vertices = uiRenderData.vertices.ToArray();
                mesh.colors32 = uiRenderData.colors.ToArray();
                mesh.uv = uiRenderData.uvs.ToArray();
                mesh.triangles = uiRenderData.triangles.ToArray();
            }
            catch (Exception e)
            {
                Util.DebugPrint("Cannot generate font mesh");
                UnityEngine.Debug.LogException(e);
                return false;
            }
            finally
            {
                uiFontRenderer.Dispose();
                uiRenderData.Dispose();
            }

            return true;
        }
    }
}
