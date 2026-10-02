# The 3:15 Escapement

A first-person murder-mystery prototype made in Unity 6 (URP).

Inventor Arthur Vance lies dead beneath the fallen pendulum of the town clocktower, and the great clock stopped at 3:15 AM. Search the gear room and study for evidence, question two shady informants, and make your accusation on the Case Board. You only get three tries.

## Features

- An octagonal neo-Gothic clocktower with interlocking gears, moonlight through the clock face, and crime-scene tape
- 9 pieces of evidence, 3 suspects and 2 red herrings, plus one clue you can only examine in place
- A Case Board with a 4-question accusation and 3 attempts. A wrong accusation only tells you how many answers were right.
- Two NPCs:
  - **Wren**, a spy who sells direct tips (each one costs your top rank)
  - **Finch**, a messenger kid who gives free riddles about the clues you're missing
- Detective ranks, plus good and bad endings
- A procedurally synthesised horror soundscape (wind, creaks, a tolling bell, whispers, a heartbeat and clue stingers), with no audio files

## Controls

| Action | Key |
|---|---|
| Move / Look | WASD / Mouse |
| Sprint | Shift |
| Inspect / Talk | E |
| Rotate held item | Hold Left Mouse |
| Put back / Close | Right Mouse or Esc |
| Case Board | Tab (or the corkboard by the desk) |
| Dialogue replies | Click or 1-4 |
| Restart after the verdict | R |

## Running it

1. Open the project in **Unity 6000.3** (Universal Render Pipeline, Input System).
2. Open `Assets/Scenes/Clocktower.unity` and press Play, then click inside the Game view.

## Rebuilding the content

- **3D models:** `SourceArt/build_clocktower.py` generates every model in Blender and exports the FBX files to `Assets/Models/Clocktower/`:
  ```
  blender --background --factory-startup --python SourceArt/build_clocktower.py
  ```
- **Scene:** in Unity, use **Tools > The 3:15 Escapement** to reconfigure the imports and rebuild the scene.

## Project layout

- `Assets/Scripts/`: gameplay (player controller, inspection, case logic, Case Board, dialogue, NPCs, soundscape)
- `Assets/Editor/`: the scene builder
- `Assets/Models/Clocktower/`: generated FBX models
- `SourceArt/`: the Blender generator script and the `.blend` source
