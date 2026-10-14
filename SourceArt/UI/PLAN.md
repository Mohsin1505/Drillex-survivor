# Drillex UI / Audio / VFX / Electric Block – Implementation Plan

## What the inspection found (Phase 1)

| System | Actual implementation |
|---|---|
| Scenes | `MainMenu.unity` (wallpaper image + Play/Help), `Lev1.unity` (gameplay). Loaded by name via `SceneLoader.StartGame()` → "Lev1", `PauseManager.MainMenu()` → "MainMenu". |
| HUD | One Screen Space Overlay canvas in Lev1 (1920×1080, match width). Debug-looking TMP texts (LiberationSans), default Unity sliders/knobs, default white buttons. |
| Train / movement | `TrainMovement` on `DRL_DrillVehicle_Train`: auto-forward, steering from legacy `Input.GetAxis("Horizontal")` only. No touch input exists. Blocks are spawned with `AddTrainBlock(prefab)` and follow the head. |
| Weapons | `AutoGun` (Basic / MachineGun / Rocket types) on Basic Gun, Double Barrel, Mini MG, Rocket Launcher cars and the head turret; `FlameThrower` on the Flame car. Bullets = `Bullet` (instantiated per shot, incl. 10 shots/s for the MG), rockets = `RocketBullet` (AoE OverlapSphere). Only the flamethrower has real VFX. |
| Electric | `Tesla Cannon.prefab` + `DRL_TeslaCar` art already exist but are **not wired**: it fires plain basic bullets and isn't in `UpgradeManager`. |
| Upgrades / unlocks | `UpgradeManager` (costs + unlock levels serialized in Lev1) + `UpgradeButtonUI` lock overlays. Block unlock levels in scene: Storage 3, Basic 4, Double 6, Mini MG 8, Flame 10, Rocket 12. Purchases spend `Manager.permanentOrb`. |
| Currency / XP | `Manager.permanentOrb` (total orbs), `TrainStorageManager.currentOrb/totalStorage` (orbs in train), `ExperienceManager` (level, XP, xpNeeded). Lev1 currently has dev values: 50,000,000 orbs and level 50. |
| Ability | One ability: Sound Trap via `TrapDeployer.DeployTrap()` with a 10 s cooldown (+ one active trap at a time). |
| Pause | `PauseManager` sets `timeScale` 0/1; upgrade panel and help panel also set timeScale directly. |
| Audio | **None.** No AudioSources, clips, mixer or audio manager anywhere in the project. |
| Save data | **None.** Nothing is persisted; `PlayerPrefs.DeleteAll()` is called on Abandon. Progress lives only in scene values. |

## Plan (reusing the existing scripts, adding only what's missing)

1. **UI kit** – generate a small set of industrial sprites (gunmetal 9-slice panels, bevelled buttons, hazard stripe, ring, icons) in-editor, pack into one SpriteAtlas (ASTC on mobile). Conthrax SemiBold for all text.
2. **HUD** – rebuild the Lev1 canvas: total orbs (animated counter + orb icon), level/XP bar with animated fill, orbs-in-train with near-capacity warning, hull bar, noise meter, compass, pause button, upgrade button (pulse when something is affordable), circular ability button driven by `TrapDeployer` (radial cooldown, active state), horizontal steer joystick feeding `TrainMovement`, safe-area fitter. All values read from the existing managers; only changed values touch the UI.
3. **Pause / Settings** – extend `PauseManager` (Resume, Restart, Settings, Main Menu; remembers previous timeScale so it doesn't fight the upgrade panel; pauses SFX, keeps music ducked). Shared Settings panel prefab used by both menus.
4. **Audio** – one persistent `AudioManager` (Master/Music/SFX volume + on/off, PlayerPrefs, restored at launch, duplicate-safe). Music and SFX are **original procedural tracks generated in-editor** (no third-party/copyrighted audio). `DeleteAll()` on Abandon will keep audio settings.
5. **Main menu** – replace the wallpaper with a live 3D stage: the real drill head + cars on desert-kit terrain, sunset light, fog, dust; title, PLAY / SETTINGS / HELP / EXIT (Exit hidden on iOS/WebGL), entrance + fade transitions.
6. **Weapon VFX** – per-weapon muzzle, projectile and impact prefabs; MG/basic bullets pooled; missile flame + smoke trail that detaches cleanly; new explosion (radius/damage unchanged); electric bolt with procedural branching arcs; flamethrower embers/smoke.
7. **Electric Gun Block** – finish `Tesla Cannon.prefab`: new `AutoGun.WeaponType.Electric`, electric bolt projectile, idle coil arcs, added to `UpgradeManager` (cost 1,750, unlock Lv 11 = after Flame Lv 10 and Mini MG Lv 8, before Rocket Lv 12; enforced so Electric can never unlock after Rocket).
8. **Upgrade menu** – redesigned workshop with card states (AVAILABLE / LOCKED Lv X / OWNED ×n / MAXED / NEED ORBS), Electric in blue, Rocket in orange-red, purchase and unlock feedback.
9. **Verify** – Play-mode tests of every flow, console check, screenshots, then report.

## Notes / decisions taken by default
- No save system exists, so "restart keeps permanent progress" and "existing players keep Rocket" can't be persisted today; I will not invent a save system. Restart reloads Lev1 like the existing Abandon flow does.
- The dev values (50M orbs, level 50) stay as they are; tests that need a fresh player set lower values at runtime only.
- Work goes on a `ui-overhaul` branch; your uncommitted material/font/ProjectSettings edits are left untouched.
