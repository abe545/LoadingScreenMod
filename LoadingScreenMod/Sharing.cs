using System.Collections.Generic;
using ColossalFramework.Packaging;
using UnityEngine;

namespace LoadingScreenMod
{
    internal sealed class Sharing : DetourUtility
    {
        internal static Sharing instance;
        int texhit, mathit, meshit, texload, matload, mesload;

        Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        readonly bool shareMeshes = Settings.settings.shareMeshes;

        internal Sharing()
        {
            instance = this;

            // Be quick, or the JIT Compiler will inline calls to this one. It is a small method, less than 32 IL bytes.
            init(typeof(PackageDeserializer), "DeserializeMeshFilter");

            if (Settings.settings.shareTextures)
                init(typeof(PackageDeserializer), "DeserializeMaterial");

            if (Settings.settings.shareMaterials)
                init(typeof(PackageDeserializer), "DeserializeMeshRenderer");
        }

        internal override void Dispose()
        {
            Util.DebugPrint("Textures / Materials / Meshes loaded:", texload, "/", matload, "/", mesload, "referenced:", texhit, "/", mathit, "/", meshit);
            Revert();
            base.Dispose();
            textures.Clear(); materials.Clear(); meshes.Clear();
            instance = null; textures = null; materials = null; meshes = null;
        }

        internal static UnityEngine.Object DeserializeMaterial(Package package, PackageReader reader)
        {
            string materialName = reader.ReadString();
            string shaderName = reader.ReadString();
            Material material = new Material(Shader.Find(shaderName));
            material.name = materialName;
            int numProperties = reader.ReadInt32();

            for (int i = 0; i < numProperties; i++)
            {
                int kind = reader.ReadInt32();

                if (kind == 0)
                    material.SetColor(reader.ReadString(), reader.ReadColor());
                else if (kind == 1)
                    material.SetVector(reader.ReadString(), reader.ReadVector4());
                else if (kind == 2)
                    material.SetFloat(reader.ReadString(), reader.ReadSingle());
                else if (kind == 3)
                {
                    string propertyName = reader.ReadString();

                    if (!reader.ReadBoolean())
                    {
                        string checksum = reader.ReadString();
                        Texture texture;

                        if (!Sharing.instance.textures.TryGetValue(checksum, out texture))
                        {
                            texture = PackageManager.FindAssetByChecksum(checksum).Instantiate<Texture>();
                            Sharing.instance.textures[checksum] = texture;
                            Sharing.instance.texload++;
                        }
                        else
                            Sharing.instance.texhit++;

                        material.SetTexture(propertyName, texture);
                    }
                    else
                        material.SetTexture(propertyName, null);
                }
            }

            return material;
        }

        internal static void DeserializeMeshRenderer(Package package, MeshRenderer renderer, PackageReader reader)
        {
            int count = reader.ReadInt32();
            Material[] materials = new Material[count];

            for (int i = 0; i < count; i++)
            {
                string checksum = reader.ReadString();
                Material material;

                if (!Sharing.instance.materials.TryGetValue(checksum, out material))
                {
                    material = PackageManager.FindAssetByChecksum(checksum).Instantiate<Material>();
                    Sharing.instance.materials[checksum] = material;
                    Sharing.instance.matload++;
                }
                else
                    Sharing.instance.mathit++;

                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }

        internal static void DeserializeMeshFilter(Package package, MeshFilter meshFilter, PackageReader reader)
        {
            string checksum = reader.ReadString();
            Mesh mesh;

            if (Sharing.instance.shareMeshes && Sharing.instance.meshes.TryGetValue(checksum, out mesh))
                Sharing.instance.meshit++;
            else
            {
                mesh = PackageManager.FindAssetByChecksum(checksum).Instantiate<Mesh>();
                Sharing.instance.meshes[checksum] = mesh;
                Sharing.instance.mesload++;
            }

            meshFilter.sharedMesh = mesh;
        }
    }
}
