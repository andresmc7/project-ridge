using System.Collections.Generic;
using ProjectRidge.AI;
using ProjectRidge.Items;
using ProjectRidge.Player;
using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>
    /// Audio central: ambiente, música adaptativa (calma ↔ persecución con golpe de alerta), latido con vida baja o
    /// al ser cazado, y todos los eventos de juego (pasos, golpes, bengalas, Lurkers, OVNI, UI). Enruta por categoría
    /// con los volúmenes del AudioBank (equivalente a grupos de mixer); el volumen maestro es AudioListener.volume.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        [SerializeField] private AudioBank bank;
        [SerializeField] private PlayerController player;
        [SerializeField] private FatherAnimator fatherAnimator;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerInventory inventory;
        [Min(0f)] [SerializeField] private float alertStingCooldown = 8f;
        [Min(0.1f)] [SerializeField] private float musicFade = 1.5f;

        private AudioSource musicCalm, musicChase, wind, forest, heartbeat, ui, playerSfx;
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private int poolIndex;
        private float lastSting = -99f;
        private float lastHealth;
        private bool anyHunting;
        private readonly Dictionary<EnemyController, AudioSource> growls = new Dictionary<EnemyController, AudioSource>();

        private void Start()
        {
            musicCalm = Loop("MusicCalm", bank.musicAmbientLoop, bank.music);
            musicChase = Loop("MusicChase", bank.musicChaseLoop, 0f);
            wind = Loop("Wind", bank.windLoop, bank.ambient * 0.8f);
            forest = Loop("Forest", bank.forestLoop, bank.ambient * 0.6f);
            heartbeat = Loop("Heartbeat", bank.heartbeatLoop, 0f);
            ui = Source2D("UI");
            ui.ignoreListenerPause = true;
            playerSfx = Source2D("PlayerSfx");
            for (int i = 0; i < 10; i++)
            {
                var s = new GameObject("Sfx3D_" + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                Configure3D(s, 2f, 40f);
                pool.Add(s);
            }

            lastHealth = health.Current;
            fatherAnimator.Footstep += OnFootstep;
            combat.Swung += () => PlayerSfx(bank.clubSwing, bank.weapons * 0.6f, 0.08f);
            combat.ClubHit += pos => At(bank.clubHit, pos, bank.weapons, 0.06f);
            combat.ClubBroke += () => PlayerSfx(bank.clubBreak, bank.weapons, 0.04f);
            combat.Crafted += () => PlayerSfx(bank.craft, bank.player, 0.05f);
            combat.Fired += () => PlayerSfx(bank.flareShot, bank.weapons, 0.04f);
            combat.DryFire += () => PlayerSfx(bank.dryFire, bank.weapons * 0.7f, 0.05f);
            health.Damaged += _ => PlayerSfx(bank.hurt, bank.player, 0.08f);
            health.Died += () => PlayerSfx(bank.death, bank.player, 0f);
            inventory.Changed += OnInventory;

            foreach (var enemy in EnemyController.Active.ToArray()) HookEnemy(enemy);

            var flow = GameFlow.Instance;
            if (flow != null)
            {
                flow.StateChanged += OnFlowState;
                flow.CheckpointReached += () => UI(bank.checkpoint, 0.8f);
                flow.Captured += () => UI(bank.ufoCapture, 1f);
            }
        }

        private void HookEnemy(EnemyController enemy)
        {
            var voice = enemy.gameObject.AddComponent<AudioSource>();
            Configure3D(voice, 3f, 45f);
            var growl = enemy.gameObject.AddComponent<AudioSource>();
            Configure3D(growl, 2f, 30f);
            growl.clip = bank.lurkerGrowlLoop;
            growl.loop = true;
            growl.volume = 0f;
            growl.Play();
            growls[enemy] = growl;

            if (enemy.TryGetComponent(out LurkerAnimator anim))
            {
                anim.Footstep += () => Voice(voice, Pick(bank.lurkerSteps), bank.enemy * 0.55f, 0.12f);
                anim.Twitched += () => Voice(voice, Pick(bank.lurkerClicks), bank.enemy * 0.45f, 0.25f);
            }
            enemy.StateChanged += s =>
            {
                switch (s)
                {
                    case EnemyState.Investigate: Voice(voice, Pick(bank.lurkerClicks), bank.enemy * 0.8f, 0.1f); Sting(); break;
                    case EnemyState.Chase: Voice(voice, bank.lurkerScreech, bank.enemy, 0.08f); Sting(); break;
                    case EnemyState.Attack: Voice(voice, bank.lurkerAttack, bank.enemy, 0.1f); break;
                    case EnemyState.Dead: Voice(voice, bank.lurkerDeath, bank.enemy, 0.05f); break;
                }
            };
            if (enemy.TryGetComponent(out EnemyHealth eh)) eh.Damaged += _ => Voice(voice, bank.lurkerHurt, bank.enemy, 0.1f);
        }

        private void Update()
        {
            anyHunting = false;
            foreach (var kv in growls)
            {
                bool hunting = kv.Key != null && kv.Key.IsHunting;
                anyHunting |= hunting;
                if (kv.Value != null)
                    kv.Value.volume = Mathf.MoveTowards(kv.Value.volume, hunting ? bank.enemy * 0.8f : 0f, Time.unscaledDeltaTime * 1.5f);
            }

            bool ending = GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Ending;
            float fade = Time.unscaledDeltaTime / musicFade;
            musicChase.volume = Mathf.MoveTowards(musicChase.volume, anyHunting && !ending ? bank.music : 0f, fade);
            musicCalm.volume = Mathf.MoveTowards(musicCalm.volume, anyHunting || ending ? 0f : bank.music, fade * 0.5f);
            bool danger = anyHunting || health.Current < health.Max * 0.35f;
            heartbeat.volume = Mathf.MoveTowards(heartbeat.volume, danger && !health.IsDead ? bank.player * 0.7f : 0f, fade);
            heartbeat.pitch = anyHunting ? 1.25f : 1f;

            if (health.Current > lastHealth + 0.5f && lastHealth > 0f && !health.IsDead && lastHealth < health.Max)
                PlayerSfx(bank.heal, bank.player, 0f);
            lastHealth = health.Current;
        }

        private void OnFlowState(GameFlow.State s)
        {
            AudioListener.pause = s == GameFlow.State.Paused;
            if (s == GameFlow.State.Paused) UI(bank.uiClick, 0.6f);
            if (s == GameFlow.State.Clue) UI(bank.clueSting, 0.9f);
            if (s == GameFlow.State.Ending)
            {
                UI(bank.radioStatic, 0.8f);
                Invoke(nameof(PlayRadioVoice), 2.4f);
            }
        }

        private void PlayRadioVoice() => UI(bank.radioVoice, 0.9f);

        private void OnFootstep(bool left)
        {
            bool wood = Physics.Raycast(player.transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                        && hit.collider.name.EndsWith("Walk");
            var clip = Pick(wood ? bank.stepsWood : bank.stepsDirt);
            float v = player.Mode == MovementMode.Crouch ? 0.25f : player.Mode == MovementMode.Sprint ? 0.9f : player.Mode == MovementMode.Jog ? 0.65f : 0.45f;
            PlayerSfx(clip, bank.player * v, 0.12f);
        }

        private readonly Dictionary<ItemDefinition, int> lastCounts = new Dictionary<ItemDefinition, int>();
        private void OnInventory(ItemDefinition item, int count)
        {
            lastCounts.TryGetValue(item, out int before);
            lastCounts[item] = count;
            if (count > before) PlayerSfx(bank.pickup, bank.player * 0.8f, 0.1f); // solo al ganar, no al gastar
        }

        private void Sting()
        {
            if (Time.time - lastSting < alertStingCooldown) return;
            lastSting = Time.time;
            UI(bank.alertSting, bank.music * 1.3f);
        }

        // ---------- Utilidades

        private AudioSource Source2D(string name)
        {
            var s = new GameObject(name).AddComponent<AudioSource>();
            s.transform.SetParent(transform, false);
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        private AudioSource Loop(string name, AudioClip clip, float volume)
        {
            var s = Source2D(name);
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            if (clip != null) s.Play();
            return s;
        }

        private static void Configure3D(AudioSource s, float min, float max)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = min;
            s.maxDistance = max;
            s.dopplerLevel = 0f;
        }

        private static AudioClip Pick(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

        private void PlayerSfx(AudioClip clip, float volume, float pitchJitter)
        {
            if (clip == null) return;
            playerSfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            playerSfx.PlayOneShot(clip, volume);
        }

        private void UI(AudioClip clip, float volume)
        {
            if (clip != null) ui.PlayOneShot(clip, volume * bank.ui / 0.7f);
        }

        private void At(AudioClip clip, Vector3 pos, float volume, float pitchJitter)
        {
            if (clip == null) return;
            var s = pool[poolIndex];
            poolIndex = (poolIndex + 1) % pool.Count;
            s.transform.position = pos;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(clip, volume);
        }

        private static void Voice(AudioSource s, AudioClip clip, float volume, float pitchJitter)
        {
            if (clip == null || s == null) return;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(clip, volume);
        }
    }
}
