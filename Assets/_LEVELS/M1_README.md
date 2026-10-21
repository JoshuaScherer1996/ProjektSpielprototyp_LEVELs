# LEVEL's - M1 Player

## Playing

Open Village, Plateau or Tower and press Play. Each scene has one saved,
connected Player prefab instance under `Actors/Player`. No player is created
at runtime or carried across scenes.

- Move with WASD, arrow keys or the left gamepad stick.
- Keyboard diagonal movement has the same maximum speed as cardinal movement.
- Analog stick strength is preserved after the existing Input System deadzone.
- Release input to stop. The well, locked door and world boundaries are solid.
- The orthographic camera follows the player in XY and keeps its initial Z (-10).
  It intentionally has no smoothing, map clamp or scene-transition behavior yet.

## Components and configuration

`Prefabs/Characters/Player.prefab` contains a 0.65 x 0.9 blue SpriteRenderer,
a matching BoxCollider2D, Rigidbody2D, PlayerInput, PlayerInputReader and
PlayerMovement. It uses the Player tag, Player physics layer, Characters sorting
layer, existing square sprite and existing greybox material.

- Rigidbody2D: Dynamic, gravity scale 0, Freeze Rotation Z, Continuous collision
  detection, Interpolate, zero linear damping.
- PlayerFrictionless.physicsMaterial2D: zero friction and bounce, assigned to the
  collider to support sliding along walls.
- PlayerInput: the existing `Assets/Settings/InputSystem_Actions.inputactions`,
  default Player map, Keyboard&Mouse scheme, automatic control-scheme switching.
  No competing action asset was added.
- PlayerInputReader: resolves `Player/Move` through PlayerInput and subscribes to
  performed/canceled events. It clamps vector length to 1 and clears input on
  disable. It does not move any physics object.
- PlayerMovement: serialized speed (default 5 units/second), body and input
  references. Applies Rigidbody2D.linearVelocity only in FixedUpdate and stops
  the body when disabled. It does not poll devices or teleport the Transform.
- CameraFollow: serialized target and XY offset; follows in LateUpdate.

Sorting order is Ground, Props, Characters, Foreground, after Default.
World/Player collisions are enabled by the existing 2D collision matrix.

The M0 validator now scopes its solid-world layer checks to Environment and
permits later actor/camera scripts, while retaining missing-reference checks.
M0 scene creation still preserves existing non-empty scenes.

## Verification performed

- Unity compilation, prefab/input/body references and camera targets in all scenes.
- Live Play Mode tests with temporary virtual Input System keyboard, mouse and
  gamepad devices, removed after testing. Tests ran outside Assets; no test
  harness or test-only component is included in a player build.
- All eight WASD directions at approximately 5 units/second, arrow-key movement,
  equal diagonal speed, release-to-stop, gamepad diagonal and analog movement.
- All four world boundaries in each scene, the Village well and Tower door.
- Camera target tracking and Z=-10 in all scenes; advancing player-loop frames
  were verified, rather than relying only on screenshots.
- Player camera render inspected at 1280 x 720.

Manual follow-up: test keyboard and a physical gamepad in each scene, move
diagonally against the well and corners, and judge speed/camera framing.
No standalone player build, combat, health or progression is included in M1.
