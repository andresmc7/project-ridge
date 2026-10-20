using System.Collections.Generic;
using ProjectRidge.Items;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Presencia física: escombros sueltos (ramas, latas, cubetas) que salen volando cuando un Lurker los atraviesa o
    /// el papá los patea corriendo (y entonces hacen ruido), y troncos grandes que la IA rodea en vez de atravesar.
    /// </summary>
    public static partial class HollowBuilder
    {
        public const int DebrisLayer = 12;
        const string DebrisLayerName = "Debris";

        /// <summary>Capa "Debris": choca con el terreno y el mundo, no con el papá ni los Lurker (no los frena).</summary>
        static void EnsureDebrisLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var slot = layers.GetArrayElementAtIndex(DebrisLayer);
            if (slot.stringValue != DebrisLayerName)
            {
                slot.stringValue = DebrisLayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
            Physics.IgnoreLayerCollision(DebrisLayer, LayerMask.NameToLayer("Player"), true);
            Physics.IgnoreLayerCollision(DebrisLayer, LayerMask.NameToLayer("Enemy"), true);
            Physics.IgnoreLayerCollision(DebrisLayer, LayerMask.NameToLayer("TallGrass"), true);
        }

        /// <summary>Los troncos de cobertura no se pueden atravesar: la IA los rodea (área no caminable al hornear).</summary>
        static void MakeNavBlocker(GameObject go)
        {
            var mod = go.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = 1; // Not Walkable
        }

        static KnockableConfig KnockConfig()
        {
            const string path = "Assets/_Project/Data/KnockableConfig.asset";
            var c = AssetDatabase.LoadAssetAtPath<KnockableConfig>(path);
            if (c == null) { c = ScriptableObject.CreateInstance<KnockableConfig>(); AssetDatabase.CreateAsset(c, path); }
            return c;
        }

        static GameObject Knockable(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Collider shape, float triggerPad, float mass, KnockableProp.Surface surface, KnockableConfig config)
        {
            var go = shape.gameObject;
            go.name = name;
            go.layer = DebrisLayer;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveGI = ReceiveGI.LightProbes;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Trigger algo más grande en "Ignore Raycast": detecta al papá y a los Lurker sin chocar con ellos.
            var trig = new GameObject("KnockTrigger");
            trig.layer = 2;
            trig.transform.SetParent(go.transform, false);
            switch (shape)
            {
                case CapsuleCollider cap:
                    var tc = trig.AddComponent<CapsuleCollider>();
                    tc.direction = cap.direction; tc.height = cap.height + triggerPad * 2f; tc.radius = cap.radius + triggerPad; tc.center = cap.center;
                    tc.isTrigger = true;
                    break;
                case BoxCollider box:
                    var tb = trig.AddComponent<BoxCollider>();
                    tb.size = box.size + Vector3.one * triggerPad * 2f; tb.center = box.center;
                    tb.isTrigger = true;
                    break;
            }
            var kp = go.AddComponent<KnockableProp>();
            var so = new SerializedObject(kp);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("surface").enumValueIndex = (int)surface;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Escombros sobre las rutas de los Lurker y en las salas (los ven pasar volando: el mundo pesa).</summary>
        static int PlaceDebris(Transform root, Terrain t, Mats m)
        {
            EnsureDebrisLayer();
            var config = KnockConfig();
            var parent = new GameObject("Debris").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(Seed + 501);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var branchMat = m.bark;
            var tin = Simple("Hollow_TinCan", null, null, new Color(0.42f, 0.4f, 0.36f), 0.55f);
            tin.SetFloat("_Metallic", 0.7f);
            var bucketMat = Simple("Hollow_Bucket", null, null, new Color(0.22f, 0.3f, 0.36f), 0.4f);
            bucketMat.SetFloat("_Metallic", 0.5f);
            var canMesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Log("Debris_Can", 0.16f, 0.05f, 10));
            var bucketMesh = ChunkyMeshGenerator.Save(ChunkyMeshGenerator.Log("Debris_Bucket", 0.32f, 0.15f, 12));
            int count = 0;

            bool Free(Vector3 p)
            {
                if (Walk(p) > -0.5f) return false;
                foreach (var h in HidePatches) if (new Vector2(p.x - h.x, p.z - h.y).sqrMagnitude < (h.z + 0.4f) * (h.z + 0.4f)) return false;
                if (DistanceToTrails(p) < 0.6f) return false; // el sendero queda limpio (el papá corre por ahí sin patear nada)
                return true;
            }

            void Branch(Vector3 p)
            {
                float len = R(0.8f, 1.5f), radius = R(0.045f, 0.08f);
                var mesh = LogMesh(len, radius);
                var cap = new GameObject().AddComponent<CapsuleCollider>();
                cap.direction = 2; cap.radius = radius; cap.height = len;
                var pos = OnGround(t, p.x, p.z, radius);
                Knockable(parent, "Branch", mesh, branchMat, pos, Quaternion.Euler(0f, R(0f, 360f), 0f), cap, 0.3f, 1.2f, KnockableProp.Surface.Wood, config);
                count++;
            }

            // Sobre cada patrulla: ramas a los lados del recorrido (el Lurker las barre al pasar).
            foreach (var (name, pts) in Patrols)
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                    for (int k = 0; k < 2; k++)
                    {
                        float f = R(0.2f, 0.8f);
                        Vector2 q = Vector2.Lerp(a, b, f);
                        Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * R(-0.5f, 0.5f);
                        var p = new Vector3(q.x + side.x, 0f, q.y + side.y);
                        if (Free(p)) Branch(p);
                    }
                }

            // Sueltas por las salas.
            for (int tries = 0; tries < 600 && count < 40; tries++)
            {
                var p = new Vector3(R(TerrainMin.x + 4f, TerrainMin.x + SizeX - 4f), 0f, R(-8f, 8f));
                if (Free(p)) Branch(p);
            }

            // Campamento: latas y cubetas (metal: suenan más).
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector3(Camp.x + R(-4f, 4f), 0f, Camp.z + R(-3.5f, 3f));
                if (!Free(p)) continue;
                bool bucket = i % 3 == 0;
                var cap = new GameObject().AddComponent<CapsuleCollider>();
                cap.direction = 2; cap.radius = bucket ? 0.15f : 0.05f; cap.height = bucket ? 0.32f : 0.16f;
                var pos = OnGround(t, p.x, p.z, bucket ? 0.15f : 0.05f);
                Knockable(parent, bucket ? "Bucket" : "TinCan", bucket ? bucketMesh : canMesh, bucket ? bucketMat : tin, pos,
                    Quaternion.Euler(0f, R(0f, 360f), 0f), cap, 0.3f, bucket ? 1.5f : 0.3f, KnockableProp.Surface.Metal, config);
                count++;
            }
            return count;
        }
    }
}
