using System.Collections;
using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Pausa breve del juego al conectar un golpe (el impacto "pesa"). Si ya hay una en curso, se alarga en vez de
    /// encadenarse; nunca deja el timeScale alterado (respeta pausas y menús que lo pongan en 0).
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        private static HitStop runner;
        private float until;
        private float scale = 1f;
        private float restore = 1f;
        private bool active;

        public static void Trigger(float realSeconds, float slowScale)
        {
            if (realSeconds <= 0f || Time.timeScale <= 0f) return;
            if (runner == null)
            {
                var go = new GameObject("HitStop");
                go.hideFlags = HideFlags.HideAndDontSave;
                runner = go.AddComponent<HitStop>();
            }
            runner.Begin(realSeconds, slowScale);
        }

        private void Begin(float seconds, float slowScale)
        {
            until = Mathf.Max(until, Time.unscaledTime + seconds);
            if (!active)
            {
                restore = Time.timeScale;
                active = true;
                scale = slowScale;
                Time.timeScale = slowScale;
                StartCoroutine(Run());
            }
        }

        private IEnumerator Run()
        {
            while (Time.unscaledTime < until) yield return null;
            // Solo restaura si nadie más cambió el timeScale mientras tanto (p. ej. la pausa).
            if (Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = restore;
            active = false;
        }
    }
}
