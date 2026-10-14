# Drillex UI / Audio / VFX / Electric Block: implementation report

Branch: `ui-overhaul`. Commit `d50f8f1` holds this work and the layout fix after it. Commit `7a7b2dd` is the separate Crew thread's work.
Your uncommitted material, font and ProjectSettings/EditorBuildSettings edits were left as they were. The one project-setting change I made is `EditorSettings.asset`, where the sprite packer is now set to "Sprite Atlas V1 – Always Enabled" so the UI atlas actually packs.

## 1. HUD (Lev1)
Rebuilt by `Drillex/UI/Rebuild Lev1 HUD` (`Assets/Editor/DrillexUI/DrillexHudBuilder.cs`), with runtime logic in `HUDController`. Everything reads the existing managers. Text is only rewritten when a value changes.
- **Total orbs**: `Manager.permanentOrb` with a rolling animated counter, a glowing orb icon and a punch animation on gain.
- **Level / XP**: `ExperienceManager` shows level, current/needed XP and an animated bar that refills from empty on level-up.
- **Orbs in train**: `TrainStorageManager.currentOrb / totalStorage`, with a gauge that turns amber at 75% and red plus a flashing **FULL** tag at capacity.
- **Hull**: `TrainHealth`. The old world-space bar on the train is replaced by this HUD gauge.
- **Noise**: `Manager.currentNoise / maxNoise`, shown as "noise building up" toward the worm.
- **Compass**: restyled, keeping `SafeZoneCompass`.
- **Pause** and **Upgrade** buttons: industrial plates with press feedback. Upgrade pulses gently and shows a "!" badge when something is affordable.
- **Ability button**: circular. It finds the equipped power through a new `IHudAbility` interface, which `TrapDeployer` implements. It shows a radial cooldown, a countdown, an ACTIVE state while a trap exists, and an icon and colour per ability id (sound trap / fire / electric / missile).
- **Touch steering**: `SteerJoystick` writes `MobileInput.Steer`, which `TrainMovement` **adds** to `Input.GetAxis("Horizontal")` and clamps. Keyboard steering and auto-forward movement are unchanged.
- **Other**: safe-area fitting, toasts (level-up, unlocks, purchases, storage full), and a restyled worm warning that no longer blocks touches.

## 2. Main menu
`Drillex/UI/Rebuild Main Menu`. It has a live 3D stage with the real drill head plus Tesla, Rocket, Gatling and Storage cars, DesertKit props, Lev1's sun/fog/post profile, drifting dust and a slow camera drift. Buttons: PLAY (existing `SceneLoader.StartGame`, now with a fade), CREW (the other thread's work), SETTINGS, HOW TO PLAY (the existing help panel, restyled) and EXIT (hidden on iOS/WebGL). The entrance animation is staggered.

## 3. Pause menu
RESUME / RESTART / SETTINGS / MAIN MENU, with an animated panel and the HUD dimmed. Pausing stops time, pauses gameplay audio and ducks the music. **Pause, workshop and help no longer resume each other**:
- `PauseManager` restores the time scale it found.
- `UpgradeStation.CloseUpgrade` only resumes time when the game isn't paused. If it closes while paused, it tells the pause manager to resume to 1.
- Esc / Android back toggles pause and closes settings first.

Restart reloads the level the same way Abandon does.

## 4. Settings and audio
Settings has a shared prefab (`Assets/UI/Prefabs/UI_SettingsPanel.prefab`) with Master / Music / SFX sliders and Music / SFX on-off switches.

`AudioManager` is created once before the first scene and is duplicate-safe. Master volume maps to `AudioListener.volume`, music to two crossfading sources, and SFX to a 12-source pool plus the flamethrower loop. It saves to PlayerPrefs (`drx.audio.*`) and restores at launch.

The Abandon flow called `PlayerPrefs.DeleteAll()`. That call is removed: PlayerPrefs only holds settings, and the run state lives in the scene. No progression save system was added, and progression is still **not** persistent.

## 5. Music and sound sources (all CC0, details in `THIRD_PARTY_ASSETS.md`)
- Menu: "Desert Loop" by iamoneabe (OpenGameArt). Gameplay: "Eastern Arctic Dubstep" by VishwaJai (OpenGameArt).
- UI, explosion, rocket, electric and impact sounds: Kenney Interface / Sci-Fi / Impact packs. Only the 17 files used were copied.
- Cannon, twin cannon, gatling, flamethrower loop, trap ping and worm siren are synthesised in-editor. No CC0 recording fitted these.
- I did not use one candidate desert track: it was labelled CC0 but quotes a well-known film theme.

## 6. Weapon VFX (`Drillex/VFX/Build Weapon VFX…`)
- **Rocket**: white-orange muzzle flash with backblast smoke. The rocket uses the launcher's own missile mesh, a flame exhaust, a world-space smoke trail, embers and a trail renderer. On impact or timeout, the trails detach and fade out cleanly. The explosion has flash, fireball, core, sparks, smoke, debris, a ground shockwave and a dust ring. It is pooled, and the damage radius is unchanged.
- **Electric**: blue-white flash, an alpha-blended blue body (so it reads on bright sand), procedural branching arcs (`LightningBurst`) and sparks. The bolt projectile has a hot core, orbiting arcs and a blue trail. The impact has a discharge of 6 branching arcs, a flash and a residual glow. Idle arcs crackle on the Tesla car's coils.
- **Gatling**: small synced muzzle flash, a single forward burst and sparks per shot. Light haze builds up on about 35% of shots. Tracer trails replace the mesh. Bullets **and** impacts are pooled.
- **Cannon / Twin cannon**: larger orange flash with smoke (the twin adds side flares) and orange tracers with a glow.
- **Flamethrower**: rising embers and heat smoke added to the existing flame, plus a looping roar.
- Mobile notes: no lights were added. Particles are capped per system. Effects use unlit URP particle shaders with GPU instancing and 128 px ASTC textures.

## 7. Electric Gun Block
This finishes the existing `Tesla Cannon.prefab` (the `DRL_TeslaCar` art) rather than building a new block. It uses the new `AutoGun.WeaponType.Electric` with the new projectile `Assets/Prefeb/ElectricBolt.prefab` (a homing `Bullet`). Stats: 28 damage, 0.7 s per shot, range 32. These are new values, since the car never had electric stats; they put it between the Gatling and Rocket in damage per second.

It is wired into `UpgradeManager` through `electricGunPrefab`, `electricGunCost` (1,750), `electricGunUnlockLevel` (11), `electricGunBonus` and `BuyElectricGun()`. It goes through the same `IsUnlocked`/`SpendOrb`/`AddTrainBlock` path as every other car.

## 8. Progression
The unlock rule in code is `currentLevel >= unlockLevel`. Existing scene values were not changed: Storage 3, Cannon 4, Twin 6, Gatling 8, Flamethrower 10, Rocket 12. Electric is set to 11. `UpgradeManager` also enforces Electric < Rocket in `Awake`/`OnValidate`. The workshop orders cars by unlock level and shows "NEXT UNLOCK".

## 9. What changed
- **Gameplay scripts** (small additive edits): AutoGun, Bullet, RocketBullet, FlameThrower, TrainMovement, TrainHealth, TrapDeployer, UpgradeManager, UpgradeStation, PauseManager, SceneLoader, GameOverButton, GameOverManager.
- **New scripts**: `Assets/Script/UI/*`, `Assets/Script/Audio/*`, `Assets/Script/VFX/*`.
- **Editor tools**: `Assets/Editor/Drillex{UI,Audio,VFX,QA}`.
- **Prefabs**: Basic Gun, Double barrel gun, mini machine gun, rocket launcher, Tesla Cannon, FlameThrower, Rocket, basicbulle, Machinebullet, DRL_DrillVehicle_Train, plus the new ElectricBolt.
- **Scenes**: Lev1 and MainMenu.
- **New assets**: `Assets/UI` (sprites, atlas, settings prefab, logo material), `Assets/Art/VFX`, `Assets/Audio`, `Assets/Resources/DrillexAudioLibrary.asset`.
- **Behaviour notes**:
  - A rocket that touches two enemies in the same physics step still applies damage twice, exactly as before. Only the explosion effect and sound play once.
  - The workshop greys out Repair when the hull is already full, so orbs aren't spent for nothing.

## 10. Tests actually run (automated, in Play mode)
`Drillex/QA/Run Full Playtest`, last run: **59 passed, 0 failed, 0 console errors**. It covers:
- Menu: music plays on a single source, settings open, the sliders and toggles drive AudioManager and save to PlayerPrefs, help pauses and resumes time.
- PLAY loads Lev1 with one AudioManager, gameplay music only, and settings carried across scenes.
- Every HUD value matches the gameplay value and updates live.
- The ability button binds to the Sound Trap. Touch steering turns the train and auto-forward continues.
- Pause stops time and the train, pauses SFX and keeps the music. Settings opened from pause works. Resume works.
- Panel sequences: pause/resume over the workshop stays paused, and closing the workshop while paused stays paused until resume. This test found a real bug, which is fixed.
- Progression at Lv 10, 11 and 12: Electric and Rocket locked → Electric only → both. A buy at Lv 10 is refused by UpgradeManager. Buying Electric charges exactly 1,750 and couples one Tesla car, shown as EQUIPPED x1. Rocket, Gatling, Flamethrower and Twin purchases work. "Not enough orbs" changes nothing.
- Combat: electric bolts fire (not basic bullets), the Tesla AutoGun uses Electric with ElectricBolt, gatling bullets stay pooled (6 instances during sustained fire), and detached rocket trails clean up.
- Restart, game over, Abandon (audio prefs kept) and back to Main Menu with menu music.

`Drillex/QA/Run Weapons Test`: Rocket, Electric, Gatling, Twin and Flamethrower cars each attach and fire at an enemy, with 0 errors. Rocket firing and its pooled explosion are verified here, because in the crowded full test the other cars kill everything before the 4 s rocket reload.

`Drillex/QA/Run VFX Gallery` gives close-up captures of every effect. Screenshots are in `SourceArt/UI/_preview/` (not committed).

## 11. Not verified / manual steps
- **Keyboard steering**: key presses can't be simulated from the harness. The code path is unchanged; press A/D once.
- **Listening**: I can't hear audio. I verified that the right clips play, don't duplicate and follow the volume settings, but please listen to the music choices and the synthesised gun sounds. Swapping a clip only needs `Assets/Resources/DrillexAudioLibrary.asset`.
- **Device performance**: no device build or profiler capture yet. The design choices are mobile-safe, but an on-device check of FPS and overdraw during heavy fights is still needed.
- **Font licence**: Conthrax SemiBold was already in the project. Confirm its licence covers embedding in a commercial app.
- Enemies are still placeholder capsules with large red world-space health bars. I didn't restyle them.

## 12. Build
No player build was produced. The project compiles in the editor with 0 errors.
