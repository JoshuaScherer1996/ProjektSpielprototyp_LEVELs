# LEVEL's - M0 Greybox

## Scenes

Open `Scenes/Village.unity`, `Scenes/Plateau.unity` or `Scenes/Tower.unity`.
Each scene contains editable `Systems`, `Environment` and `Markers` roots.
The floor is 24 x 14 world units, centered at the origin. Four static solid
BoxCollider2D boundaries enclose it. Floors have no colliders.

- Village: grey floor and a connected `Well.prefab` instance at the origin.
  The well has a CircleCollider2D with radius 1.5.
- Plateau: green floor, no characters or enemies.
- Tower: dark grey floor and a connected `LockedBossDoor.prefab` instance
  at (0, 5.1), with a solid 6 x 3 BoxCollider2D.

All cameras are orthographic and use the existing URP 2D renderer.
The included GreyboxUnlit material keeps the placeholder colors predictable
without changing the project's URP or lighting configuration. Camera renders
were visually checked at 1280 x 720 (16:9).

## Markers

All objects below `Markers/SpawnPoints` and `Markers/Transitions` are plain
Transforms. Select them in the Hierarchy to inspect their positions. They have
no runtime scripts, trigger colliders or scene-loading behavior.

| Scene | Marker | Position (x, y) |
| --- | --- | --- |
| Village | Spawn_Default | 0, -4 |
| Village | Spawn_FromPlateau | 0, 5 |
| Village | Transition_ToPlateau | 0, 6.5 |
| Plateau | Spawn_FromVillage | 0, -5 |
| Plateau | Spawn_FromTower | 0, 5 |
| Plateau | Transition_ToVillage | 0, -6.5 |
| Plateau | Transition_ToTower | 0, 6.5 |
| Tower | Spawn_FromPlateau | 0, -5 |
| Tower | Transition_ToPlateau | 0, -6.5 |

The world boundaries are intentionally closed in M0. A later milestone can
adapt the edges when the transition markers gain actual scene-loading behavior.

## Layers and build scenes

- Sorting order: Default, Ground, Props, Foreground.
- Physics: World (currently user layer 8), used by all solid M0 obstacles.
- Characters, Player, Enemy and SceneTrigger layers are deferred until used.
- Effective build order: Village, Plateau, Tower, then the preserved SampleScene.
  The active Windows platform profile currently uses the global scene list.

The existing Input Actions asset at `Assets/Settings/InputSystem_Actions.inputactions`
is preserved for later milestones. No second input asset or gameplay code is included.

## Explicit editor tools

`Tools > LEVELs > M0 > Create Missing Greybox` runs the editor-only setup.
It does not execute on import or in a player build. It refuses to run during
Play Mode, compilation, or with unsaved scene changes. It leaves existing
non-empty scenes and existing prefab assets intact, creates missing assets,
and places M0 scenes first in the effective build scene list. It opens Village
on completion. It is not a reset or rebuild command for hand-edited scenes.

`Tools > LEVELs > M0 > Validate Greybox` checks saved scenes, prefab links,
sprite/material references, missing scripts, layers, marker structure,
colliders and effective build order. Validation temporarily opens closed M0
scenes additively and closes them afterward without saving.

The same methods can be called through the connected Unity CLI:

```powershell
unity command script eval --code 'return Levels.EditorTools.M0GreyboxSetup.Build();' --port 7800 --caller plugin --skill unity-cli --format json
unity command script eval --code 'return Levels.EditorTools.M0GreyboxSetup.Validate();' --port 7800 --caller plugin --skill unity-cli --format json
```

No movement, enemies, combat, transitions, progression, shop or bosses exist in M0.
No commit or push has been performed.
