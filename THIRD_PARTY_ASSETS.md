# Drillex – third-party assets

Every external file added to the project, where it came from, and its licence.
All entries below are **CC0 1.0 (public domain dedication)**: free for commercial use (Google Play included), no attribution required. Credits are listed anyway as good practice.

| File in project | Asset / author | Source | Licence | Why it was chosen |
|---|---|---|---|---|
| `Assets/Audio/External/OpenGameArt/MUS_DesertLoop_iamoneabe.mp3` | "Desert Loop" by iamoneabe | https://opengameart.org/content/desert-loop | CC0 1.0 | Loopable desert/oriental VGM track for the main menu |
| `Assets/Audio/External/OpenGameArt/MUS_EasternArcticDubstep_VishwaJai.mp3` | "Eastern Arctic Dubstep" by VishwaJai | https://opengameart.org/content/eastern-arctic-dubstep | CC0 1.0 (built from public-domain samples per the author) | Energetic Middle-Eastern / electronic loop for gameplay; loopable at full length |
| `Assets/Audio/External/Kenney/UI_*.ogg` (click_002, maximize_006, minimize_006, confirmation_002/003/004, error_006, glass_002/005) | Interface Sounds by Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 | Clean UI clicks, open/close, purchase, error, level-up, orb chimes |
| `Assets/Audio/External/Kenney/SciFi_*.ogg` (explosionCrunch_002, lowFrequency_explosion_000, thrusterFire_001, laserRetro_004, forceField_002, impactMetal_001) | Sci-Fi Sounds by Kenney | https://kenney.nl/assets/sci-fi-sounds | CC0 1.0 | Rocket launch, explosion, electric zap and electric impact |
| `Assets/Audio/External/Kenney/Impact_*.ogg` (impactMetal_light_002, impactPunch_heavy_001) | Impact Sounds by Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | Bullet hit sound |

Only the individual files listed were copied into `Assets/`; the full packs were not imported.
Kenney's licence text is kept at `Assets/Audio/External/Kenney/Kenney_License.txt`.

## Considered and rejected
- "Desert calmness and fighting" (OpenGameArt): marked CC0, but it quotes a well-known film theme, so its rights are unclear. Not used.

## Made for Drillex (no third-party rights)
- UI sprites, icons, VFX textures: generated procedurally (`SourceArt/UI/steps/10_ui_sprites.cs`).
- Cannon / twin cannon / gatling shots, flamethrower loop, sound-trap ping, worm siren: synthesised in-editor (`Assets/Editor/DrillexAudio/DrillexAudioSynth.cs`).
- Font: Conthrax SemiBold was already in the project before this work.
