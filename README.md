# DEVOUR Freeze, No-clip & Unlimited UV Mod

Freeze the main monster, fly through walls, and keep your UV light charged in **DEVOUR** on Windows Steam. Includes VR controller shortcuts and keyboard controls for **solo or host sessions**.

**Current version: v0.4.0.** Targets **DEVOUR 6.0.34 / Steam build 25725702**, Unity 2022.3.62f2, Windows x64 IL2CPP. The installer and mod check the exact game build and stay disabled if it changes.

[**Download the latest release**](https://github.com/BoostedMage1612/DEVOUR-Freeze-Noclip-UV-Mod/releases/latest) · [All versions](https://github.com/BoostedMage1612/DEVOUR-Freeze-Noclip-UV-Mod/releases) · [Changelog](CHANGELOG.md)

## Install or update

1. Close DEVOUR.
2. Download **DEVOUR_Monster_Freeze_v0.4.0.zip** from the release's **Assets** section.
3. Right-click the ZIP and choose **Extract All**. Keep the extracted folder.
4. Run **INSTALL.cmd**. It locates the Steam installation or lets you choose it.
5. Launch through Steam in VR or desktop mode and start a solo/host round.
6. Check that the upper-right desktop overlay says **DEVOUR Mod v0.4.0**.

Use the attached installer ZIP, not GitHub's automatic **Source code** downloads. The ZIP includes the compiled mod, MelonLoader 0.7.3, installer and support scripts; you do not need to compile anything. First launch may take several minutes while MelonLoader generates support files and may prompt for the official .NET 6 Desktop Runtime.

The installer supports direct upgrades from our v0.1.0, v0.2.0, v0.2.1, v0.2.2, v0.3.0 and v0.3.1 packages. It checks the previous files and preserves unrelated mods, generated bindings, original game binaries and saves.

## Features

- **Monster freeze:** toggle the main monster's behavior and attack suppression.
- **No-clip flight:** move through walls, rise, descend and hover using mapped VR controls or the keyboard.
- **Unlimited UV:** your local player's UV starts full and stays full during use in Easy, Normal, Hard and Nightmare. It is automatic; use the game's normal UV control. Nightmare receives the native charged-battery state. Native difficulty, stun strength and ability restrictions remain in effect.
- **Compact desktop overlay:** version, Freeze status and No-clip status.
- **Diagnostic export:** F9 or the supplied command creates a troubleshooting ZIP.

## VR controls

| Input | Action |
| --- | --- |
| Left thumbstick click alone | DEVOUR's normal run action |
| Right thumbstick click alone | Toggle no-clip **when released** |
| Both thumbstick clicks held together | Toggle monster freeze once |
| Movement joystick while flying | Move horizontally through walls |
| Assigned Jump while flying | Rise |
| Assigned Crouch/Duck while flying | Descend |
| Assigned Sprint while flying | Move faster |

Either stick can be pressed first for freeze. Once both clicks overlap, the combination consumes the right click, so releasing it does not also toggle no-clip. Release **both** before the next freeze combination. These are the physical left and right sticks in either handedness setting.

Flight follows DEVOUR's Head / Controller / Character movement direction setting. Native turning and headset looking remain available. Releasing movement/jump/crouch hovers; looking up or down does not change altitude. Descending uses the mapped crouch button, not physically bending down.

Menus, chat, pause, player/scene changes and controller interruptions cancel pending gestures. Release both stick clicks after an interruption before trying again.

## Keyboard controls

| Input | Action |
| --- | --- |
| F6 | Toggle monster freeze |
| F7 | Toggle no-clip |
| F9 | Export diagnostics |
| WASD while flying | Move |
| Space while flying | Rise |
| Assigned crouch while flying | Descend |
| Shift while flying | Move faster |

## Behavior and compatibility

- Designed for your solo game or a session you host. Joined-client operation is not supported.
- UV targets only the exact local player's battery. It does not change other players' batteries, health or poison. Native knockout and other ability restrictions still apply.
- No-clip ends on knockout, being carried/held, hiding, player replacement, round ending or host-session loss. Exiting within an obstacle returns to the no-clip entry position; exiting in midair restores gravity.
- The overlay is on the desktop game view; a headset-mounted menu is not included.
- Different game builds, additional mod combinations, and every map/controller/difficulty combination are not individually verified.

## Troubleshooting and removal

Press **F9**, or close the game and run **EXPORT_REPORT.cmd**. Reports are saved inside the extracted package's **Reports** folder. If you move that folder, run INSTALL.cmd again to update the in-game export destination.

For an issue, include the mod version, game build, map, difficulty, VR controller (if used), and what happened. Review any diagnostic ZIP before posting it publicly; it can contain game logs and local installation paths.

To remove the mod, close DEVOUR and run **REMOVE_MOD.cmd** from the package. Modified files and unrelated mods are preserved.

## Versions and verification

v0.4.0 is the first GitHub release. Future releases will have their own version tags, notes and installer ZIPs, with earlier downloads kept under **Releases**.

The owner reported **v0.4.0 working successfully** before requesting this publication. The downloadable ZIP is byte-for-byte the same package they tested; publication makes no gameplay changes.

Original validation: **118 offline regression checks** passed, including the existing freeze/VR/flight controls and UV scenarios, plus **8 installation/report/removal checks**. The mod compiled against the game's generated bindings without warnings. These offline tests use stand-ins and do not establish live compatibility with every configuration. [VALIDATION.txt](VALIDATION.txt) preserves the original pre-playtest record.

Installer ZIP SHA-256:

```text
733387C41DCF5D80CCBCA30B95F74D46AF637C97A9D45CE8D5EA85022E0E219F
```

## Source and building

This repository contains the C# source, regression tests, Windows support scripts and payload manifests. The compiled DLL and loader archive are distributed in the release ZIP. Downloading this repository alone is not an installation package.

With a .NET 6 SDK, run the stand-in regression checks:

```sh
dotnet run --project Tests/Regression.csproj -c Release
```

After MelonLoader has generated the reference assemblies in your DEVOUR installation:

```sh
dotnet build Source/DevourMonsterFreeze.csproj -c Release -p:GameDir="C:/path/to/DEVOUR"
```

See [BUILD_NOTES.txt](BUILD_NOTES.txt) for implementation details and the exact target fingerprints, and [READ_ME_FIRST.txt](READ_ME_FIRST.txt) for the packaged instructions.

Unofficial fan-made mod; a copy of DEVOUR is required. No original game binaries, assets, saves or generated game assemblies are included. See [third-party notices](THIRD_PARTY_NOTICES.md) for the bundled loader.
