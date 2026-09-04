# Prompt untuk agy — Fake-route rig di Sandbox

Referensi: docs/superpowers/plans/2026-09-03-avatar-animation-test-harness.md

```
# CONTEXT

Repo: D:\Dev\Projects\UnityProjects\Learning\DARSI-Indoor Navigation
Read CLAUDE.md first. Live Coplay MCP access to the running Unity Editor --
call set_unity_project_root FIRST, before any other Unity tool.

Background: AIAvatarGuideController.cs (the AI guide's movement logic) was
rewritten from scratch and has NEVER ACTUALLY RUN -- it's only passed a
compile check. It cannot move at all right now because its dependencies are
missing from the test scene:

  - ShowPath (a MultiSet-SDK.Core class, NOT project code) needs a baked
    NavMesh to compute a path across, and the Sandbox scene currently has
    ZERO baked NavMesh (verified: NavMesh.CalculateTriangulation() returns 0
    vertices).
  - ShowPath's public API is exactly: ResetPath(), SetPositionFrom(Transform),
    SetPositionTo(Transform). Feed it two Transforms and it computes a real
    NavMesh path into its own LineRenderer on a timer (public field
    pathUpdateFrequency). Do NOT try to hand-write LineRenderer positions
    yourself -- use this API, it already does the real thing.
  - AIAvatarGuideController.Awake() auto-resolves `showPath` via
    FindFirstObjectByType<ShowPath>() and reads the LineRenderer from it.
  - The scene already has: Avatar_Companion (GameObject, active, carries BOTH
    AvatarCompanionController and AIAvatarGuideController, both enabled),
    Main Camera (carries SimpleSandboxFreeCam, moveSpeed 1.3, and
    GuideStateHUD -- both already built this session, do not modify either),
    Floor_Grid (a 50x50m flat mesh at world origin, Y=0).
  - GuideStateHUD.cs already exists at Assets/Scripts/Avatar/GuideStateHUD.cs
    and already has a working keybind pattern (T key cycles Time.timeScale,
    using Keyboard.current.tKey.wasPressedThisFrame). Follow this exact
    pattern for the new keybind below -- do not create a second HUD script.

# OBJECTIVE

Make it possible to actually trigger and observe AIAvatarGuideController's
real movement logic in the Sandbox scene. Four steps, in order:

1. BAKE A NAVMESH over Floor_Grid. Add whatever is needed for
   NavMesh.CalculateTriangulation() to return a non-empty result covering at
   least the area from world (0,0,0) to (0,0,10) -- mark Floor_Grid Navigation
   Static and bake (Window > AI > Navigation in older Unity, or the
   NavMeshSurface component if this Unity version uses the newer package --
   check which is actually available before picking one). Verify the bake
   worked by calling NavMesh.CalculateTriangulation() again and confirming
   vertices > 0.

2. PLACE TWO MARKER TRANSFORMS on the baked NavMesh: empty GameObjects named
   "RouteStart" at world position (0, 0, 2) and "RouteEnd" at world position
   (0, 0, 8) -- a straight 6m line, well inside Floor_Grid's bounds and near
   Avatar_Companion's current position. Add them as children of a new empty
   parent GameObject named "RouteMarkers" so they're easy to find/delete later.

3. ADD ONE KEYBIND to GuideStateHUD.cs (same file, same Update() method,
   same Keyboard.current pattern already used for T) -- pressing "R" should:
     a. Resolve ShowPath via FindFirstObjectByType<ShowPath>() (cache it,
        same pattern as the existing `guide` field)
     b. Resolve RouteStart/RouteEnd transforms (by name is fine, this is a
        test-only script)
     c. Call showPath.SetPositionFrom(routeStart) and
        showPath.SetPositionTo(routeEnd)
     d. Call guide.StartLeading() (guide is already resolved in Awake())
   Add this key to the existing on-screen HUD text so the tester knows R
   exists. Guard all of this with null checks -- if ShowPath or the markers
   aren't found, do nothing silently (log a warning, don't throw).

4. VERIFY END TO END. Enter Play Mode, press R, and confirm via Coplay
   (get_game_object_info on Avatar_Companion, or reading GuideStateHUD's
   displayed values) that:
     - AIAvatarGuideController.CurrentState actually changes away from
       IdleStand (i.e. it's really moving, not stuck)
     - the avatar's position actually changes over a few seconds (sample
       position, wait, sample again -- show both readings)
   Then EXIT Play Mode before finishing (per constraints below).

# STYLE

Precise and evidence-backed, same as prior rounds. Every claim of success
needs a cited Coplay tool result or an exact file:line, not a description of
intent.

# TONE

Careful, non-expansive. Prefer doing less and verifying more.

# AUDIENCE

Project owner (Bagus) and a Claude Code session that will independently verify
everything before it's committed -- assume zero shared memory, name every
file/GameObject/value explicitly.

# RESPONSE FORMAT

1. What you changed, file by file (NavMesh bake settings used, marker
   positions actually placed, the exact new code added to GuideStateHUD.cs).
2. Verification evidence for each of the 4 objective steps above, in order.
3. Anything noticed but deliberately not touched.
4. Anything you could not verify and why.

# CONSTRAINTS

- Scene scope: Assets/Scenes/Sandbox_AvatarCompanion.unity ONLY. Do NOT touch
  TestingHCM.unity.
- Do NOT modify AIAvatarGuideController.cs at all -- this rig exists
  specifically to test that file's code UNCHANGED. Not even comments.
- Do NOT modify SimpleSandboxFreeCam.cs or the moveSpeed value already set.
- Do NOT modify AvatarGuide.controller (the Animator Controller) -- its
  BlendTree was already updated this session (Idle/Walk/SlowRun wired in).
- Do NOT touch walkClipSpeed, MediumRun, the orphaned Point/PointDir animator
  states, or anything related to the Cari Teman feature.
- Do NOT run git commit, git push, or git checkout. Leave everything
  uncommitted for review.
- This working directory is shared with another session and may have
  unrelated uncommitted/dirty files in it -- if you see dirty files outside
  what you touched, leave them exactly alone, do not investigate or revert
  them.
- Do NOT leave Play Mode running when you finish -- exit it after step 4's
  verification.
- If any step fails, STOP and report the failure with what you tried. Do not
  substitute a different approach without flagging it as a deviation.
```
