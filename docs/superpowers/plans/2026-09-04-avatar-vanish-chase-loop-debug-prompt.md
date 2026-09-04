# Prompt untuk agy — Debug "avatar blinking, moving like crazy" di Sandbox

Referensi: docs/DECISIONS.md Amandemen 034-B, docs/superpowers/plans/2026-09-03-fake-route-rig-prompt.md

```
# CONTEXT

Repo: D:\Dev\Projects\UnityProjects\Learning\DARSI-Indoor Navigation
Read CLAUDE.md first. Live Coplay MCP access to the running Unity Editor --
call set_unity_project_root FIRST, before any other Unity tool.

Background: AIAvatarGuideController.cs (the AI guide's movement logic, "lompat-tunggu"
model, ADR-034 Amandemen 034-B) was verified working in Play Mode on 2026-09-04 -- the
state machine (IdleStand -> LeadingPath -> WaitingForUser -> Chasing -> ArrivalPointing)
genuinely cycles and the avatar's Transform.position genuinely moves. BUT the project owner
reports that while testing, the avatar "often blinks, moving like crazy" -- hard to debug
because it's not obviously reproducible on demand.

A LIVE Play Mode session on 2026-09-04 around 14:25-14:27 (Sandbox_AvatarCompanion.unity)
captured something concrete via get_unity_logs: a PERFECTLY PERIODIC ~8-second cycle,
repeating for over a minute straight:

  Chasing (T+0s) -> LeadingPath (T+2.0s) -> WaitingForUser (T+2.3s) -> Chasing (T+6.0s, exactly
  chaseStallSeconds) -> repeat

This means: every single WaitingForUser cycle hit the FULL 6-second stall timeout (the user
transform, i.e. Camera.main, never got within advanceTriggerDistance=1.5m of the current
waypoint), which triggered Chasing, which walks the avatar to within waypointArrivalEpsilon
(0.15m, see AIAvatarGuideController.cs field, ~line 65) of Camera.main's position, then
immediately picks a new waypoint via NextWaypoint(userS, pathLength) -- and since the user's
projected position along the route (userS) barely moved, it's likely to re-pick almost the
SAME waypoint, and the whole cycle repeats.

STRONG CANDIDATE ROOT CAUSE FOR THE "BLINKING" HALF (not yet visually confirmed with a
screenshot, only reasoned from code -- YOUR JOB IS TO CONFIRM OR REFUTE THIS, not assume it):

Avatar_Companion also carries AvatarSafetyFade.cs (Assets/Scripts/Avatar/AvatarSafetyFade.cs).
It measures HORIZONTAL distance from the avatar to Camera.main every frame and, per the
documented behavior in docs/DECISIONS.md Amandemen 034-A, sets `rend.enabled = false` on ALL
renderers the instant that distance drops to <= fadeEndDistance (0.5m default) -- the avatar
"HILANG MENDADAK" (vanishes instantly), it does NOT fade smoothly (that's a deliberate,
documented decision in 034-A, do not treat it as a bug on its own or try to make it fade
smoothly without reading 034-A first).

AIAvatarGuideController's Chasing state (Update(), the `case GuideState.Chasing:` block,
around line 250-260) calls MoveToward(userTarget, chaseSpeed) where userTarget is
Camera.main's position, and MoveToward's own arrival check is
`Vector3.Distance(before, target) <= waypointArrivalEpsilon` (0.15m). 0.15m is INSIDE
AvatarSafetyFade's 0.5m vanish threshold. So: every time Chasing successfully "catches up"
to the user, geometrically it MUST have passed through (and very likely ended inside) the
safety-fade vanish zone first -- meaning every successful catch-up in this loop is a
candidate vanish event. Combined with the ~8-second periodic loop above, this could produce
exactly the reported "blinking, moving like crazy" -- vanish-reappear-vanish-reappear every
~8 seconds, synchronized with Chasing.

This is a REASONED HYPOTHESIS grounded in reading both files, NOT a confirmed bug yet -- no
screenshot or direct alpha/rend.enabled sample was captured live before Play Mode stopped on
its own (see below). Confirm it with real evidence before proposing any fix.

# OBJECTIVE

1. REPRODUCE AND CONFIRM (or refute) the vanish-during-Chasing hypothesis with direct
   evidence, not inference:
   a. Enter Play Mode in Sandbox_AvatarCompanion.unity (or check if it's already running --
      it has been observed stopping on its own twice already this session for an unknown
      reason, possibly a concurrent Editor session or Unity's Error Pause reacting to an
      unrelated NullReferenceException spam from ShowPath.cs -- if you find out WHY Play Mode
      stops on its own, report that too, it's an open question).
   b. Trigger the route (GuideStateHUD, press R, or call TriggerRoute() directly via
      execute_script -- same pattern used in prior sessions, see
      docs/superpowers/plans/2026-09-03-fake-route-rig-prompt.md for the exact API).
   c. While it's cycling, sample repeatedly (every 1-2 real seconds, over at least one full
      ~8-second cycle) via execute_script: AIAvatarGuideController.CurrentState, the
      horizontal distance from the avatar to Camera.main, and
      AvatarSafetyFade.CurrentAlpha / AvatarSafetyFade.IsFadedOut (both public properties,
      already exist, no new code needed to read them). Also take at least one
      computer/screenshot-equivalent capture (Coplay's scene/game view capture tool if
      available) at the moment IsFadedOut reads true, if it ever does.
   d. Report the exact samples (numbers, not descriptions) showing whether distance actually
      drops to <= 0.5m and whether IsFadedOut actually flips to true during a Chasing
      catch-up.

2. SEPARATELY, characterize the periodic-loop mechanism itself (this part IS already
   confirmed via logs, you're characterizing it further, not re-proving it):
   a. Confirm whether the loop is purely a SANDBOX TESTING ARTIFACT (nobody sustaining
      forward WASD movement with SimpleSandboxFreeCam.cs long enough for userS to advance
      past each waypoint) vs a structural bug that would also occur with a real human walking
      normally. Do this by actually driving Camera.main forward with SUSTAINED WASD input
      (simulate via the Input System, or if that's impractical headlessly, reason from
      SimpleSandboxFreeCam.moveSpeed=1.3 vs AIAvatarGuideController.moveSpeed=1.4 -- if the
      user's forward speed is close to or slower than the avatar's leg speed, the avatar
      could plausibly always stay just out of advanceTriggerDistance reach, which would
      explain a persistent stall even with genuine continuous forward movement. Show the
      actual numbers/reasoning, don't just assert an answer).
   b. Report whether this seems like a sandbox-only testing artifact (low urgency) or something
      that would realistically happen on a real device with a real user walking normally
      (higher urgency, would need a real fix to the state machine, not just a workaround).

3. DO NOT apply any fix yet. Report findings only, with a section proposing what a fix
   SHOULD look like GIVEN what you found (e.g. "give Chasing its own stopping distance
   larger than AvatarSafetyFade.fadeStartDistance instead of sharing waypointArrivalEpsilon
   with the waypoint-arrival logic" is one plausible direction if the vanish hypothesis is
   confirmed -- but propose this only if your own evidence supports it, and name what you'd
   need to check before committing to it, e.g. whether fadeStartDistance/fadeEndDistance are
   themselves tunable without breaking the documented 034-A safety guarantee).

# STYLE

Precise and evidence-backed, same as prior rounds. Every claim needs a cited number from a
Coplay tool result, not a description of intent. If you cannot reproduce something, say so
explicitly rather than assuming the hypothesis above is correct.

# TONE

Skeptical of your own hypothesis (the one handed to you above) until you've actually seen the
numbers. It's a strong lead, not a foregone conclusion.

# AUDIENCE

Project owner (Bagus) and a Claude Code session that will independently verify everything
before it's committed -- assume zero shared memory, name every file/line/value explicitly.

# RESPONSE FORMAT

1. Whether Play Mode was already running or you started it, and whether it stopped on its
   own again during your session (if so, at what point, and any log evidence of why).
2. Direct sample evidence for objective 1: state, distance-to-camera, CurrentAlpha,
   IsFadedOut, sampled across at least one full Chasing cycle, with actual numbers and
   timestamps.
3. Verdict: is the vanish-during-Chasing hypothesis confirmed, partially confirmed, or
   refuted -- with the evidence that makes you say so.
4. Findings for objective 2 (sandbox-artifact vs structural), with the reasoning/numbers
   that back it up.
5. A proposed fix DIRECTION (not an implementation) for whichever findings turned out to be
   real bugs, with open questions you'd want answered before implementing it.
6. Anything noticed but deliberately not touched.

# CONSTRAINTS

- Scene scope: Assets/Scenes/Sandbox_AvatarCompanion.unity ONLY. Do NOT touch
  TestingHCM.unity.
- Do NOT modify AIAvatarGuideController.cs, AvatarSafetyFade.cs, GuideStateHUD.cs, or
  SimpleSandboxFreeCam.cs. Read-only investigation only -- no fix in this pass.
- Do NOT modify AvatarGuide.controller.
- Do NOT touch the Cari Teman feature, walkClipSpeed, MediumRun, or the orphaned
  Point/PointDir animator states -- unrelated to this task.
- Do NOT run git commit, git push, or git checkout. Nothing should be staged or committed
  from this pass -- it's read-only investigation.
- This working directory is shared with another session and may have unrelated
  uncommitted/dirty files in it -- if you see dirty files outside what you touched, leave
  them exactly alone, do not investigate or revert them.
- Do NOT leave Play Mode running when you finish -- exit it after your investigation is
  done.
- If any step fails or you get stuck, STOP and report what you tried and what happened. Do
  not substitute a different approach without flagging it as a deviation.
```
