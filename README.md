# Escape

A 2D top-down horror game made in Unity (C#). You're trapped in a dark haunted house.
Find the 3 keys, unlock the exit, and get out before the shadow monster catches you.

## Controls

| Key | Action |
|-----|--------|
| W A S D | Move |
| Space (hold) | Flashlight. Scares the monster away; runs on a recharging battery. |
| Enter | Start / Pause / Restart |

## Setup (about 5 minutes)

1. Create a new Unity project with the **2D** template (Unity 2022.3 LTS or Unity 6 both work).
2. Copy the `Assets/Scripts` folder from this repo into your project's `Assets` folder.
3. In an empty scene, create an empty GameObject (**GameObject > Create Empty**) and name it `GameManager`.
4. Drag `GameManager.cs` onto it.
5. Press **Play**.

That's it. The house, player, monster, lighting, sounds, and menus are all built by the scripts at runtime.
Nothing else needs to be in the scene (the default Main Camera is fine; the script configures it).

> If you see an error about the Input System: go to **Edit > Project Settings > Player > Active Input Handling**
> and pick **Both**. (The scripts support either input system, so this is only a fallback.)

## Replacing the art and sound with downloaded assets

The game ships with placeholder pixel art and synthesized sounds generated in code so it runs immediately.
To swap in real assets, which is where the Art and Sound rubric points come from:

- **Art:** Select the `GameManager` object. Under **Art Overrides**, drag any sprite into a slot
  (floor, wall, bookshelf, table, player, monster, key, exitDoor). Any size works; sprites are
  auto-scaled to one tile. A good free (CC0) match is Kenney's *Tiny Dungeon* pack (kenney.nl).
  For pixel art, set the texture's Filter Mode to **Point** and Pixels Per Unit to **16**.
- **Sound:** On the same object, the **SoundManager** component has a slot for every effect
  (footstep, key pickup, door unlock, growl, shriek, flashlight click, heartbeat, game over, victory).
  Empty slots keep the generated sound. Freesound.org and Kenney's audio packs are good sources.

## How the code works

| Script | What it does |
|--------|--------------|
| `GameManager.cs` | Builds the world, owns the game state (Title / Playing / Paused / GameOver / Won), handles Enter, camera follow, score, and draws the HUD and menus with IMGUI. |
| `Level.cs` | Turns the text map (`HouseMap`) into tiles, furniture, keys, and the exit. Also does **breadth-first search** pathfinding on the tile grid for the monster. |
| `PlayerController.cs` | WASD movement through a dynamic Rigidbody2D (physics stops you at walls), facing direction, footsteps. |
| `Flashlight.cs` | Battery drain/recharge with a lockout when it hits zero, the rotating darkness overlay with a beam cut out of it, and scaring the monster when the beam hits it. |
| `MonsterAI.cs` | State machine: **Patrol → Chase → Search → Patrol**, plus **Flee** when hit by the flashlight. Sees you only with clear line of sight, hears you if you walk close by, and speeds up with each key you collect. |
| `KeyPickup.cs` / `ExitDoor.cs` | Collectibles and the locked/unlocked exit. |
| `Obstacle.cs` | Tag component for walls and furniture: they block movement and the monster's line of sight (this is how hiding works). |
| `SoundManager.cs` | Plays all sounds. The heartbeat is the only "music" and it's tied to gameplay: it speeds up and gets louder as the monster gets closer. |
| `SpriteFactory.cs` / `SoundFactory.cs` | Generate the placeholder art and sounds in code. |
| `InputHelper.cs` | Reads the keyboard with either the old Input Manager or the new Input System. |
| `GameArt.cs` | The list of sprite slots shown in the Inspector, plus sprite scaling/material helpers. |

### Editing the house

The level is the `HouseMap` string array at the top of `Level.cs`. Legend:
`#` wall, `.` floor, `+` doorway, `P` player start, `M` monster start, `K` key,
`E` exit, `W` monster patrol waypoint, `B`/`F` bookshelf, `T` table. Keep every row the same length.

### Scoring

- +500 per key
- +100 each time you scare the monster off with the flashlight
- Time bonus on escape: 2000 minus 10 per second
- Battery bonus on escape: up to +200 for a charged flashlight
- Best score is saved between sessions

## Recording the gameplay video

Use **Window > General > Recorder** (install the *Recorder* package) or any screen recorder.
Show: the title screen, picking up a key, hiding behind a wall to lose the monster, scaring it
with the flashlight, the locked door message, the door unlocking, and both a game over and an escape.
