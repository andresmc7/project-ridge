using System.IO;
using ProjectRidge.CameraRig;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// QA visual: renderiza la cámara principal con el encuadre de juego (IsoCameraConfig) mirando a un punto y
    /// guarda un PNG. Sirve en edición y en play mode; restaura la cámara al terminar.
    /// </summary>
    public static class Shots
    {
        public static string Capture(string file, Vector3 focus, int width = 1600, int height = 900)
        {
            var cam = Camera.main;
            var config = AssetDatabase.LoadAssetAtPath<IsoCameraConfig>("Assets/_Project/Data/IsoCameraConfig.asset");
            var t = cam.transform;
            Vector3 pos = t.position;
            Quaternion rot = t.rotation;
            float fov = cam.fieldOfView;

            Quaternion r = Quaternion.Euler(config.pitch, config.yaw, 0f);
            t.SetPositionAndRotation(focus + Vector3.up * config.targetHeight + r * new Vector3(0f, 0f, -config.distance) + Quaternion.Euler(0f, config.yaw, 0f) * Vector3.forward * config.framingOffset, r);
            cam.fieldOfView = config.fieldOfView;

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            t.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            return file;
        }
    }
}
