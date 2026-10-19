using System;
using System.Collections;
using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Primer avistamiento con guion: al pisar el sendero, un Lurker cruza la otra orilla del arroyo entre la niebla
    /// y desaparece. No ataca ni percibe: solo establece la amenaza. Ocurre una sola vez.
    /// A mitad de camino se detiene y mira al papá, y luego huye corriendo. La pausa ocurre la primera vez que el
    /// cuerpo queda bien dentro del encuadre a distancia media (así el jugador la ve siempre, camine como camine);
    /// si eso no pasa, ocurre igual en el punto de respaldo de la ruta.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ScriptedApparition : MonoBehaviour
    {
        [SerializeField] private GameObject body;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform[] path;
        [SerializeField] private float speed = 2.2f;
        [SerializeField] private float startDelay = 0.4f;
        [Tooltip("Punto de respaldo: si no se detuvo antes por estar en cuadro, se detiene aquí.")]
        [SerializeField] private int stareAtPoint = 2;
        [SerializeField] private float stareSeconds = 1.8f;
        [SerializeField] private float fleeSpeed = 4.6f;
        [Tooltip("Distancia al papá (m) en la que la pausa se ve bien: ni encima ni perdida en la niebla.")]
        [SerializeField] private Vector2 stareDistance = new Vector2(5f, 13f);
        [Tooltip("Margen del encuadre (fracción de pantalla) que debe quedar libre alrededor del cuerpo.")]
        [Range(0f, 0.4f)] [SerializeField] private float viewMargin = 0.15f;

        public static event Action<Vector3> Appeared;

        private bool triggered;
        private bool stared;
        private Transform player;
        private Camera cam;

        private void Awake()
        {
            if (body != null) body.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered || !other.TryGetComponent(out ProjectRidge.Player.PlayerHealth _)) return;
            triggered = true;
            player = other.transform;
            cam = Camera.main;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(startDelay);
            body.transform.position = path[0].position;
            body.SetActive(true);
            Appeared?.Invoke(path[0].position);
            animator.SetBool("Hunting", false);
            float current = speed;
            for (int i = 1; i < path.Length; i++)
            {
                if (!stared && i - 1 >= stareAtPoint)
                {
                    yield return Stare();
                    current = fleeSpeed;
                }
                Vector3 to = path[i].position;
                while ((body.transform.position - to).sqrMagnitude > 0.05f)
                {
                    if (!stared && WellInView())
                    {
                        yield return Stare();
                        current = fleeSpeed;
                    }
                    Vector3 step = Vector3.MoveTowards(body.transform.position, to, current * Time.deltaTime);
                    Vector3 dir = to - body.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f) body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                    body.transform.position = step;
                    animator.SetFloat("Speed", current);
                    yield return null;
                }
            }
            body.SetActive(false);
        }

        private bool WellInView()
        {
            if (cam == null || player == null) return false;
            float d = Vector3.Distance(body.transform.position, player.position);
            if (d < stareDistance.x || d > stareDistance.y) return false;
            Vector3 v = cam.WorldToViewportPoint(body.transform.position + Vector3.up * 1.5f);
            return v.z > 0f && v.x > viewMargin && v.x < 1f - viewMargin && v.y > viewMargin && v.y < 1f - viewMargin;
        }

        // Se detiene, gira hacia el papá... y arranca a correr.
        private IEnumerator Stare()
        {
            stared = true;
            animator.SetFloat("Speed", 0f);
            float until = Time.time + stareSeconds;
            while (Time.time < until)
            {
                if (player != null)
                {
                    Vector3 look = player.position - body.transform.position;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 4f);
                }
                yield return null;
            }
            animator.SetBool("Hunting", true);
        }
    }
}
