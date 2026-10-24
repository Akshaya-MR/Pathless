# Pathless

**Pathless** is a cyberpunk-themed arcade racing game set in **Neo-Kowloon 2087**. Drive into a neon-soaked city after dark, race a field of AI rivals, drift through the lanes, dodge roadblocks, and chase boosts along a repeating highway beneath a synthwave sky.

> Own the night. Find your line.

## Screenshots

Screenshot slots for the menu and race gameplay. Add images under `docs/screenshots/` and replace each placeholder with a Markdown image when ready.

| Main menu | In-race gameplay |
| --- | --- |
| *Add `docs/screenshots/main-menu.png` here* | *Add `docs/screenshots/race-gameplay.png` here* |

| Car selection | Pause menu |
| --- | --- |
| *Add `docs/screenshots/car-selection.png` here* | *Add `docs/screenshots/pause-menu.png` here* |

## Game at a glance

- **Genre:** Cyberpunk / synthwave arcade racing
- **Engine:** Unity 6 (**6000.6.6f1**)
- **Primary scene:** `Assets/Scenes/Main.unity`
- **Race field:** One player plus four AI rivals
- **Default race distance:** 1,500 metres
- **Setting:** Neo-Kowloon 2087, a neon city of towers, roadside props, glowing road lines, and a retro-futuristic sky

Pathless is an in-development playable project. Its race environment is assembled by the runtime bootstrap, while the roadside city layout is made from editable scene objects.

## Gameplay

### Drive, drift, and overclock

Use arcade-style acceleration, braking, steering, and handbrake drifting to navigate the road. Sustained drifts build the **Overclock Core** and bank drift score when the drift ends. Spend core with **E** or **Left Shift** to engage overclock acceleration.

### Boost pads and road hazards

The track places neon boost pads and red roadblock-style obstacles along the roadway. A boost pad gives a **five-second, 1.3× speed boost** and adds overclock charge. Hitting a roadblock slows the car. Car-to-car impacts and obstacle hits can play the collision sound when they happen close to the player.

### Race and rivals

Race against four AI drivers with distinct handles, crews, colors, and driving personalities. The vehicle registry currently includes:

- Silver Coupe
- Porsche GT3 RS
- Cyberpunk Car
- Sci-Fi Vehicle

AI vehicle models are selected from the registry, excluding the player's chosen model, with the lineup shuffled before a model is reused.

### Rolling cyberpunk city

The roadway recycles behind the racers to create a continuous drive without keeping the entire course active at once. Buildings, lights, and other roadside props are copied from the editable layout and recycled with the road sections. This lets you arrange the city directly in the Unity scene while the runtime handles looping.

## HUD and audio

The in-game HUD includes a speedometer, score, race position/timing, distance information, an Overclock Core display, and a start countdown. The pause menu includes resume, restart, settings/audio, and quit controls. The top-right audio button toggles all audio and remembers the setting between sessions.

Current audio files in `Assets/music/`:

| File | In-game use |
| --- | --- |
| `321 GO GO GO.mp3` | Countdown callout |
| `engine_start.mp3` | Engine start/rev during the countdown |
| `Cars after the count down.mp3` | Launch/rev sound on GO |
| `Oops thats gonna leave a scratch.mp3` | Car and obstacle collision sound |
| `Race music 1.mp3` | Race music |

Add more music tracks to `Assets/music/`; the Unity editor refreshes the playlist catalog automatically. Use `.mp3` (or another Unity-supported audio format) and give sound-effect files clear names such as `engine`, `countdown`, `collision`, or `oops` so they are not mistaken for music.

## Controls

| Action | Keyboard |
| --- | --- |
| Accelerate | W / Up Arrow |
| Brake / reverse | S / Down Arrow |
| Steer | A / D or Left / Right Arrow |
| Handbrake / drift | Space |
| Overclock boost | E or Left Shift |
| Reset car | R |
| Toggle chase camera | C |
| Pause / resume | Escape |
| Start race / countdown | Enter |
| Cycle development car visuals | F1 |
| Rebuild development track | F2 |

Gamepad input is also supported through Unity's Input System.

## Download and play

Download the latest Windows 64-bit build from the [Pathless GitHub Releases page](https://github.com/Akshaya-MR/Pathless/releases/latest). Extract the downloaded ZIP, open the extracted folder, and run `Pathless.exe`. Press **Enter** at the menu to start the race.

## Run the project

1. Install **Unity 6000.6.6f1** using Unity Hub.
2. Clone the repository with Git LFS installed, or run `git lfs pull` after cloning to fetch the model, audio, and texture assets.
3. Open the repository folder in Unity Hub.
4. Open `Assets/Scenes/Main.unity`.
5. Press **Play**, then **Enter** to begin the countdown and release the racers.

## Edit the roadside city

In the Main scene, expand **GameBootstrap > Roadside Props - EDIT ME**. Place or duplicate city models under the left- or right-side layout objects. The source props are hidden during play; the game makes runtime copies and recycles them along the track.

See [`Assets/Models/PlaceableCityProps/HOW_TO_PLACE.txt`](Assets/Models/PlaceableCityProps/HOW_TO_PLACE.txt) for distances, guide lines, and placement details.

## Repository layout

| Path | Contents |
| --- | --- |
| `Assets/Scenes/` | Main and test scenes |
| `Assets/Scripts/Car/` | Vehicle handling, visual setup, drift, and registry |
| `Assets/Scripts/AI/` | Rival drivers and personalities |
| `Assets/Scripts/Track/` | Rolling road, pickups, hazards, and roadside layout |
| `Assets/Scripts/UI/` | HUD and menus |
| `Assets/Scripts/Core/` | Runtime bootstrap and race setup |
| `Assets/Models/Vehicles/Resources/` | Vehicle models and textures loaded at runtime |
| `Assets/Models/PlaceableCityProps/` | Editable city buildings and roadside props |
| `Assets/music/` | Race music and sound effects |
| `Assets/Resources/` | Runtime-loaded assets and music catalog |

## Asset and license notes

Some models, textures, and the skybox are third-party assets. Known sources and license notes are listed in [`Assets/Models/PlaceableCityProps/ASSET_SOURCES.txt`](Assets/Models/PlaceableCityProps/ASSET_SOURCES.txt) and [`Assets/Models/Vehicles/Resources/VehicleModels/ASSET_SOURCES.txt`](Assets/Models/Vehicles/Resources/ASSET_SOURCES.txt). The source notes flag assets whose redistribution terms still need confirmation. Check the relevant license before sharing builds or source assets.
