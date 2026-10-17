# MASTER PROMPT — UNITY CINEMATIC SURVIVAL GAME PoC

> **Cómo usar este documento en Project Ridge**
>
> Este spec es la **meta final** del vertical slice, no una orden para construirlo todo de una vez.
> - Se trabaja **por hitos (M1–M8)** según `CLAUDE.md`. Cada hito cierra con su grupo de pruebas de la sección 22.
> - Dos carriles en paralelo: **sistemas en greybox** (M1–M5) y **look dev** (una esquina del bosque con arte e iluminación finales). El layout de The Hollow se viste con arte cuando ya funciona en greybox. El greybox nunca es el resultado final (sección 19).
> - **Decisión de armas para el PoC:** melee silencioso (garrote de madera con durabilidad) + **pistola de bengalas** como arma ruidosa. La bengala disparada a distancia cae, arde unos segundos con luz y ruido, y atrae a los enemigos (distracción). A quemarropa aturde. Munición muy limitada (2–3 en todo el nivel). Donde este documento dice "firearm", aplica a la pistola de bengalas. El revólver queda para el juego completo.
> - Si algo de este spec choca con `CLAUDE.md` o con una instrucción directa de Andres, pregunta antes de actuar.

## 1. YOUR ROLE AND PRIMARY OBJECTIVE

Act as a senior Unity game developer, technical artist, gameplay programmer, AI engineer, level designer, sound designer, and mobile optimization specialist.

Your task is to build a **fully playable, visually compelling proof of concept for an atmospheric, isometric 3D survival game for iPhone and iPad**.

I am a solo indie developer. I previously completed a Mahjong-meets-Poker roguelike called Yama using an AI-assisted workflow with Claude Code, GitHub, Unity/Xcode tools where applicable, and AI-generated visual assets.

I want to use a similar AI-assisted workflow for this project, but the visual requirements are substantially higher. This is not a prototype where gray boxes, primitive capsules, placeholder animations, or a technically working but visually empty environment are acceptable as the final result.

I want a compact vertical slice that feels like a small section of a finished commercial game.

The proof of concept must demonstrate:

- Convincing isometric 3D presentation.
- A complete, atmospheric environment.
- A believable, animated adult male protagonist.
- Responsive movement and character animation.
- Enemy AI with patrol, investigation, search, pursuit, and attack behaviors.
- A functional stealth system.
- UFO searchlights that dynamically affect gameplay.
- Combat, weapons, ammunition, resources, and pickups.
- Environmental storytelling.
- Spatial audio, sound propagation, and atmospheric effects.
- A complete playable objective and ending.
- Reliable performance on iPhone and iPad.
- A project structure that can be extended into a much larger narrative game.

**Do not merely explain how to make this game. Build it inside the current Unity project, run it, test it, and fix the problems you find.**

Work autonomously wherever reasonable. Make sensible design decisions without repeatedly asking for approval. Prioritize a polished, complete vertical slice over a sprawling collection of unfinished systems.

If a feature is too expensive or complex for this PoC, implement a convincing simplified version rather than leaving it out entirely.

## 2. INSPECT THE EXISTING PROJECT FIRST

Before changing anything:

1. Inspect the existing Unity project, Unity version, render pipeline, scenes, assets, scripts, packages, and build settings.
2. Identify any existing systems worth preserving.
3. Check whether the project uses Universal Render Pipeline (URP).
4. Verify the available tools, asset-generation capabilities, installed packages, and editor automation options.
5. Create or update a sensible project structure before implementing new systems.
6. Avoid unnecessarily replacing working systems or deleting existing assets.
7. Use the current Unity version and compatible APIs whenever possible.

If URP is already configured, preserve and improve it. If the project uses another pipeline, evaluate the safest migration path before changing it.

Use built-in Unity functionality and free tools wherever practical. Do not make the project dependent on paid plugins, subscription services, or inaccessible external assets.

If an external asset or tool is unavailable, use an appropriate alternative and continue.

Do not claim that a scene builds, a test passes, an animation works, or a device target performs correctly unless you have actually verified it.

## 3. GAME VISION — THE FATHER IS ALONE

The game is a cinematic, atmospheric, isometric survival experience set in a remote mountain forest at night.

The protagonist is a father searching for his missing family after a frightening, unexplained encounter involving strange creatures and unidentified flying objects.

The full game will eventually feature multiple interconnected areas, narrative revelations, environmental discoveries, and a larger mystery.

However, **this proof of concept covers only the father, who is completely alone.**

His wife and children are missing and must be discovered in later areas of the full game.

### Mandatory narrative constraints

- The father is the only human character who appears in the playable PoC.
- Do not include his wife or children as NPCs, followers, AI companions, playable characters, background characters, or visible figures.
- Do not implement companion-following systems, companion commands, companion animations, companion health, or companion UI.
- Do not show the family physically in the level, including in cinematic sequences, reflections, distant silhouettes, or scripted encounters.
- Do not reveal the family's location or fate.
- The father may discover environmental clues connected to them, such as a family photograph, a child's abandoned belonging, an interrupted campsite, or a familiar object.
- These clues must suggest a personal story without resolving the mystery.
- The PoC must have a satisfying playable objective and conclusion even though the family is not found during this level.

The emotional foundation is isolation, uncertainty, fear, vulnerability, and determination.

The player should feel that this man is trying to survive while following traces of something deeply personal.

The environment and gameplay must carry the story. Avoid excessive exposition, dialogue, or explanatory text.

## 4. VISUAL DIRECTION

### Overall aesthetic

Create a visually rich, cinematic, stylized-realistic 3D world.

The visual target combines:

- The readability and clear environmental composition of a high-quality stylized isometric adventure.
- The grounded material quality, lighting, environmental storytelling, and tension of a serious survival thriller.
- The restrained camera movement and deliberate framing of an interactive cinematic experience.

Do not copy specific copyrighted characters, environments, or assets from existing games. Use them only as broad visual-quality references.

The result should feel mature, atmospheric, and believable rather than cute, toy-like, or cartoonish.

### Color palette

Use a restrained, mostly desaturated palette:

- Deep forest green.
- Charcoal and near-black.
- Wet earth brown.
- Muted gray stone.
- Cold blue-gray atmospheric haze.
- Subdued moss green.
- Occasional pale cyan or cold white illumination.
- Very restrained crimson accents from alien eyes or distant phenomena.

The environment should remain dark and subdued, while strategically placed light sources create contrast and visual interest.

Avoid excessive saturation, colorful fantasy vegetation, neon cyberpunk effects, bright magical glows, or an abundance of competing colors.

### Environmental materials

Use believable, varied materials:

- Wet bark and tree roots.
- Moss-covered stones.
- Muddy trails.
- Damp grass and undergrowth.
- Rough wood.
- Weathered metal.
- Waterlogged soil.
- Fog-softened distant trees.
- Small puddles and wet surfaces.

Where practical, implement convincing roughness variation, normal detail, subtle surface highlights, and material variation.

Avoid making every surface uniformly glossy or uniformly rough.

### Lighting

Lighting is central to the experience.

Implement a deliberate lighting setup using URP-compatible techniques:

- A subdued moonlit or overcast nighttime environment.
- Strong environmental silhouettes.
- Selective local illumination.
- Volumetric-looking fog or an inexpensive mobile-compatible approximation.
- Controlled contrast between illuminated areas and deep shadow.
- Wet surfaces that catch light.
- UFO illumination that visibly changes the appearance of the environment.
- Restrained post-processing, including color grading, subtle bloom, and vignette where performance permits.

The player should be able to understand the environment while still feeling threatened by darkness.

Do not make the level so dark that gameplay becomes frustrating.

Do not rely on a single full-screen darkness overlay to create atmosphere.

### Character and creature materials

The father must look like a believable adult in practical outdoor clothing.

The creatures must look physically integrated into the environment, with appropriate scale, lighting, shadows, and materials.

Avoid generic shiny plastic materials, flat unlit characters, floating objects, visible seams, and obviously primitive placeholder geometry.

## 5. CAMERA AND PRESENTATION

Implement a stable isometric or three-quarter top-down camera.

Suggested starting parameters:

- Camera angle: approximately 35–50 degrees downward.
- Perspective or carefully tuned orthographic projection, whichever produces the strongest result.
- The protagonist remains clearly visible during movement and combat.
- Camera framing should show enough surrounding terrain to support stealth decisions.
- Camera movement should be smooth and restrained.
- Avoid excessive rotation, shaking, or cinematic movement that interferes with gameplay.

The camera should feel intentional, like a composed shot rather than a generic strategy-game camera.

Implement occlusion handling for trees and large foreground objects. When an object blocks the protagonist, use an appropriate fade, transparency, or camera adjustment so the player does not lose track of the character.

Ensure that transparency effects do not create distracting visual artifacts.

If feasible, add subtle camera impulse for impacts, attacks, and major environmental events. Keep it optional and restrained.

## 6. LEVEL DESIGN — "THE HOLLOW"

Create one complete, compact level named **THE HOLLOW**.

The setting is a remote mountain forest shortly after rainfall, at night.

The player begins alone near the remains of an abandoned ranger shelter. He must navigate a hostile environment, discover evidence connected to his missing family, survive alien encounters, and reach a final exit or extraction point.

### Required locations and environmental elements

Build a coherent environment containing:

1. **Starting area:** an abandoned ranger shelter, damaged wooden structure, scattered supplies, and signs of a recent disturbance.
2. **Forest trail:** a muddy, winding path with dense trees, rocks, roots, and natural obstructions.
3. **Wooden crossing:** a small bridge or narrow crossing over a stream or ravine.
4. **Hiding areas:** large rocks, fallen logs, tree clusters, vegetation, and other meaningful cover.
5. **Resource caches:** small supplies or containers placed where exploration is rewarded.
6. **Family clue:** an inspectable personal object or photograph suggesting that the father has been following his family's trail.
7. **UFO searchlight zone:** an exposed clearing where the player can observe and evade a moving beam of light.
8. **Alien encounter area:** a location where enemy patrols, sound, cover, and line of sight create meaningful stealth decisions.
9. **Final objective area:** a damaged access road, radio station, abandoned vehicle, trail marker, or similar destination that supports a believable end to the PoC.

Use terrain elevation and natural obstacles to create visual depth.

The level should have at least two meaningful routes through a significant portion of the environment. One can be shorter but more exposed, while another can be longer and offer better concealment.

### Level-design requirements

- Implement real colliders and navigable terrain.
- Ensure the player cannot walk through trees, rocks, buildings, or other solid objects.
- Ensure enemies can navigate the intended routes.
- Use cover and elevation to affect visibility and movement.
- Avoid giant empty spaces that have no gameplay purpose.
- Avoid repetitive placement of identical assets.
- Use lighting, composition, and environmental details to guide the player naturally.
- Place resources and clues deliberately rather than randomly.
- Ensure the level is fully traversable and can be completed without developer intervention.

If a fully custom terrain is impractical, construct a convincing modular environment from high-quality compatible assets. The final presentation must not resemble a collection of Unity primitives.

## 7. PLAYER CHARACTER — SOLO SURVIVOR

The protagonist is a single adult male in his late thirties, dressed for outdoor travel in a remote mountain environment.

His clothing should be practical, subdued, and slightly worn. A backpack or small survival pack is appropriate.

He should look like an ordinary father caught in an extraordinary situation, not a heavily armed military operator or superhero.

### Character asset and rig

Use a suitable rigged 3D character model with a compatible humanoid skeleton.

Preferred workflow:

1. Use an available high-quality character model or create one through a suitable modeling or AI-assisted asset workflow.
2. Configure the rig correctly.
3. Import or create compatible animations.
4. Set up an Animator Controller with appropriate parameters and transitions.
5. Verify the animation in the actual game scene.

Do not leave the final character as a capsule, mannequin, static PNG, or unanimated placeholder.

If a fully bespoke character is not possible, use a suitable existing rigged model and improve its materials, clothing, lighting, and presentation.

### Required movement states

Implement:

- Idle.
- Walk.
- Jog.
- Sprint.
- Crouch.
- Crouch-walk.
- Start and stop transitions where available.
- Directional movement.
- Turn and rotation behavior.
- Melee attack.
- Weapon handling.
- Aiming and firing if a firearm is equipped.
- Hit reaction.
- Stagger or knockback where appropriate.
- Death.
- Recovery or respawn after death.

Use a blend tree or equivalent system for smooth locomotion.

Movement speed, acceleration, turning, and animation playback should feel responsive without looking mechanical.

The character should orient toward movement direction when appropriate, and toward the target when aiming or attacking.

### Player controls

Support both:

**Editor testing**
- WASD or arrow keys for movement.
- Mouse or a configurable keyboard control for aiming.
- Keyboard shortcuts for interaction, attack, and other actions.

**iPhone and iPad**
- A virtual movement joystick or equivalent touch-based movement control.
- A context-sensitive interaction button.
- Crouch and sprint controls.
- Attack and aim/fire controls when relevant.
- A pause button.

Touch controls must be large enough to use comfortably without covering the central gameplay area.

Do not force the player to use a keyboard for essential actions on iOS.

Use Unity's existing Input System if compatible with the project.

### Solo-character requirements

- No human companions.
- No companion AI.
- No companion-following logic.
- No companion-related interface.
- All player actions must work with a single protagonist.
- Environmental clues can be inspected or collected without another character being present.

## 8. ENEMY DESIGN — ALIEN CREATURES

Create one primary alien creature type with enough visual distinction and animation quality to make encounters memorable.

The creature should feel biologically wrong and difficult to understand.

### Visual design

Use an asymmetrical, unsettling silhouette:

- Unusually long or uneven limbs.
- An unnatural posture.
- A narrow, distorted, or difficult-to-read head silhouette.
- Irregular proportions.
- Dark, low-reflectance skin or an organic-looking surface.
- Sparse, small red eyes or similarly restrained crimson features.
- Subtle movement that feels physically unnatural.

Avoid conventional armored soldiers, robots, mechanical exoskeletons, recognizable sci-fi armor, weapons mounted on limbs, or overly familiar humanoid alien designs.

The creature should not look like a brightly colored fantasy monster.

Its visual identity should come from its silhouette, movement, scale, and the way light reveals it.

### Required animation states

- Idle.
- Slow patrol movement.
- Investigation.
- Search.
- Faster pursuit.
- Attack.
- Attack recovery.
- Hit reaction where applicable.
- Death or incapacitation if combat can eliminate it.

Use a suitable rigged model and actual animations.

If custom animation is unavailable, use compatible retargeted animations and modify the timing, posture, and movement to establish a distinctive identity.

Avoid sliding feet, sudden snapping between states, and floating movement.

### Enemy AI

Implement a state machine with these states:

`PATROL → INVESTIGATE → SEARCH → CHASE → ATTACK → SEARCH → PATROL`

The transitions should be based on actual game conditions.

**Patrol**
- Follow a defined route.
- Pause occasionally.
- Look or turn naturally when possible.

**Investigate**
- Respond to a sound or suspicious event.
- Move toward the approximate source.
- Avoid instantly knowing the player's exact location.

**Search**
- Search the last known position.
- Move around nearby cover.
- Turn, pause, and inspect the surroundings.
- Return to patrol after a configurable period without new evidence.

**Chase**
- Pursue the player when the creature has sufficient evidence of the player's location.
- Use navigation and obstacle avoidance.
- Stop chasing if the player breaks detection and the creature loses track of the player.

**Attack**
- Enter a suitable range.
- Play an attack animation.
- Apply damage during a defined active window.
- Include a recovery period to prevent continuous, frame-by-frame damage.

Use Unity AI Navigation/NavMesh or a compatible navigation solution.

Ensure the AI can move around terrain obstacles and cannot pass through walls, rocks, or solid environmental objects.

If the creature becomes stuck, implement a recovery behavior where practical.

Expose important parameters for testing, including patrol speed, investigation speed, chase speed, detection distance, hearing sensitivity, search duration, attack range, and damage.

## 9. STEALTH AND DETECTION SYSTEM

Stealth must be a real gameplay system, not simply a crouch animation.

Implement detection based on:

- Distance.
- Field of view.
- Line of sight.
- Occlusion by solid objects and terrain.
- Player movement speed.
- Crouching.
- Noise generated by player actions.
- UFO illumination where relevant.

### Vision

The enemy should detect the player when the player is within its effective vision range, inside its field of view, and not blocked by appropriate obstacles.

Use raycasts or an equivalent method for line-of-sight checks.

Trees, rocks, terrain, walls, and other intended occluders should affect detection.

Do not let enemies detect the player through solid walls or large rocks.

### Movement and visibility

- Walking creates relatively little noise.
- Sprinting creates more noise.
- Crouching reduces visibility and noise.
- Crouching behind suitable cover can break line of sight.
- Being in an exposed area makes detection more likely.
- The player should not become completely invisible simply because the crouch button is pressed.

Use a readable detection-feedback system, such as a subtle visual indicator, contextual audio, or a restrained UI cue.

Do not fill the screen with unnecessary stealth meters.

### Detection feedback

Give the player a fair warning when an enemy begins to notice him.

For example:

- A creature pauses or turns toward a sound.
- Its posture changes when suspicious.
- It investigates a location before fully committing to a chase.
- Audio cues indicate increasing danger.

Avoid instant detection without a plausible cause.

## 10. UFO SEARCHLIGHT SYSTEM

Create a threatening UFO or unidentified aerial object that casts a moving searchlight across part of the level.

The UFO should be a major environmental presence, not just a decorative object.

### Visual requirements

- A distant or partially obscured aerial silhouette.
- A moving cone of pale white or cold cyan-green illumination.
- Light visible through fog or atmospheric haze where feasible.
- A beam that illuminates the terrain and objects beneath it.
- A restrained, unsettling ambient sound.
- A slow and deliberate scanning motion.

The UFO does not need complex flight simulation. A controlled path or scanning pattern is sufficient.

### Gameplay requirements

The searchlight must affect player exposure.

Implement:

- Beam direction and movement.
- Beam angle and effective range.
- Detection checks.
- Occlusion by terrain and appropriate opaque cover.
- An exposure or detection buildup system.
- A warning when the player is exposed.
- A hostile response when exposure exceeds a configurable threshold.
- Exposure reduction when the player leaves the beam or enters effective cover.

Use raycasts and geometric checks rather than relying only on visual overlap with a light cone.

The player should be able to hide behind suitable rocks, terrain features, or solid structures.

Crouching may reduce exposure in certain situations but must not negate direct illumination in an open area.

The beam must not detect the player through solid terrain or objects that should block it.

### UFO behavior

For the PoC, implement a simple but convincing scanning routine:

1. Sweep across an exposed area.
2. Pause briefly at one or more points.
3. Reverse or change direction.
4. React when the player is detected.
5. Trigger a clear but restrained escalation event.

The escalation can cause a creature to investigate, redirect a nearby patrol, or activate a threatening audio cue.

Keep the system modular so future levels can introduce different UFO behaviors.

## 11. SOUND PROPAGATION AND AUDIO DESIGN

Audio must contribute directly to both atmosphere and gameplay.

Implement a centralized sound-event system.

Each event should contain, where applicable:

- World position.
- Loudness or effective radius.
- Event category.
- Lifetime.
- Source identity.
- Optional obstruction or attenuation behavior.

### Player-generated sounds

Account for:

- Walking footsteps.
- Sprinting footsteps.
- Crouch movement.
- Stepping on noisy debris.
- Opening or searching containers.
- Picking up resources.
- Weapon swings.
- Weapon impacts.
- Weapon breakage.
- Gunshots.
- Reloading or handling equipment where appropriate.

Different actions should generate different levels of noise.

### Enemy and environmental sounds

Include:

- Creature vocalizations or breathing.
- Footsteps and movement.
- Search and attack sounds.
- Distant forest ambience.
- Wind through trees.
- Water and stream ambience.
- Rain-drip or wet-environment details.
- Distant, low-frequency UFO ambience.
- Searchlight or detection-related sound cues.
- Appropriate impact and interaction sounds.

### AI sound response

Enemies must be able to hear relevant sound events and investigate their approximate source.

A loud gunshot should attract attention from a greater distance than quiet walking.

Environmental obstacles should attenuate sound where practical.

Do not make every enemy instantly aware of the player's exact location after a single sound.

Use a centralized audio manager with separate mixer groups or equivalent routing for:

- Music.
- Ambient environment.
- Player sounds.
- Enemy sounds.
- Weapons and impacts.
- UFO effects.
- UI.

Support pause and volume control.

If original sound assets cannot be generated, use suitable legally available assets or procedural approximations. Do not leave important gameplay events silent.

## 12. COMBAT AND WEAPONS

Combat is dangerous, resource-limited, and secondary to stealth.

The player should be able to fight, but the game should not encourage mindless aggression.

Implement at least one melee weapon and, if feasible within the available assets and schedule, one firearm.

### Melee weapon

Implement one or both of the following:

**Wooden club**
- Relatively quiet.
- Moderate damage.
- Limited durability.
- Can break after repeated use.
- Breakage creates a distinct sound that can attract nearby enemies.

**Metal pipe**
- Greater damage or durability.
- Louder impacts.
- Distinctive attack animation and sound.
- Appropriate limitations to prevent it from dominating combat.

If time is limited, one polished melee weapon is preferable to two poorly implemented ones.

### Melee mechanics

Implement:

- Attack startup.
- Active hit window.
- Recovery.
- Attack cooldown.
- Range and hit detection.
- Damage application.
- Impact feedback.
- Durability reduction.
- Weapon breakage where relevant.

Do not apply damage every frame while the attack animation overlaps an enemy.

Ensure one swing cannot unintentionally register multiple hits unless explicitly designed to do so.

### Firearm

If a suitable weapon asset and animation are available, implement a basic firearm with:

- Aim.
- Fire.
- Limited ammunition.
- Reloading.
- Muzzle flash.
- Impact feedback.
- Appropriate sound.
- Recoil or a restrained camera impulse.
- Hitscan or projectile-based hit detection.
- Enemy damage and death or incapacitation.
- Noise propagation.

Gunfire must be significantly louder than ordinary movement and should influence enemy behavior.

Do not require the firearm for level completion. The player must be able to complete the PoC through stealth and evasion.

### Combat balance

The player should feel vulnerable.

- Enemies should be threatening at close range.
- Combat should have readable telegraphs.
- Attacks should have clear timing.
- Damage should be communicated through animation, sound, and restrained visual feedback.
- Avoid excessive gore or unnecessarily graphic effects.
- The player should have a reasonable opportunity to escape after a mistake.

## 13. RESOURCES, PICKUPS, AND INVENTORY

Implement a compact survival-resource system.

Required resources:

- Ammunition, if firearms are implemented.
- Medical supplies.
- Salvage or crafting material.
- Weapon durability.

### Interactions

The player should be able to:

- Collect visible pickups.
- Search suitable containers.
- Inspect a narrative clue.
- Recover limited supplies.
- Receive immediate feedback when an item is collected.

Place supplies strategically so exploration has value.

Use environmental storytelling to communicate why supplies are present.

### Inventory

Keep the system deliberately small for this PoC.

Display only the information the player needs:

- Current health.
- Equipped weapon.
- Ammunition, if relevant.
- Weapon durability, if relevant.
- Selected resource counts.

Avoid a complicated grid inventory, extensive crafting trees, or elaborate equipment management unless they can be implemented without undermining the rest of the PoC.

### Survival balance

The player should never need to fight every enemy to progress.

Scarcity should create meaningful choices between fighting, avoiding, and searching.

## 14. ENVIRONMENTAL STORYTELLING

Narrative discoveries are essential because the family does not appear in this PoC.

Include at least three distinct environmental storytelling moments, preferably integrated into the level rather than presented as cutscenes.

Examples:

1. A family photograph or personal object at the abandoned shelter.
2. Evidence that someone passed through the forest in a hurry.
3. A child's belonging or a familiar family item near a dangerous route.
4. An abandoned camp with signs of an interrupted departure.
5. A radio message or environmental clue suggesting that the situation is larger than the protagonist understands.

Select at least three that fit naturally into the level.

### Narrative rules

- Do not show the family.
- Do not use family members as NPCs or companions.
- Do not reveal their fate.
- Do not explain the entire alien mystery.
- Do not force long dialogue sequences.
- Do not use excessive written exposition.

Clues should be brief, specific, and emotionally meaningful.

Allow the player to inspect a clue using the interaction system.

Where appropriate, use a short contextual text, a small visual close-up, or a restrained audio reaction from the father.

Avoid turning every object into a collectible with a large UI notification.

## 15. OBJECTIVES AND LEVEL COMPLETION

The PoC must have a clear beginning, middle, and end.

### Beginning

The father starts near the abandoned ranger shelter.

The player receives a minimal contextual objective, such as:

"Find a way through the forest."

The environment should communicate the immediate situation without a lengthy tutorial.

### Middle

The player explores the forest, discovers clues, gathers supplies, and encounters the alien threat.

The UFO searchlight should create at least one deliberate stealth challenge.

At least one enemy encounter should allow the player to choose between a more exposed route and a more concealed route.

### End

The player reaches a believable exit, such as a damaged access road, abandoned radio station, or trail beyond the immediate danger zone.

Trigger a short ending sequence or gameplay event that provides a sense of completion while preserving the larger mystery.

For example, the player may find evidence that the trail continues beyond the current area or hear an unexplained transmission.

Do not reveal where the family is.

Provide a restart option after completion.

The ending should feel intentional, not like the player simply walked off the edge of the level.

## 16. USER INTERFACE AND FEEDBACK

Create a restrained interface appropriate for a cinematic survival game.

Required elements:

- Health indicator.
- Weapon and ammunition information when relevant.
- Resource counts.
- Context-sensitive interaction prompt.
- Objective indicator.
- Pause menu.
- Restart or return-to-menu functionality.
- Clear feedback when taking damage.
- Clear feedback when picking up items.
- Readable stealth or detection feedback.
- A simple death and recovery flow.
- A clear level-completion state.

### Visual style

Use subdued colors, restrained typography, and minimal screen clutter.

Do not use oversized cartoon icons, colorful fantasy inventory panels, excessive borders, or generic mobile-game styling.

The interface should support immersion rather than dominate the screen.

### Mobile usability

- Touch targets must be large enough for reliable interaction.
- Controls should be positioned for comfortable use on both iPhone and iPad.
- Respect screen safe areas.
- Support different aspect ratios.
- Avoid text that becomes too small on iPhone.
- Avoid controls that obscure enemies or the protagonist.

Provide a way to pause the game and adjust essential audio settings.

## 17. VISUAL EFFECTS AND CINEMATIC DETAILS

Use effects selectively to improve atmosphere and communicate gameplay.

Consider:

- Low-density atmospheric fog.
- Subtle wind movement in grass or foliage.
- Light rain residue or dripping water.
- Footstep particles or small ground interactions.
- Weapon impact effects.
- Muzzle flash if a firearm is implemented.
- Small debris effects.
- Restrained blood or hit effects where appropriate.
- UFO beam illumination.
- Subtle light shafts or a mobile-friendly approximation.
- Controlled camera impulse.
- Distant silhouettes revealed briefly by lighting.
- Small environmental particles visible in the searchlight.

Effects must be grounded in the environment.

Avoid excessive particle counts, constant screen-space effects, exaggerated explosions, magical glows, or unnecessary visual noise.

Prioritize effects that improve readability, tension, or perceived production quality.

## 18. TECHNICAL ARCHITECTURE

Create a clean, maintainable Unity project.

Use modular C# systems with clear responsibilities.

Suggested systems include:

- GameManager.
- LevelManager.
- PlayerController.
- PlayerInputController.
- PlayerHealth.
- PlayerCombat.
- PlayerInventory.
- WeaponController.
- Interactable.
- Pickup.
- EnemyController.
- EnemyStateMachine.
- EnemyPerception.
- SoundEventManager.
- StealthSystem.
- UFOController.
- UFOExposureSystem.
- ObjectiveManager.
- CheckpointManager.
- AudioManager.
- UIManager.
- Save or checkpoint persistence if appropriate.

These are suggested responsibilities, not a requirement to create a separate script for every item. Combine or split systems where it makes the project simpler and more reliable.

### Architecture requirements

- Avoid one giant script containing all gameplay logic.
- Avoid excessive coupling between unrelated systems.
- Use serialized fields or ScriptableObjects for important tuning values where appropriate.
- Use interfaces or events for interactions where they materially improve maintainability.
- Avoid unnecessary per-frame allocations.
- Avoid repeated expensive scene searches during gameplay.
- Use object pooling for frequently spawned effects if needed.
- Add comments where logic is non-obvious.
- Provide sensible default values.
- Make systems testable and configurable in the Inspector.
- Keep future levels and enemy types in mind without overengineering the PoC.

Do not create abstractions merely for their own sake.

## 19. ART ASSET PIPELINE

Use the best available workflow for producing convincing visuals with reasonable cost and complexity.

Potential tools include:

- Unity Asset Store free assets or other legally usable asset sources.
- Blender for asset adjustments, collision meshes, and simple modeling.
- AI-assisted 3D asset creation when available.
- Rigged character assets with compatible animations.
- Animation retargeting tools where appropriate.
- Custom materials, lighting, and post-processing.
- Procedural placement tools for vegetation and environmental details.

Do not assume any paid service or tool is available.

### Asset requirements

- Keep scale consistent across the environment.
- Verify materials and textures in the actual URP scene.
- Ensure models have sensible pivots and orientations.
- Verify animation compatibility before building systems around an asset.
- Ensure imported assets have suitable colliders or add them.
- Use LODs or simplified meshes where appropriate.
- Compress textures for the target device.
- Avoid importing unnecessarily large source textures into the mobile build.
- Keep third-party assets organized and their licenses identifiable.

### Visual-quality fallback

If a perfect custom asset cannot be produced, prioritize:

- Strong silhouette.
- Correct scale.
- Good materials.
- Convincing lighting.
- Appropriate animation.
- Cohesive art direction.

A modest model with good lighting and animation is better than an expensive-looking model that does not fit the world.

Never present obvious gray-box geometry as the finished art direction.

## 20. MOBILE PERFORMANCE AND IOS REQUIREMENTS

The target platform is iPhone and iPad.

Build with URP and mobile performance in mind.

### Performance goals

- Target 60 FPS on capable devices.
- Maintain a stable 30 FPS minimum on the agreed representative target device.
- Avoid major frame-time spikes during enemy encounters, combat, or lighting changes.
- Keep memory usage reasonable.
- Avoid unnecessary battery-draining effects.

Actual performance must be measured on a real device before claiming that the target has been achieved. If no device is available, report Editor measurements and the remaining verification requirements honestly.

### Optimization requirements

Use appropriate combinations of:

- LODs.
- Static batching or GPU instancing where appropriate.
- Occlusion culling where beneficial.
- Efficient collision and navigation setups.
- Controlled real-time shadow distance.
- Limited shadow-casting lights.
- Efficient foliage rendering.
- Texture compression.
- Appropriate texture resolution.
- Limited transparent surfaces and particle overdraw.
- Efficient AI update rates.
- Non-allocating physics queries where practical.
- Object pooling where useful.
- Reduced update frequency for distant or inactive AI.
- Mobile-compatible post-processing.
- Sensible quality settings.

Do not enable expensive features indiscriminately just because they improve a screenshot in the Editor.

### Graphics settings

Create at least one sensible mobile quality configuration.

Prioritize:

- Readable lighting.
- Stable frame rate.
- Consistent material appearance.
- Smooth character animation.
- Reasonable battery and thermal behavior.

Where possible, expose quality settings or provide separate profiles for lower- and higher-performance devices.

### iOS build readiness

Verify:

- The correct target platform.
- Compatible packages.
- Input handling on touch devices.
- Screen orientation and aspect ratio.
- Safe-area handling.
- Application pause and resume behavior.
- No reliance on Editor-only functionality during gameplay.
- No missing runtime assets.
- No blocking exceptions in the player logs.

Prepare the project for an iOS build and document any signing, provisioning, or Xcode steps that require my intervention.

Do not claim to have produced a successful device build unless that build has actually been completed and verified.

## 21. EDITOR AUTOMATION AND TESTING TOOLS

Where possible, use Unity Editor scripting to reduce manual setup.

Create tools that can:

- Build or validate the PoC scene.
- Verify required objects and references.
- Check for missing components.
- Check for missing materials or animation controllers.
- Validate essential player and enemy configuration.
- Check navigation setup.
- Check that UI elements are connected.
- Identify missing references and relevant console errors.

If you can use editor automation or an available Unity integration to create objects, configure scenes, assign assets, and run tests, use it.

Do not stop after writing scripts. Ensure the scripts are attached to appropriate GameObjects and their dependencies are configured.

Avoid creating duplicate managers, duplicate input systems, or multiple competing implementations of the same feature.

## 22. ACCEPTANCE TESTS

The PoC is not complete until the following tests have been executed or explicitly identified as blocked by the available environment.

### Project and level

- The project opens without critical compilation errors.
- The level loads successfully.
- The player spawns at the intended starting location.
- The level has appropriate lighting, materials, terrain, and environmental assets.
- The level can be completed from start to finish.
- Collision prevents the player from walking through intended solid objects.
- The player cannot become permanently stuck in normal traversal.

### Player

- Walking, jogging, sprinting, crouching, and turning work correctly.
- Character animations transition smoothly enough for normal play.
- Interaction and pickups work.
- Health, damage, death, and recovery work.
- The player can attack and weapons behave according to their configuration.
- The game can be controlled using keyboard and mouse in the Editor.
- Touch controls are configured for iOS and tested wherever an appropriate device or simulator is available.

### Enemy AI

- Enemies patrol their routes.
- Enemies investigate audible events.
- Enemies use line of sight and do not detect the player through intended solid cover.
- Enemies search the last known position instead of instantly tracking the player forever.
- Enemies pursue and attack under the correct conditions.
- Enemies return to a non-hostile state when appropriate.
- Enemies navigate around intended obstacles.

### Stealth and UFO

- Crouching changes the player's noise or detection profile.
- Sprinting generates more noise than quiet movement.
- Loud sounds attract enemies according to their effective range.
- The UFO searchlight moves through its intended scanning pattern.
- The beam can detect the player when exposed.
- Effective cover blocks or reduces exposure as designed.
- Exposure decreases when the player escapes the beam or enters suitable cover.
- Detection feedback communicates danger to the player.

### Resources and narrative

- Pickups update the inventory correctly.
- Resource quantities and weapon durability behave correctly.
- Environmental clues can be inspected.
- At least three narrative discoveries exist in the level.
- No family members appear in the PoC.
- No companion-following or companion-AI systems are required for completion.
- The level ends in a deliberate, playable completion state.
- The player can restart after completion.

### Presentation and reliability

- Audio plays for the essential gameplay events.
- UI elements remain readable across supported aspect ratios.
- Pause and resume work correctly.
- There are no recurring critical exceptions during a complete playthrough.
- Performance has been measured in the available environment, and limitations are documented.

Do not mark a test as passed simply because the code exists. Execute the test wherever possible.

## 23. DEVELOPMENT WORKFLOW

Work in the following order.

### Phase 1 — Inspect and plan

Inspect the existing project and report a concise implementation plan.

Identify:

- Existing assets worth reusing.
- Missing dependencies.
- Major risks.
- Available testing capabilities.
- The fastest path to a visually convincing vertical slice.

Do not spend excessive time writing documentation before building the game.

### Phase 2 — Build the visual foundation

Create the playable level, camera, lighting, environment, protagonist, and enemy visual setup.

At this stage, prioritize the overall visual impression.

The first playable scene should already have the intended art direction rather than looking like a generic Unity demo.

### Phase 3 — Implement core gameplay

Implement:

- Player movement.
- Character animation.
- Interaction and pickups.
- Enemy AI.
- Stealth.
- UFO scanning and exposure.
- Combat.
- Resources.
- Health and recovery.
- Objectives and completion.

Keep systems integrated into the actual scene.

### Phase 4 — Implement atmosphere and narrative

Add:

- Environmental clues.
- Sound propagation.
- Enemy audio.
- Forest ambience.
- UFO audio.
- Visual effects.
- Gameplay feedback.
- Final objective and ending.

### Phase 5 — Test and polish

Run the acceptance tests.

Fix:

- Compilation errors.
- Missing references.
- Broken animation transitions.
- AI navigation problems.
- Incorrect collision.
- Detection through solid cover.
- UI layout problems.
- Broken interactions.
- Runtime exceptions.
- Obvious visual inconsistencies.
- Performance problems that can be reproduced.

Repeat testing after important fixes.

### Phase 6 — Handoff

Provide a concise handoff explaining:

- What was built.
- How to open and play the level.
- The controls for Editor and iOS.
- Which systems are implemented.
- Which acceptance tests were actually executed and their results.
- Any remaining bugs or limitations.
- The steps required to build and run on an iPhone or iPad.
- Any external assets or packages used.
- The most important next steps for turning the PoC into a full game.

Do not claim completion if the level cannot be played from beginning to end.

## 24. FINAL QUALITY BAR

The goal is not to produce the largest amount of code in the shortest time.

The goal is to produce the strongest achievable small, playable, cinematic survival experience within the available environment and tooling.

Prioritize:

- A cohesive visual identity.
- A believable and animated solo protagonist.
- A convincing alien creature.
- A beautifully composed forest environment.
- Responsive and reliable movement.
- Meaningful stealth and enemy behavior.
- A genuinely threatening UFO searchlight.
- Sound that supports atmosphere and gameplay.
- Environmental storytelling about the missing family.
- A complete playable objective.
- Stable, maintainable implementation.
- Honest testing and a clear handoff.

Do not sacrifice the visual quality of the level to add unnecessary features.

Do not sacrifice playability to create a beautiful static scene.

Do not add family companions to make the level feel less empty. The father's isolation is intentional and central to the narrative.

If a feature must be simplified, simplify its implementation while preserving its intended player experience.

Begin by inspecting the existing project, then implement the complete vertical slice. Do not stop at a plan, a list of recommendations, or code snippets. Build, run, test, and improve the actual project to the fullest extent supported by the available tools.
