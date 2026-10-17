using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Convierte sets de texturas de Poly Haven (diff / nor_gl / rough / ao) en materiales URP Lit y TerrainLayers.
    /// Los originales en ThirdParty no se modifican (solo sus import settings); la máscara empaquetada
    /// (R metálico=0, G AO, B 0, A suavidad=1-rough) se guarda en Assets/_Project/Art/Textures.
    /// </summary>
    public static class PolyHavenMaterialBuilder
    {
        public const string TexturesRoot = "Assets/ThirdParty/PolyHaven/Textures";
        public const string ModelsRoot = "Assets/ThirdParty/PolyHaven/Models";
        public const string OutTextures = "Assets/_Project/Art/Textures/PolyHaven";
        public const string OutMaterials = "Assets/_Project/Art/Materials/PolyHaven";
        public const string OutTerrainLayers = "Assets/_Project/Art/TerrainLayers";

        public struct TextureSet
        {
            public Texture2D Albedo, Normal, Mask;
        }

        public static TextureSet BuildTextureSet(string folder, string name)
        {
            string diff = Find(folder, name + "_diff");
            string nor = Find(folder, name + "_nor_gl");
            string rough = Find(folder, name + "_rough");
            string ao = Find(folder, name + "_ao");

            Configure(diff, srgb: true, normal: false, readable: false);
            Configure(nor, srgb: false, normal: true, readable: false);
            if (rough != null) Configure(rough, srgb: false, normal: false, readable: true);
            if (ao != null) Configure(ao, srgb: false, normal: false, readable: true);

            var set = new TextureSet
            {
                Albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(diff),
                Normal = AssetDatabase.LoadAssetAtPath<Texture2D>(nor),
                Mask = rough != null ? PackMask(name, rough, ao) : null
            };

            if (rough != null) Configure(rough, srgb: false, normal: false, readable: false);
            if (ao != null) Configure(ao, srgb: false, normal: false, readable: false);
            return set;
        }

        public static Material BuildLitMaterial(string name, TextureSet set, float smoothnessScale = 1f, bool doubleSided = false, Texture2D alphaClip = null)
        {
            EnsureFolder(OutMaterials);
            // Prefijo PolyHaven_ (no PH_: en este proyecto PH_ significa placeholder de IA no comercial).
            string path = OutMaterials + "/PolyHaven_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", set.Albedo);
            mat.SetColor("_BaseColor", Color.white);
            if (set.Normal != null)
            {
                mat.SetTexture("_BumpMap", set.Normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (set.Mask != null)
            {
                mat.SetTexture("_MetallicGlossMap", set.Mask);
                mat.SetTexture("_OcclusionMap", set.Mask);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.SetFloat("_Smoothness", smoothnessScale);
                mat.SetFloat("_OcclusionStrength", 1f);
            }
            if (doubleSided) mat.SetFloat("_Cull", 0f);
            if (alphaClip != null)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.4f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static TerrainLayer BuildTerrainLayer(string name, TextureSet set, float tileSize)
        {
            EnsureFolder(OutTerrainLayers);
            string path = OutTerrainLayers + "/TL_" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = set.Albedo;
            layer.normalMapTexture = set.Normal;
            layer.maskMapTexture = set.Mask;
            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.smoothness = 1f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>Albedo con canal alfa tomado de una textura de máscara aparte (p. ej. el helecho).</summary>
        public static Texture2D CombineAlpha(string name, Texture2D albedo, string alphaPath)
        {
            string albedoPath = AssetDatabase.GetAssetPath(albedo);
            Configure(albedoPath, srgb: true, normal: false, readable: true);
            Configure(alphaPath, srgb: false, normal: false, readable: true);
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var alpha = AssetDatabase.LoadAssetAtPath<Texture2D>(alphaPath);
            Color[] c = src.GetPixels();
            Color[] a = alpha.GetPixels();
            for (int i = 0; i < c.Length; i++) c[i].a = a[i].r;
            var outTex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
            outTex.SetPixels(c);
            outTex.Apply();
            EnsureFolder(OutTextures);
            string outPath = OutTextures + "/" + name + "_albedo_alpha.png";
            File.WriteAllBytes(outPath, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            Configure(albedoPath, srgb: true, normal: false, readable: false);
            Configure(alphaPath, srgb: false, normal: false, readable: false);
            AssetDatabase.ImportAsset(outPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(outPath);
            imp.alphaIsTransparency = true;
            imp.mipMapsPreserveCoverage = true; // el follaje recortado no se adelgaza a distancia
            imp.alphaTestReferenceValue = 0.4f;
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static Texture2D PackMask(string name, string roughPath, string aoPath)
        {
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            var ao = aoPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath) : null;
            int w = rough.width, h = rough.height;
            Color[] r = rough.GetPixels();
            Color[] a = ao != null && ao.width == w && ao.height == h ? ao.GetPixels() : null;

            var mask = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var px = new Color[r.Length];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color(0f, a != null ? a[i].r : 1f, 0f, 1f - r[i].r);
            mask.SetPixels(px);
            mask.Apply();

            EnsureFolder(OutTextures);
            string outPath = OutTextures + "/" + name + "_mask.png";
            File.WriteAllBytes(outPath, mask.EncodeToPNG());
            Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(outPath);
            Configure(outPath, srgb: false, normal: false, readable: false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static void Configure(string path, bool srgb, bool normal, bool readable)
        {
            if (path == null) return;
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool changed = imp.sRGBTexture != srgb || imp.isReadable != readable
                || (normal && imp.textureType != TextureImporterType.NormalMap);
            imp.sRGBTexture = srgb;
            imp.isReadable = readable;
            if (normal) imp.textureType = TextureImporterType.NormalMap;
            imp.maxTextureSize = 1024;
            if (changed) imp.SaveAndReimport();
        }

        public static string Find(string folder, string prefix)
        {
            foreach (var guid in AssetDatabase.FindAssets(prefix, new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).StartsWith(prefix)) return p;
            }
            return null;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
