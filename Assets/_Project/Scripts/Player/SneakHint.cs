using ProjectRidge.AI;
using ProjectRidge.UI;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Enseña el sigilo una vez por partida. Con un Lurker cerca, el stick apenas inclinado agacha al papá (pisadas
    /// de 1.2 m en vez de 3.5): el juego lo resuelve solo, pero nada lo decía. El bot moría en cada corrida a la
    /// salida de la sala 2 por caminar de pie a 3.9 m de un Lurker. Si el papá camina de pie un
    /// rato con un Lurker a ≤ 10 m, aparece el aviso con el control que se esté usando.
    /// </summary>
    public class SneakHint : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private SurvivalHud hud;
        [Tooltip("Segundos seguidos caminando de pie con un Lurker cerca antes del aviso.")]
        [Min(0f)] [SerializeField] private float exposedSeconds = 1.2f;
        [Min(0.5f)] [SerializeField] private float showSeconds = 3.5f;
        [Tooltip("Un Lurker despierto a esta distancia (m) oiría las pisadas: es cuando el aviso tiene sentido (no cerca de un reflector).")]
        [Min(1f)] [SerializeField] private float lurkerRadius = 10f;
        [SerializeField] private string stickText = "Ease the stick to sneak";
        [SerializeField] private string keyboardText = "Press C to crouch";

        private float exposed;
        /// <summary>El aviso ya se mostró en esta partida.</summary>
        public bool Shown { get; private set; }

        private void Update()
        {
            if (Shown || hud == null) return;
            bool hunted = false, lurkerNear = false;
            foreach (var e in EnemyController.Active)
            {
                hunted |= e.IsHunting;
                lurkerNear |= !e.IsDead && (e.transform.position - transform.position).sqrMagnitude < lurkerRadius * lurkerRadius;
            }
            bool walkingUpright = !player.IsCrouching && player.CurrentSpeed > 0.3f && player.Mode != MovementMode.Sprint;
            exposed = lurkerNear && walkingUpright && !hunted ? exposed + Time.deltaTime : 0f;
            if (exposed < exposedSeconds) return;
            Shown = true;
            hud.ShowToast(input.MoveFromKeyboard ? keyboardText : stickText, showSeconds);
        }
    }
}
