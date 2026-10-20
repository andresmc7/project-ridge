using ProjectRidge.AI;
using ProjectRidge.CameraRig;
using ProjectRidge.Core;
using ProjectRidge.Player;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// "Peso" del combate: reacción física (resortes) en el papá y los Lurker, golpe de cámara (extensión de
    /// Cinemachine) y la salpicadura de icor al conectar el garrote.
    /// </summary>
    public static partial class HollowBuilder
    {
        static HitReactionConfig ReactionConfig(string name, float stiffness, float damping, float impulse, float arms, float head)
        {
            string path = "Assets/_Project/Data/" + name + ".asset";
            var c = AssetDatabase.LoadAssetAtPath<HitReactionConfig>(path);
            if (c == null) { c = ScriptableObject.CreateInstance<HitReactionConfig>(); AssetDatabase.CreateAsset(c, path); }
            c.stiffness = stiffness;
            c.damping = damping;
            c.impulseScale = impulse;
            c.arms = arms;
            c.head = head;
            EditorUtility.SetDirty(c);
            return c;
        }

        static void AddReaction(GameObject character, HitReactionConfig config)
        {
            Animator animator = null;
            foreach (var a in character.GetComponentsInChildren<Animator>(true))
                if (a.avatar != null && a.avatar.isHuman) { animator = a; break; }
            if (animator == null) return;
            // Limpia reacciones viejas puestas en un Animator sin avatar.
            foreach (var stale in character.GetComponentsInChildren<HitReaction>(true))
                if (stale.gameObject != animator.gameObject) Object.DestroyImmediate(stale);
            var r = animator.GetComponent<HitReaction>();
            if (r == null) r = animator.gameObject.AddComponent<HitReaction>();
            var so = new SerializedObject(r);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Project Ridge/The Hollow/Setup Combat Feel")]
        public static string SetupFeel()
        {
            // El papá: firme (se dobla y vuelve); el Lurker: flácido, rebota más y los brazos largos quedan colgando.
            var father = ReactionConfig("HitReaction_Father", 190f, 13f, 460f, 0.5f, 0.5f);
            var lurker = ReactionConfig("HitReaction_Lurker", 130f, 7.5f, 620f, 0.85f, 0.7f);
            var player = Object.FindFirstObjectByType<PlayerController>();
            AddReaction(player.gameObject, father);
            int enemies = 0;
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                AddReaction(e.gameObject, lurker);
                enemies++;
            }

            var fall = player.GetComponent<PlayerFallRecovery>();
            if (fall == null) fall = player.gameObject.AddComponent<PlayerFallRecovery>();
            var fso = new SerializedObject(fall);
            fso.FindProperty("controller").objectReferenceValue = player;
            fso.FindProperty("body").objectReferenceValue = player.GetComponent<CharacterController>();
            fso.ApplyModifiedPropertiesWithoutUndo();

            var rig = Object.FindFirstObjectByType<IsoCameraRig>();
            if (rig != null && rig.GetComponent<CameraShake>() == null) rig.gameObject.AddComponent<CameraShake>();

            var combat = player.GetComponent<PlayerCombat>();
            var so = new SerializedObject(combat);
            so.FindProperty("hitFx").objectReferenceValue = IchorPrefab();
            so.ApplyModifiedPropertiesWithoutUndo();
            return "combat feel: father + " + enemies + " lurkers, camera shake " + (rig != null);
        }

        /// <summary>Salpicadura de icor oscuro (gotas que caen con gravedad) al conectar el garrote.</summary>
        static ParticleSystem IchorPrefab()
        {
            const string path = "Assets/_Project/Prefabs/FX_Ichor.prefab";
            var go = new GameObject("FX_Ichor");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.02f, 0.03f), new Color(0.05f, 0.01f, 0.02f));
            main.gravityModifier = 1.6f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22, 32) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.08f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.04f;
            psr.lengthScale = 1.2f;
            string matPath = DataDir + "/FX/FX_Ichor.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DataDir + "/FX/Tex_SoftDot.png"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            psr.sharedMaterial = m;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved.GetComponent<ParticleSystem>();
        }
    }
}
