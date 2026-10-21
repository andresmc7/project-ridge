using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectRidge.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Atmósfera de The Hollow: luna con sombras de nubes, niebla densa y bancos de niebla baja, llovizna, esporas,
    /// hojas que caen, luciérnagas y polillas, vaho del papá, charcos en el sendero, haz volumétrico del OVNI con
    /// polvo y resplandor en el cielo, SSAO y la gradación (sombras frías, luces cálidas, grano, aberración).
    /// </summary>
    public static partial class HollowBuilder
    {
        const string FxDir = DataDir + "/FX";

        // ------------------------------------------------------------------ Texturas procedurales

        static Texture2D WriteTex(string name, int w, int h, System.Func<int, int, Color> px, bool repeat, bool alphaIsTransparency = true, bool linear = false)
        {
            PolyHavenMaterialBuilder.EnsureFolder(FxDir);
            string path = FxDir + "/" + name + ".png";
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
            var arr = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) arr[y * w + x] = px(x, y);
            t.SetPixels(arr);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.alphaIsTransparency = alphaIsTransparency;
            imp.sRGBTexture = !linear;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float TileFbm(float u, float v, int octaves, float seed)
        {
            // fbm periódico: ruido sobre un toro 4D aproximado con dos círculos.
            float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f;
            for (int o = 0; o < octaves; o++)
            {
                float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
                float nx = Mathf.Cos(a) * freq + seed, ny = Mathf.Sin(a) * freq + seed * 0.7f;
                float nz = Mathf.Cos(b) * freq + seed * 1.3f, nw = Mathf.Sin(b) * freq + seed * 0.4f;
                float n = (Mathf.PerlinNoise(nx + nz * 1.7f, ny + nw * 1.3f) + Mathf.PerlinNoise(nz + nx * 0.6f, nw + ny * 0.9f)) * 0.5f;
                sum += n * amp; norm += amp; amp *= 0.5f; freq *= 2f;
            }
            return sum / norm;
        }

        static Texture2D NoiseTex() => WriteTex("Tex_Noise", 256, 256, (x, y) =>
        {
            float n = TileFbm(x / 256f, y / 256f, 4, 3.1f);
            n = Mathf.Clamp01((n - 0.3f) * 2.2f);
            return new Color(n, n, n, n);
        }, true, false, true);

        static Texture2D CloudCookie() => WriteTex("Tex_CloudCookie", 256, 256, (x, y) =>
        {
            // Claros de luna entre las copas: manchas de luz y oscuridad bien marcadas (no un velo uniforme).
            float n = PeriodicFbm(x / 256f, y / 256f, 4, 3, 117);
            float c = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0.42f, 0.6f, n));
            return new Color(c, c, c, c);
        }, true, false, true);

        static Texture2D SoftDot() => WriteTex("Tex_SoftDot", 64, 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
            return new Color(1f, 1f, 1f, a);
        }, false);

        static Texture2D LeafSprite() => WriteTex("Tex_Leaf", 64, 64, (x, y) =>
        {
            float u = (x + 0.5f) / 64f * 2f - 1f, v = (y + 0.5f) / 64f * 2f - 1f;
            float w = 0.42f * (1f - v * v);
            float a = Mathf.Clamp01((w - Mathf.Abs(u)) * 40f) * Mathf.Clamp01((1f - Mathf.Abs(v)) * 20f);
            float vein = Mathf.Abs(u) < 0.03f ? 0.7f : 1f;
            return new Color(0.42f * vein, 0.32f * vein, 0.16f * vein, a);
        }, false);

        static Texture2D BeamCookie() => WriteTex("Tex_BeamCookie", 128, 128, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f)) / 64f;
            float core = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, d));
            float ring = 1f - 0.25f * Mathf.Sin(d * 26f) * Mathf.SmoothStep(0.2f, 0.8f, d);
            float n = 0.8f + 0.2f * TileFbm(x / 128f, y / 128f, 3, 5f);
            float c = core * ring * n;
            return new Color(c, c, c, c);
        }, false, false, true);

        // ------------------------------------------------------------------ Materiales con viento

        internal static Material WindMaterial(string name, Texture albedo, Texture normal, Color color, float smoothness, float strength, float heightScale, float flutter, bool cullOff, float translucency)
        {
            string path = DataDir + "/" + name + ".mat";
            var shader = Shader.Find("ProjectRidge/FoliageWind");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_UseNormal", 1f); m.EnableKeyword("_NORMALMAP"); }
            else { m.SetFloat("_UseNormal", 0f); m.DisableKeyword("_NORMALMAP"); }
            m.SetFloat("_WindStrength", strength);
            m.SetFloat("_HeightScale", heightScale);
            m.SetFloat("_WindFlutter", flutter);
            m.SetFloat("_Cull", cullOff ? 0f : 2f);
            m.SetFloat("_Translucency", translucency);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material FxMaterial(string name, string shader, Texture tex, Color color, bool additive)
        {
            string path = FxDir + "/" + name + ".mat";
            PolyHavenMaterialBuilder.EnsureFolder(FxDir);
            var sh = Shader.Find(shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            if (shader == "Universal Render Pipeline/Particles/Unlit")
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.SrcAlpha : BlendMode.SrcAlpha));
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SoftParticlesEnabled", 1f);
                m.EnableKeyword("_SOFTPARTICLES_ON");
                m.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.6f, 0f, 0f));
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ Entrada

        static string BuildAtmosphere(Terrain t, GameObject player)
        {
            var log = new System.Text.StringBuilder();
            var noise = NoiseTex();
            var dot = SoftDot();
            var old = GameObject.Find("Atmosphere");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Atmosphere").transform;

            SetupRenderPipeline(log);
            SetupMoon(log);
            SetupGrading();
            SetupFlashlight(player);
            SetupHeroFill(player, log);

            // Niebla de escena: densa, claustrofóbica, fría.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.045f, 0.055f, 0.05f); // verde musgo oscuro, no azul marino (comparación con la referencia, P3)
            RenderSettings.fogDensity = 0.012f; // velo leve: la niebla espesa va baja, en tarjetas sobre las zonas hondas
            Camera.main.backgroundColor = RenderSettings.fogColor;

            int cards = FogCards(root, t, noise);
            log.AppendLine("fog cards=" + cards);
            int puddles = 0; // sin charcos: desde la cámara alta se leían como agujeros negros (Puddles queda disponible)
            log.AppendLine("puddles=" + puddles);
            int fungi = GlowFungi(root, t);
            log.AppendLine("glow fungi=" + fungi);
            Particles(root, player, dot);
            log.AppendLine("particles ok");
            UpgradeUfo(noise, dot);
            log.AppendLine("ufo beam ok");
            return log.ToString();
        }

        static void SetupRenderPipeline(System.Text.StringBuilder log)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                asset.supportsCameraDepthTexture = true;
                asset.supportsHDR = true;
                asset.shadowDistance = 28f; // CLAUDE.md: sombra de luna corta (~25–30 m); el encuadre llega a ~25 m
                asset.shadowCascadeCount = 2;
                if (asset.name.StartsWith("Mobile"))
                {
                    // iPhone 16+ (A18): sombras nítidas y escala de render con FSR (reescalado + nitidez) en vez de bilineal.
                    asset.mainLightShadowmapResolution = 2048;
                    asset.renderScale = 0.8f;
                    asset.upscalingFilter = UpscalingFilterSelection.FSR;
                    asset.fsrOverrideSharpness = true;
                    asset.fsrSharpness = 0.85f;
                }
                var so = new SerializedObject(asset);
                var cookies = so.FindProperty("m_SupportsLightCookies");
                if (cookies != null) cookies.boolValue = true;
                var soft = so.FindProperty("m_SoftShadowsSupported");
                if (soft != null) soft.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
            // SSAO en cada renderer (A18+: lo soporta de sobra).
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue; // nunca tocar los renderers de los paquetes
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var existing = data.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
                if (existing != null) { ConfigureSsao(existing, path.Contains("Mobile")); log.AppendLine("SSAO configured in " + data.name); continue; }
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance("ScreenSpaceAmbientOcclusion");
                if (feature == null) { log.AppendLine("SSAO type not found"); continue; }
                feature.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                ConfigureSsao(feature, path.Contains("Mobile"));
                EditorUtility.SetDirty(data);
                log.AppendLine("SSAO added to " + data.name);
            }
            SetupTiltShift(log);
            var cam = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            cam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.antialiasingQuality = AntialiasingQuality.Medium;
            cam.renderPostProcessing = true;
            cam.requiresDepthTexture = true;
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Desenfoque de maqueta en los bordes (Echoes of Wisdom): pase de pantalla completa antes del post-proceso,
        /// en todos los renderers del proyecto (PC y iPhone).
        /// </summary>
        static void SetupTiltShift(System.Text.StringBuilder log)
        {
            string matPath = DataDir + "/Hollow_TiltShift.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Hidden/ProjectRidge/TiltShift")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetFloat("_FocusY", 0.42f);   // el papá queda en el tercio bajo (encuadre adelantado al norte)
            mat.SetFloat("_Band", 0.17f);
            mat.SetFloat("_Feather", 0.26f);
            mat.SetFloat("_MaxRadius", 0.0105f);
            mat.SetFloat("_Corners", 0.6f);
            mat.SetFloat("_Highlight", 3f);
            EditorUtility.SetDirty(mat);

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var feature = data.rendererFeatures.FirstOrDefault(f => f is FullScreenPassRendererFeature && f.name == "TiltShift") as FullScreenPassRendererFeature;
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = "TiltShift";
                    AssetDatabase.AddObjectToAsset(feature, data);
                    data.rendererFeatures.Add(feature);
                    var so = new SerializedObject(data);
                    var map = so.FindProperty("m_RendererFeatureMap");
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.AppendLine("tilt-shift added to " + data.name);
                }
                feature.passMaterial = mat;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                feature.fetchColorBuffer = true;
                feature.requirements = ScriptableRenderPassInput.Color;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>Mismo AO en PC y iPhone (el de por defecto de CreateInstance es otro look); en móvil a media resolución.</summary>
        static void ConfigureSsao(ScriptableRendererFeature feature, bool mobile)
        {
            var so = new SerializedObject(feature);
            so.FindProperty("m_Settings.Intensity").floatValue = 0.4f;
            so.FindProperty("m_Settings.Radius").floatValue = 0.3f;
            so.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.25f;
            so.FindProperty("m_Settings.Downsample").boolValue = mobile;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }

        static void SetupMoon(System.Text.StringBuilder log)
        {
            var moon = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (moon == null) { log.AppendLine("no moon"); return; }
            moon.color = new Color(0.66f, 0.74f, 0.8f); // fría pero poco saturada: la escena no debe teñirse de azul
            moon.intensity = 1.35f; // baja: la luz fuerte sale de las islas (linterna, faroles, haces), no del cielo
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.9f;
            moon.transform.rotation = Quaternion.Euler(30f, 200f, 0f); // contraluz (POLISH_PASS 3.10): desde el fondo hacia la cámara, recorta siluetas
            moon.cookie = CloudCookie();
            if (!moon.TryGetComponent(out UniversalAdditionalLightData _)) moon.gameObject.AddComponent<UniversalAdditionalLightData>();
            if (!moon.TryGetComponent(out CloudShadows _)) moon.gameObject.AddComponent<CloudShadows>();
            var cs = new SerializedObject(moon.GetComponent<CloudShadows>());
            cs.FindProperty("cookieSize").floatValue = 30f; // manchas de luz entre copas, del tamaño de una sala
            cs.FindProperty("velocity").vector2Value = new Vector2(0.25f, 0.1f);
            cs.ApplyModifiedPropertiesWithoutUndo();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.11f, 0.13f, 0.14f); // 40% menos que antes: el 60% de la pantalla en sombra profunda
            RenderSettings.ambientEquatorColor = new Color(0.05f, 0.052f, 0.045f);
            RenderSettings.ambientGroundColor = new Color(0.021f, 0.024f, 0.027f);
            // Sin skybox, los reflejos usaban un cubemap viejo y brillante: charcos y superficies mojadas
            // reflejan ahora un cielo nocturno tenue con el halo de la luna.
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = NightReflection(-moon.transform.forward);
            RenderSettings.reflectionIntensity = 1f;
        }

        /// <summary>Cubemap HDR del cielo nocturno: degradado horizonte-cenit, suelo oscuro y halo de luna.</summary>
        static Cubemap NightReflection(Vector3 moonDir)
        {
            const string path = "Assets/_Project/Art/Hollow/Hollow_NightReflection.cubemap";
            const int n = 64;
            var cube = new Cubemap(n, TextureFormat.RGBAHalf, true);
            var horizon = new Color(0.07f, 0.085f, 0.11f);
            var zenith = new Color(0.02f, 0.028f, 0.045f);
            var ground = new Color(0.01f, 0.012f, 0.014f);
            var moonTint = new Color(0.6f, 0.71f, 0.92f);
            var px = new Color[n * n];
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;
                    Vector3 d = face switch
                    {
                        CubemapFace.PositiveX => new Vector3(1f, -v, -u),
                        CubemapFace.NegativeX => new Vector3(-1f, -v, u),
                        CubemapFace.PositiveY => new Vector3(u, 1f, v),
                        CubemapFace.NegativeY => new Vector3(u, -1f, -v),
                        CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
                        _ => new Vector3(-u, -v, -1f),
                    };
                    d.Normalize();
                    Color c = d.y >= 0f ? Color.Lerp(horizon, zenith, Mathf.Pow(d.y, 0.6f)) : Color.Lerp(horizon, ground, Mathf.Clamp01(-d.y * 6f));
                    float m = Mathf.Max(0f, Vector3.Dot(d, moonDir));
                    c += moonTint * (Mathf.Pow(m, 24f) * 0.18f + Mathf.Pow(m, 900f) * 1.6f);
                    c.a = 1f;
                    px[j * n + i] = c;
                }
                cube.SetPixels(px, face);
            }
            cube.Apply(true);
            var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        /// <summary>Exposición y contraste del grading (ajustados midiendo el histograma de las capturas, P3).</summary>
        public static float GradingExposure = 3.7f, GradingContrast = 18f;

        /// <summary>QA (P3): prueba otra exposición/contraste sobre el perfil de post.</summary>
        public static void TuneGrading(float exposure, float contrast)
        {
            GradingExposure = exposure;
            GradingContrast = contrast;
            SetupGrading();
        }

        static void SetupGrading()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Art/LookDev/LookDev_PostFX.asset");
            if (profile == null) return;
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T c)) { c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); }
                c.active = true;
                return c;
            }
            Get<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var ca = Get<ColorAdjustments>();
            // Oscuridad legible: medios tonos visibles (media de luminancia ~0.2 medida en captura), negros solo en los bordes.
            // POLISH_PASS 3.10: más oscuro y con más contraste; el histograma de la captura debe dar >= 50% de píxeles con
            // luminancia < 0.08 y >= 3% por encima de 0.6 (islas de luz).
            ca.postExposure.Override(GradingExposure); ca.contrast.Override(GradingContrast); ca.saturation.Override(-6f);
            // Balance algo cálido: la referencia es tierra y musgo con luz de farol, no un azul uniforme.
            var wb = Get<WhiteBalance>();
            wb.temperature.Override(14f); wb.tint.Override(-4f);
            var smh = Get<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.82f, 1.0f, 0.97f, -0.02f));     // sombras verde azulado oscuro
            smh.midtones.Override(new Vector4(0.97f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.12f, 1.0f, 0.84f, 0.03f));     // luces altas cálidas
            var lgg = Get<LiftGammaGain>();
            lgg.lift.Override(new Vector4(0.98f, 1f, 1.03f, 0f)); // sin elevar el negro: +0.01 lineal subía todo el piso a ~0.1 en pantalla (P3)
            var grain = Get<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium2); grain.intensity.Override(0.32f); grain.response.Override(0.75f);
            Get<ChromaticAberration>().intensity.Override(0.12f);
            var vig = Get<Vignette>();
            vig.intensity.Override(0.36f); vig.smoothness.Override(0.5f); vig.color.Override(new Color(0.01f, 0.015f, 0.02f));
            var bloom = Get<Bloom>();
            // Umbral bajo e intensidad alta: las islas de luz brillan alrededor (P3: >= 3% de píxeles > 0.6).
            bloom.threshold.Override(0.7f); bloom.intensity.Override(1.6f); bloom.clamp.Override(6f); bloom.scatter.Override(0.72f); bloom.tint.Override(new Color(1f, 0.95f, 0.9f)); // clamp: sin tope, la linterna a medio metro de una pared se volvía un disco blanco gigante
            var dof = Get<DepthOfField>(); // reemplazado por el tilt-shift (pase propio)
            dof.mode.Override(DepthOfFieldMode.Off);
            dof.gaussianStart.Override(17f); dof.gaussianEnd.Override(34f); dof.gaussianMaxRadius.Override(1.2f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Luz de relleno fría y suave, solo para la capa de render del papá: lo mantiene legible en la oscuridad sin
        /// iluminar el escenario (no cuenta como luz visible del presupuesto: no toca el entorno).
        /// </summary>
        static void SetupHeroFill(GameObject player, System.Text.StringBuilder log)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var so = new SerializedObject(asset);
                var p = so.FindProperty("m_SupportsLightLayers");
                if (p != null) { p.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); }
                log.AppendLine("rendering layers on " + asset.name + ": " + (p != null));
            }
            var old = GameObject.Find("HeroFill");
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject("HeroFill");
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4.6f;
            l.intensity = 2.6f;
            l.color = new Color(0.55f, 0.66f, 0.88f);
            l.shadows = LightShadows.None;
            var data = go.AddComponent<UniversalAdditionalLightData>();
            data.renderingLayers = 2u;
            FollowPlayer(go, player.transform, new Vector3(-0.8f, 2.8f, -1.9f)); // desde el lado de la cámara (al sur)
        }

        static void SetupFlashlight(GameObject player)
        {
            var t = player.transform.Find("Flashlight");
            if (t == null) return;
            var l = t.GetComponent<Light>();
            // Linterna de mano: haz con anillo (cookie), alcance medio, sombras suaves (una de las dos luces con sombra).
            l.intensity = 30f; // isla de luz cálida en el suelo (P3: histograma >= 3% de luces); el disco blanco lo evitan el tope del bloom y el origen adelantado
            l.range = 14f;
            l.spotAngle = 52f;
            l.innerSpotAngle = 20f; // halo suave: borde ancho, sin disco duro
            l.colorTemperature = 3200f;
            l.useColorTemperature = true;
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
            l.color = new Color(1f, 0.95f, 0.88f);
            l.cookie = BeamCookie();
            l.shadows = LightShadows.Soft;
        }

        // ------------------------------------------------------------------ Niebla baja

        static int FogCards(Transform root, Terrain t, Texture2D noise)
        {
            var mat = new Material(Shader.Find("ProjectRidge/FogCard"));
            string path = FxDir + "/FX_FogCard.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.shader = mat.shader; Object.DestroyImmediate(mat); mat = existing; } else AssetDatabase.CreateAsset(mat, path);
            mat.SetTexture("_NoiseTex", noise);
            mat.SetColor("_Color", new Color(0.5f, 0.58f, 0.66f, 0.32f));
            mat.SetFloat("_NoiseScale", 0.07f);
            mat.SetVector("_Speed", new Vector4(0.04f, 0.015f, 0f, 0f));
            mat.SetFloat("_SoftDistance", 1.2f);
            mat.SetFloat("_CameraFade", 5f);
            EditorUtility.SetDirty(mat);
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var parent = new GameObject("FogCards").transform;
            parent.SetParent(root, false);
            int n = 0;
            void Card(Vector3 pos, float size, float yaw)
            {
                var go = new GameObject("FogCard");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, yaw, 0f));
                go.transform.localScale = new Vector3(size, size, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                n++;
            }
            // El arroyo exhala niebla.
            // (El arroyo ya no lleva niebla encima: se leía como neblina en vez de agua.)
            // Niebla que se arrastra desde el bosque hasta el borde de cada sala, nunca encima del escenario:
            // el sendero y los escondites siempre se leen; la oscuridad y la niebla los rodean.
            var rng = new System.Random(31);
            int placed = 0;
            for (int tries = 0; tries < 4000 && placed < 70; tries++)
            {
                var p = new Vector3(TerrainMin.x + (float)rng.NextDouble() * SizeX, 0f, TerrainMin.y + (float)rng.NextDouble() * SizeZ);
                float d = Walk(p);
                // Al sur, sobre la terraza baja (primer plano, bajo el nivel del escenario); al norte, solo detrás del
                // segundo escalón: nunca delante de los muros ni del bosque de la primera terraza.
                bool southSide = !float.IsInfinity(SouthClearance(p, 0f));
                if (southSide ? (d < 1.4f || d > 9f) : d < StepUp2At + 2.5f) continue;
                float size = 7f + (float)rng.NextDouble() * 5f;
                Card(OnGround(t, p.x, p.z, 0.6f + (float)rng.NextDouble() * 0.6f), size, (float)rng.NextDouble() * 360f);
                placed++;
            }
            return n;
        }

        // ------------------------------------------------------------------ Hongos luminosos

        static Mesh ShroomPart(string name, bool cap)
        {
            const int seg = 8;
            var v = new List<Vector3>();
            var tris = new List<int>();
            if (!cap)
            {
                // Tallo: cilindro unitario (radio 1, alto 1), se escala por instancia.
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(d); v.Add(d + Vector3.up);
                }
                for (int i = 0; i < seg; i++) { int b = i * 2; tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 }); }
            }
            else
            {
                // Sombrero: media elipsoide aplastada con borde un poco caído.
                const int rings = 4;
                for (int r = 0; r <= rings; r++)
                {
                    float phi = r / (float)rings * Mathf.PI * 0.5f;
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = i / (float)seg * Mathf.PI * 2f;
                        float rad = Mathf.Cos(phi);
                        v.Add(new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(phi) * 0.6f - (r == 0 ? 0.15f : 0f), Mathf.Sin(a) * rad));
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int i = 0; i < seg; i++)
                    {
                        int a0 = r * (seg + 1) + i, a1 = a0 + seg + 1;
                        tris.AddRange(new[] { a0, a1, a0 + 1, a1, a1 + 1, a0 + 1 });
                    }
            }
            var m = new Mesh { name = name, vertices = v.ToArray(), triangles = tris.ToArray() };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Hongos bioluminiscentes en racimos a lo largo del borde de cada sala: dibujan el contorno del escenario
        /// (lectura de diorama), ponen color frío en la oscuridad y no gastan luces (emisión + bloom).
        /// </summary>
        static int GlowFungi(Transform root, Terrain t)
        {
            var capMat = AssetDatabase.LoadAssetAtPath<Material>(DataDir + "/Hollow_GlowCap.mat");
            if (capMat == null) { capMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(capMat, DataDir + "/Hollow_GlowCap.mat"); }
            capMat.SetColor("_BaseColor", new Color(0.25f, 0.8f, 0.78f));
            capMat.SetFloat("_Smoothness", 0.55f);
            capMat.EnableKeyword("_EMISSION");
            capMat.SetColor("_EmissionColor", new Color(0.18f, 1.25f, 1.15f));
            capMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(capMat);
            var stemMat = AssetDatabase.LoadAssetAtPath<Material>(DataDir + "/Hollow_GlowStem.mat");
            if (stemMat == null) { stemMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(stemMat, DataDir + "/Hollow_GlowStem.mat"); }
            stemMat.SetColor("_BaseColor", new Color(0.72f, 0.78f, 0.7f));
            stemMat.EnableKeyword("_EMISSION");
            stemMat.SetColor("_EmissionColor", new Color(0.05f, 0.2f, 0.18f));
            EditorUtility.SetDirty(stemMat);

            var stem = ShroomPart("ShroomStem", false);
            var cap = ShroomPart("ShroomCap", true);
            var chunks = new Dictionary<(int, int), (List<CombineInstance> stems, List<CombineInstance> caps)>();
            var rng = new System.Random(57);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool NearPatch(Vector3 p)
            {
                foreach (var h in HidePatches) if (new Vector2(p.x - h.x, p.z - h.y).magnitude < h.z + 0.8f) return true;
                return false;
            }

            int clusters = 0, count = 0;
            for (int tries = 0; tries < 20000 && clusters < 110; tries++)
            {
                var c = new Vector3(R(TerrainMin.x, TerrainMin.x + SizeX), 0f, R(TerrainMin.y, TerrainMin.y + SizeZ));
                float d = Walk(c);
                if (d < -0.5f || d > 0.9f) continue;
                if (DistanceToTrails(c) < 2.4f || NearPatch(c) || Mathf.Abs(c.x - RavineX(c.z)) < 4.2f) continue;
                clusters++;
                int n = rng.Next(3, 8);
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector3(c.x + R(-0.45f, 0.45f), 0f, c.z + R(-0.45f, 0.45f));
                    p.y = GroundY(t, p) - 0.02f;
                    float h = R(0.07f, 0.26f), capR = R(0.05f, 0.13f);
                    var tilt = Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f));
                    var key = (Mathf.FloorToInt(p.x / 12f), Mathf.FloorToInt(p.z / 12f));
                    if (!chunks.TryGetValue(key, out var lists)) chunks[key] = lists = (new List<CombineInstance>(), new List<CombineInstance>());
                    lists.stems.Add(new CombineInstance { mesh = stem, transform = Matrix4x4.TRS(p, tilt, new Vector3(capR * 0.28f, h, capR * 0.28f)) });
                    lists.caps.Add(new CombineInstance { mesh = cap, transform = Matrix4x4.TRS(p + tilt * Vector3.up * h, tilt, Vector3.one * capR) });
                    count++;
                }
            }

            var parent = new GameObject("GlowFungi").transform;
            parent.SetParent(root, false);
            int k = 0;
            foreach (var kv in chunks)
            {
                foreach (var (list, mat, tag) in new[] { (kv.Value.stems, stemMat, "Stems"), (kv.Value.caps, capMat, "Caps") })
                {
                    var mesh = new Mesh { name = "Fungi" + tag + "_" + k, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(list.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    mesh = ChunkyMeshGenerator.Save(mesh);
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveGI = ReceiveGI.LightProbes;
                }
                k++;
            }
            return count;
        }

        // ------------------------------------------------------------------ Charcos

        static Mesh PuddleMesh(int seed)
        {
            var rng = new System.Random(seed);
            const int seg = 20;
            var v = new Vector3[seg + 1];
            var uv = new Vector2[seg + 1];
            var tris = new int[seg * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float r = 1f + 0.22f * Mathf.Sin(a * 3f + (float)rng.NextDouble() * 6f) + 0.1f * (float)rng.NextDouble();
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.7f);
                uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
                tris[i * 3] = 0; tris[i * 3 + 1] = (i + 1) % seg + 1; tris[i * 3 + 2] = i + 1;
            }
            var m = new Mesh { name = "Puddle_" + seed, vertices = v, uv = uv, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return ChunkyMeshGenerator.Save(m);
        }

        static int Puddles(Transform root, Terrain t)
        {
            var mat = Simple("Hollow_Puddle", null, null, new Color(0.03f, 0.035f, 0.04f), 0.97f);
            // Solo reflejo del entorno (cielo nocturno); el brillo especular directo de la luna se quemaba con el bloom.
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            var parent = new GameObject("Puddles").transform;
            parent.SetParent(root, false);
            var meshes = new[] { PuddleMesh(1), PuddleMesh(2), PuddleMesh(3) };
            var rng = new System.Random(9);
            int n = 0;
            var pts = MainTrail.Concat(NorthTrail).ToArray();
            for (int i = 1; i < pts.Length; i++)
            {
                if (rng.NextDouble() < 0.35) continue;
                Vector3 p = Vector3.Lerp(pts[i - 1], pts[i], (float)rng.NextDouble());
                if (Mathf.Abs(p.x - RavineX(p.z)) < 5f) continue;
                var go = new GameObject("Puddle");
                go.transform.SetParent(parent, false);
                Vector3 pos = OnGround(t, p.x + (float)(rng.NextDouble() - 0.5), p.z + (float)(rng.NextDouble() - 0.5), 0.025f);
                Vector3 nrm = t.terrainData.GetInterpolatedNormal((pos.x - t.transform.position.x) / SizeX, (pos.z - t.transform.position.z) / SizeZ);
                go.transform.SetPositionAndRotation(pos, Quaternion.FromToRotation(Vector3.up, nrm) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                go.transform.localScale = Vector3.one * (0.5f + (float)rng.NextDouble() * 0.9f);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[rng.Next(meshes.Length)];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ Partículas

        static ParticleSystem Emitter(Transform parent, string name, Material mat, System.Action<ParticleSystem> setup)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            setup(ps);
            return ps;
        }

        static void FollowPlayer(GameObject go, Transform player, Vector3 offset)
        {
            var f = go.AddComponent<FollowTarget>();
            var so = new SerializedObject(f);
            so.FindProperty("target").objectReferenceValue = player;
            so.FindProperty("offset").vector3Value = offset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Particles(Transform root, GameObject player, Texture2D dot)
        {
            var parent = new GameObject("Particles").transform;
            parent.SetParent(root, false);
            var p = player.transform;

            // Llovizna alrededor del jugador (bosque recién llovido).
            var rainMat = FxMaterial("FX_Rain", "Universal Render Pipeline/Particles/Unlit", dot, new Color(0.7f, 0.8f, 0.9f, 0.32f), true);
            var rain = Emitter(parent, "Drizzle", rainMat, ps =>
            {
                var m = ps.main; m.startLifetime = 0.85f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f); m.maxParticles = 900;
                var e = ps.emission; e.rateOverTime = 520f;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(26f, 1f, 26f);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = new ParticleSystem.MinMaxCurve(0.8f, 1.2f); v.y = new ParticleSystem.MinMaxCurve(-11f, -9f); v.z = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.035f; r.lengthScale = 1f;
            });
            // Nace por debajo de la cámara (~11 m): ninguna gota pasa pegada al lente como un rayón.
            FollowPlayer(rain.gameObject, p, new Vector3(0f, 7.5f, 0f));

            // Esporas y polvo flotando a la altura del pecho.
            var sporeMat = FxMaterial("FX_Spores", "Universal Render Pipeline/Particles/Unlit", dot, new Color(0.85f, 0.8f, 0.7f, 0.4f), true);
            var spores = Emitter(parent, "Spores", sporeMat, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f); m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f); m.maxParticles = 300;
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0.15f));
                var e = ps.emission; e.rateOverTime = 30f;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(22f, 3f, 22f);
                var n = ps.noise; n.enabled = true; n.strength = 0.25f; n.frequency = 0.3f; n.scrollSpeed = 0.1f;
                var c = ps.colorOverLifetime; c.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                c.color = g;
            });
            FollowPlayer(spores.gameObject, p, new Vector3(0f, 1.6f, 0f));

            // Hojas y agujas que caen girando.
            var leafMat = FxMaterial("FX_Leaves", "Universal Render Pipeline/Particles/Unlit", LeafSprite(), new Color(0.75f, 0.75f, 0.7f, 1f), false);
            var leaves = Emitter(parent, "FallingLeaves", leafMat, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f); m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f); m.maxParticles = 120;
                m.gravityModifier = 0.025f; m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                var e = ps.emission; e.rateOverTime = 7f;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(24f, 1f, 24f);
                var n = ps.noise; n.enabled = true; n.strength = 0.6f; n.frequency = 0.5f;
                var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            });
            FollowPlayer(leaves.gameObject, p, new Vector3(0f, 7f, 0f));

            // Luciérnagas junto al refugio y en los bordes del prado (vida pese a todo).
            var flyMat = FxMaterial("FX_Fireflies", "Universal Render Pipeline/Particles/Unlit", dot, new Color(0.85f, 1f, 0.45f, 1f), true);
            foreach (var c in new[] { CabinCenter + new Vector3(4f, 1.2f, -3f), new Vector3(Meadow.x - 6f, 1f, Meadow.z + 4f), new Vector3(Camp.x - 3f, 1f, Camp.z - 4f) })
            {
                Emitter(parent, "Fireflies", flyMat, ps =>
                {
                    var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f); m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f); m.maxParticles = 40;
                    var e = ps.emission; e.rateOverTime = 4f;
                    var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 3.5f;
                    var n = ps.noise; n.enabled = true; n.strength = 0.5f; n.frequency = 0.4f;
                    var col = ps.colorOverLifetime; col.enabled = true;
                    var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.2f, 0.6f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                    col.color = g;
                }).transform.position = new Vector3(c.x, GroundY(Terrain.activeTerrain, c) + c.y, c.z);
            }

            // Polillas alrededor del farol del refugio.
            var lantern = GameObject.Find("LanternLight");
            if (lantern != null)
            {
                var mothMat = FxMaterial("FX_Moths", "Universal Render Pipeline/Particles/Unlit", dot, new Color(0.9f, 0.85f, 0.75f, 0.7f), false);
                var moths = Emitter(parent, "Moths", mothMat, ps =>
                {
                    var m = ps.main; m.startLifetime = 5f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f); m.maxParticles = 12;
                    m.simulationSpace = ParticleSystemSimulationSpace.Local;
                    var e = ps.emission; e.rateOverTime = 2f;
                    var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.5f;
                    var v = ps.velocityOverLifetime; v.enabled = true; v.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); v.orbitalY = new ParticleSystem.MinMaxCurve(2f, 4f); v.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f); v.radial = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
                    var n = ps.noise; n.enabled = true; n.strength = 0.8f; n.frequency = 2f;
                });
                moths.transform.SetParent(lantern.transform, false);
            }

            // Vaho del papá: noche fría, una bocanada cada ~3 s.
            var breathMat = FxMaterial("FX_Breath", "Universal Render Pipeline/Particles/Unlit", dot, new Color(0.85f, 0.88f, 0.92f, 0.22f), false);
            var head = player.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "Head");
            if (head != null)
            {
                var breath = Emitter(parent, "Breath", breathMat, ps =>
                {
                    var m = ps.main; m.startLifetime = 1.3f; m.startSpeed = 0.35f; m.startSize = 0.1f; m.maxParticles = 30;
                    var e = ps.emission; e.rateOverTime = 0f;
                    e.SetBursts(new[] { new ParticleSystem.Burst(0.2f, 6, 6, 0, 3.2f) { cycleCount = 0 } });
                    var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 12f; s.radius = 0.01f;
                    var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.2f));
                    var col = ps.colorOverLifetime; col.enabled = true;
                    var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
                    col.color = g;
                });
                breath.transform.SetParent(head, false);
                breath.transform.localPosition = new Vector3(0f, 0.05f, 0.12f);
                breath.transform.localRotation = Quaternion.identity;
            }
        }

        // ------------------------------------------------------------------ OVNI

        static void UpgradeUfo(Texture2D noise, Texture2D dot)
        {
            var ufo = Object.FindFirstObjectByType<ProjectRidge.AI.UFOController>();
            if (ufo == null) return;
            var beam = ufo.transform.Find("Beam");
            var light = beam.GetComponent<Light>();
            light.cookie = BeamCookie();
            light.color = new Color(0.78f, 0.93f, 1f);
            var visual = beam.Find("BeamVisual");
            var mat = new Material(Shader.Find("ProjectRidge/VolumetricBeam"));
            string path = FxDir + "/FX_UfoBeam.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.shader = mat.shader; Object.DestroyImmediate(mat); mat = existing; } else AssetDatabase.CreateAsset(mat, path);
            mat.SetTexture("_NoiseTex", noise);
            mat.SetColor("_Color", new Color(0.6f, 0.85f, 1f));
            mat.SetFloat("_Intensity", 0.75f);
            mat.SetFloat("_Density", 0.08f);
            mat.SetFloat("_NoiseScale", 0.16f);
            mat.SetFloat("_NoiseSpeed", 0.3f);
            mat.SetFloat("_CameraFade", 6f);
            mat.SetFloat("_EdgePower", 1.6f);
            EditorUtility.SetDirty(mat);
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Polvo y gotas suspendidos dentro del haz.
            var old = beam.Find("BeamDust");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var dustMat = FxMaterial("FX_BeamDust", "Universal Render Pipeline/Particles/Unlit", dot, new Color(0.75f, 0.92f, 1f, 0.55f), true);
            var dust = Emitter(beam, "BeamDust", dustMat, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f); m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f); m.maxParticles = 400;
                var e = ps.emission; e.rateOverTime = 110f;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 12f; s.radius = 0.05f; s.length = 22f; s.shapeType = ParticleSystemShapeType.ConeVolume;
                var n = ps.noise; n.enabled = true; n.strength = 0.3f; n.frequency = 0.5f;
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            });
            dust.transform.localRotation = Quaternion.identity; // el cono de la forma apunta a +Z como el haz

            // Resplandor difuso en el cielo: la nave se intuye detrás de las nubes.
            var glowOld = ufo.transform.Find("SkyGlow");
            if (glowOld != null) Object.DestroyImmediate(glowOld.gameObject);
            var glowMat = FxMaterial("FX_SkyGlow", "Universal Render Pipeline/Particles/Unlit", dot, new Color(0.55f, 0.8f, 1f, 0.16f), true);
            var glow = Emitter(ufo.transform, "SkyGlow", glowMat, ps =>
            {
                var m = ps.main; m.startLifetime = 1000f; m.startSpeed = 0f; m.startSize = 22f; m.maxParticles = 1; m.simulationSpace = ParticleSystemSimulationSpace.Local;
                var e = ps.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var s = ps.shape; s.enabled = false;
            });
            glow.transform.localPosition = new Vector3(0f, 2f, 0f);
        }
    }
}
