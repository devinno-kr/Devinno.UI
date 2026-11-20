# Devinno.UI.Scene3D — 3D view

Covers: showing a glTF model in a screen (`SceneView`), moving, coloring and picking its parts from bound values (`Rotate`, `Move`, `Color`, `Clip`), writing values from code (`SceneState`), preparing the model in Blender, checking your work.
Read first: the core screen rules in `.devinno/Devinno.UI/llms.txt` — markup, binding, `Invalidate` / `Repaint`, custom widgets.

<a id="setup"></a>
## Setup

- Reference `Devinno.UI.Scene3D` next to the core package and a host (`Devinno.UI.OpenTK` or `Devinno.UI.WinForms`).
- Register the tag once at startup, before the first screen is built:

```csharp
using Devinno.UI.Scene3D;

var registry = new ComponentRegistry().UseScene3D();
var app = new DvApp { Theme = DvTheme.Dark, Registry = registry };
```

- Copy the model next to the program (csproj: `<None Include="Models\cell.glb" CopyToOutputDirectory="PreserveNewest" />`). `src` is relative to the working directory, so set `Environment.CurrentDirectory = AppContext.BaseDirectory;` at startup when the program can be started from elsewhere.
- Needs desktop OpenGL 3.3 (Windows PC graphics drivers have it). Works in `ViewWindow` and `ViewControl`, with CPU and GPU drawing. A window created with `gles: true` (OpenGL ES) shows a message in the view instead of the model.

<a id="sceneview"></a>
## SceneView

```xml
<SceneView src="Models/cell.glb" camera="{Camera}" value="{Bind Selected}" pick="Robot,Pump1,Door" grow="1">
  <Rotate node="Turret"  axis="y" value="{TurretDeg}"/>
  <Rotate node="Arm1"    axis="z" value="{Arm1Deg}"/>
  <Move   node="LiftRod" axis="y" value="{LiftMm}" scale="0.001"/>
  <Move   node="FingerL" axis="z" value="{GripPct}" scale="-0.0005"/>
  <Move   node="FingerR" axis="z" follow="FingerL" ratio="-1"/>
  <Rotate node="GearA"   axis="y" value="{GearDeg}"/>
  <Rotate node="GearB"   axis="y" follow="GearA" ratio="-2"/>
  <Color  node="PumpLamp" value="{LampColor}"/>
</SceneView>
```

| Attribute | Value | Default | Binding | Behavior |
|---|---|---|---|---|
| `src` | path of a `.glb` file | none | yes | Read once per path and kept for the life of the program; a changed file is not read again while running |
| `camera` | name of a camera in the model | whole model | yes | A new name moves the view to that camera in 300 ms. A name the model does not have (for example `Free`) shows the whole model |
| `value` | name of the selected part | none | two-way (`{Bind …}`) | A click writes the name of the click unit |
| `pick` | node names, comma-separated | each root node | yes | Click units: a click on any part below a unit selects the unit |
| `state` | a `SceneState` object | none | yes | Values written from code ([SceneState](#scenestate)) |

- Size: like other elements (`grow`, `style="width:…; height:…"`). Without a size it measures 640×480.
- Input: drag turns the view around its target, the wheel zooms, a click (moving less than 6 px) selects, hovering highlights the click unit. The selected unit is tinted blue.
- Choosing a camera again by the same name does not reset a drag or zoom; the view moves only when `camera` changes.

<a id="definitions"></a>
## Definitions: Rotate, Move, Color, Clip

Child elements of `SceneView` are settings, not drawn elements. Each names a node of the model by its glTF name (the Blender object name). Their values are read when the screen is rebuilt (`Invalidate`), like other bound attributes.

| Element | Attributes | Effect |
|---|---|---|
| `Rotate` | `node`, `axis`, `value`, `scale` (1), `offset` (0), `follow`, `ratio` (1), `disabled` | Turns the node about its own origin by `value × scale + offset` degrees |
| `Move` | `node`, `axis`, `value`, `scale` (1), `offset` (0), `follow`, `ratio` (1), `disabled` | Moves the node along its own axis by `value × scale + offset` meters |
| `Color` | `node`, `value`, `amount` (1), `disabled` | Mixes the color of the node and its children toward `value` by `amount` (0–1). At 1 the part is that color only and its texture is hidden. An empty `value` keeps the model's color |
| `Clip` | `name`, `value`, `min` (0), `max` (clip length in seconds), `disabled` | Poses the model at a point of a glTF animation: `value` from `min` to `max` maps to the start and end of the clip |

- `axis`: `x`, `y`, `z`, `-x`, `-y`, `-z` or three numbers (`"0 0.7071 0.7071"`), in the node's own axes as exported. Angles are right-handed: looking from the positive end of the axis toward the origin, a positive angle turns counterclockwise.
- Children follow their parent node: turning `Turret` carries `Arm1` with it, and `Arm1`'s axes turn too.
- `follow="GearA" ratio="-2"`: takes the amount of the `Rotate` (or `Move`) on `GearA` and multiplies it by `ratio` — meshed gears, mirrored fingers. `value`, `scale` and `offset` are then not used.
- Several `Rotate`s on one node add up; so do several `Move`s.
- `disabled="{UseClip}"` switches a definition off. Use it to choose between computed values and a Blender animation for the same parts.
- `Color` `value`: `#RRGGBB`, `#RRGGBBAA` or a theme token (`state-run`, `alarm-major` …).
- Units: `value × scale` should come out in degrees for `Rotate` and in meters for `Move`. A value in millimeters takes `scale="0.001"`.

<a id="scenestate"></a>
## Values from code: SceneState

For values that change faster than the screen should be rebuilt, or that come from a communication or simulation thread, write them to a `SceneState` and bind it to `state`.

```csharp
public SceneState Scene { get; } = new();   // VM property; markup: <SceneView … state="{Scene}">

// any thread
Scene.Rotate("Turret", Vector3.UnitY, degrees);
Scene.Move("LiftRod", Vector3.UnitY, meters);
Scene.Color("PumpLamp", SKColors.Red);
App.Surface.Repaint();   // redraw without rebuilding the screen
```

- A value written here replaces the markup value of the same kind (rotation, move, color, clip) on the same node until `Reset(node)`; other kinds on that node still follow the markup.
- `Batch(s => { … })` writes several values as one change, so a frame never shows only some of them.
- `SetWorldPose(node, position, rotation)` sets a node's pose in world space, ignoring its parent — for poses computed by a physics simulation.
- `SetClipTime(clip, seconds)` poses the model at a time of an animation clip.
- `PartClicked` (UI thread) and `Selected` also report clicks, with or without a `value` binding.

<a id="blender"></a>
## Preparing the model in Blender

- **Origin at the pivot**: a part turns about its object origin. Put each moving object's origin on its hinge or slide axis (Set Origin → Origin to 3D Cursor).
- **Apply rotation and scale** (Ctrl+A → Rotation & Scale) on moving objects, so their own axes match the world axes. Otherwise `axis` is in the object's tilted axes.
- **Parent** parts that move together (arm under turret, finger under gripper). Children follow their parent.
- **Unique names**: `node` is the object name. When two objects share a name, the first one in the file is used.
- **Axes after export**: the glTF exporter turns Blender Z-up into Y-up. With rotation applied, Blender X stays `x`, Blender Z becomes `y`, Blender -Y becomes `z` (toward the viewer).
- **Export** File → Export → glTF 2.0, format glTF Binary (`.glb`). Include cameras to use them as `camera` names; include animations to use them as `Clip` names (the action name). Base color images are packed into the file.
- **Materials**: base color, base color texture, alpha (opaque, alpha clip, blend) and backface culling are used. Other material settings (metallic, roughness, normal maps, emission) are not.

<a id="check"></a>
## Checking your work

- Unknown node, clip or attribute names, an axis that cannot be read and a `follow` without its source are skipped with a Trace warning, once each. The build does not check them. Read the Trace output (or add a `TraceListener`) while writing the markup.
- A file that cannot be read, or no OpenGL 3.3, shows the reason inside the view.
- PNG without a window works as for other screens (`DvRenderer.RenderToFile`); the machine still needs an OpenGL 3.3 driver.

<a id="performance"></a>
## Performance and memory

- The scene is drawn again only when a value, the camera, the size, the selection or the highlight changes; otherwise the last picture is reused.
- On a GPU host (`Gpu = true`) the scene is drawn straight into a texture of the host's GL context. On a CPU host it is drawn in a hidden GL context and read back, which costs more for large views.
- The picture is at most 2048 px per side (at the display scale). Anti-aliasing is 4× up to about 1.6 million pixels, 2× above.
- A view that has not been drawn for 30 seconds gives its GL memory back the next time another 3D view draws, and draws again when it is shown.

<a id="limits"></a>
## Limits

- Desktop OpenGL 3.3 only; not for OpenGL ES boards.
- No physics, inverse kinematics or collision: compute poses in C# (or a simulator) and write them as values.
- Lights from the file, shadows and materials beyond those listed under [Blender](#blender) are not used; the built-in lights follow the camera.
- Values change in one step; smooth motion comes from writing values often (for example every 20–50 ms).
- Highlight and selection colors are fixed.
