DEVOUR Mod v0.4.0 - Unlimited UV + Freeze + VR No-clip

UPDATE
1. Close DEVOUR.
2. Extract this entire ZIP to a normal folder.
3. Double-click INSTALL.cmd. It upgrades your v0.3.1 directly.
   Our v0.1.0, v0.2.0, v0.2.1, v0.2.2 and v0.3.0 packages are also supported.
4. Launch DEVOUR through Steam in VR and start a solo/host round.
5. Confirm the upper-right desktop overlay says DEVOUR Mod v0.4.0.

UNLIMITED UV - AUTOMATIC
Your local player's UV starts full and stays full while you use it.
Works in Easy, Normal, Hard and Nightmare in solo/host sessions.
No key or controller toggle is needed. Use your normal UV control.
Nightmare receives the game's charged-battery state automatically.
Other difficulties keep their normal full capacity, or an existing battery
bonus capacity. Native difficulty, stun strength and ability restrictions stay
as they are. This does not force UV on while knocked out or otherwise blocked.
The charge is maintained after respawning and returning from purgatory.

VR TOGGLES
LEFT thumbstick click alone: DEVOUR's normal run action. No mod toggle.
RIGHT thumbstick click alone: No-clip ON/OFF when you RELEASE the click.
BOTH thumbstick clicks held together: Freeze ON/OFF when they overlap.

Either stick may be pressed first. There is no narrow timing window: hold one
click and press the other so both are down together. Once the freeze toggle
fires, the right click is consumed and cannot also toggle no-clip on release.
Release BOTH clicks before using the freeze combination again.

The right-only toggle deliberately waits for release to distinguish a single
click from the combination. No-clip does not change from a freeze combination,
including if you were already flying. The mod only observes these inputs;
it leaves DEVOUR's normal run input available, including during a combination.
These use the physical left/right sticks in either handedness setting.

MOVEMENT WHILE NO-CLIP IS ON
Your existing flight controls remain:
- Normal movement joystick: fly horizontally through walls.
- Normal joystick turning and headset looking remain available.
- Hold assigned JUMP: rise. Hold assigned CROUCH/DUCK: descend.
- Release movement/jump/crouch: hover at the current position and height.
- Assigned sprint control: move faster.
Movement follows DEVOUR's Head / Controller / Character direction setting.
Looking up/down does not change flight altitude. Duck means the mapped crouch
button, not physically bending down in the headset.

KEYBOARD
F6: Freeze toggle. F7: No-clip toggle. F9: Export a diagnostic ZIP.
WASD: Move. Space: Rise. Assigned crouch: Descend. Shift: Faster.
Keyboard toggles still activate on key press.

Menus, chat, pause and disabled gameplay input suspend flight input.
VR uses the game's headset-presence state; an unfocused desktop mirror alone
does not block input. Desktop play still requires focus. A menu, scene/player
change or controller interruption cancels a pending right-click toggle.
After spawning or an input interruption, release both clicks before trying
again. A held click carried over from that interruption cannot toggle a feature.

REPORTS AND OVERLAY
F9 and EXPORT_REPORT.cmd save to Reports inside THIS extracted package folder.
Send UPLOAD_THIS_DEVOUR_Freeze_....zip from that folder if a control needs work.
If you move the extracted folder, run INSTALL.cmd again to update the F9 path.
Reports include the control layout, pending-right-click state, VR devices,
counts, movement input names, last no-clip attempt/session, and UV readiness,
charge, difficulty and blocked drain/reset counts.
The desktop overlay remains three lines: version, Freeze, No-clip.

QUICK LIVE CHECK
1. Start a round: UV should already show full. Hold your normal UV control;
   the charge should remain full. Release and use it again.
2. Try Nightmare too: its battery charge should be available immediately.
3. After a respawn/return from purgatory, check that UV is still full.
4. Confirm left-click run, right-click/release no-clip, both-click freeze and
   normal flight/jump/duck still work as before.

No-clip exit and all existing lifecycle behavior are preserved. Exiting inside
an obstacle returns to the position where no-clip began. Exiting in midair
restores gravity. Knockout, being carried/held, hiding, a player change, a
round ending or leaving your host session ends flight.

REMOVE
Close DEVOUR and run REMOVE_MOD.cmd from this package. Changed files and other
mods are preserved. Original game binaries and saves are not replaced.

VALIDATION
The v0.3.1 baseline was confirmed working by the user in VR. This UV update
compiles against the same DEVOUR 6.0.34 bindings. All 118 offline regression
checks passed. Installer/export results are recorded in VALIDATION.txt.
It has not been tested in a running DEVOUR session or on a headset here.
Source and tests are included; no compilation is needed to install.
See BUILD_NOTES.txt and VALIDATION.txt for technical details.
