using System;
using ProjectRidge.AI;
using ProjectRidge.Items;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Botón ACCIÓN (POLISH_PASS 3.1): un solo botón cuyo efecto sale del contexto, por prioridad:
    /// inspeccionar pista › usar (radio) › recoger › registrar › derribo por la espalda › golpe (Lurker cerca y
    /// garrote) › empujar (sin garrote). Sin contexto, el botón se oculta.
    /// </summary>
    public class PlayerActions : MonoBehaviour
    {
        public enum Context { None, Inspect, Use, PickUp, Search, Takedown, Strike, Shove }

        [SerializeField] private CombatConfig config;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;

        public Context Current { get; private set; }
        /// <summary>El Lurker al que apunta el contexto de combate (derribo, golpe, empujón).</summary>
        public EnemyController Target { get; private set; }

        public event Action<Context> Performed;

        private void OnEnable() => input.ActionPressed += Perform;
        private void OnDisable() => input.ActionPressed -= Perform;

        private void Update() => Current = Resolve(out var _);

        private Context Resolve(out EnemyController target)
        {
            target = null;
            Target = null;
            if (health.IsDead || controller.InputLocked) return Context.None;
            var it = interactor.Peek();
            if (it != null)
            {
                switch (it.ActionKind)
                {
                    case Interactable.Kind.Inspect: return Context.Inspect;
                    case Interactable.Kind.Use: return Context.Use;
                    case Interactable.Kind.PickUp: return Context.PickUp;
                    case Interactable.Kind.Search: return Context.Search;
                }
            }
            var near = NearestLurker(config.strikeContextRange);
            Target = near;
            if (near == null) return Context.None;
            target = near;
            if (combat.CanTakedown(near)) return Context.Takedown;
            if (near.IsDown) return Context.None; // ya está en el suelo: huir, no rematar
            return combat.HasClub ? Context.Strike : Context.Shove;
        }

        /// <summary>Ejecuta lo que el contexto diga (tecla E, botón A, botón ACCIÓN táctil).</summary>
        public void Perform()
        {
            var ctx = Resolve(out var target);
            bool done = false;
            switch (ctx)
            {
                case Context.Inspect:
                case Context.Use:
                case Context.PickUp:
                case Context.Search:
                    done = interactor.UseCurrent();
                    break;
                case Context.Takedown: done = combat.Takedown(target); break;
                case Context.Strike: combat.Strike(); done = true; break;
                case Context.Shove: done = combat.Shove(target); break;
            }
            if (done) Performed?.Invoke(ctx);
        }

        private EnemyController NearestLurker(float range)
        {
            EnemyController best = null;
            float bestSqr = range * range;
            foreach (var e in EnemyController.Active)
            {
                if (e.IsDead) continue;
                Vector3 d = e.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = e; }
            }
            return best;
        }
    }
}
