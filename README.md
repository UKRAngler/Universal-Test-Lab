# Universal Test Lab

> **Public beta — v0.12.0-beta.1:** the core aircraft, helicopter, ground-vehicle, loadout, target, preset, and mission-generation workflows are usable. Some experimental ground-proxy features still have engine-level limitations; read [Known beta limitations](#known-beta-limitations) before reporting a bug.

Universal Test Lab is a Windows GUI for building hot-load War Thunder User Missions with configurable aircraft, helicopters, drones, and playable ground vehicles, plus custom loadouts and air, ground, or naval targets.

The interface uses a hardware-rendered WPF shell with custom glass-style window chrome, per-monitor DPI scaling, dark dropdown menus, and a responsive **Vehicle → Loadout → Scenario** workflow.

![Universal Test Lab application](docs/application.png)

## Features

- 1,568 air vehicles, including 114 helicopters and the event V-1, with nation, rank, and vehicle-type filters.
- 1,249 playable ground vehicles with the same nation, rank, and research-module workflow, including an **Event / Experimental** category for units such as the Goliath 303a.
- Stable maximize/restore and DPI-aware scaling without WinForms resize flicker or clipped fixed-width filters.
- Modules, configuration, map, preset, Support, confirmation, and result panels open as overlays inside the main window, with the inactive workspace blurred behind them.
- 1,838 weapon, rack, targeting-pod, and experimental ground-SAM entries.
- Native aircraft mounts and optional cross-aircraft weapon injection.
- The central weapon catalog is divided by weapon type with visible section headers.
- Per-vehicle research-modification selection, including alternative fixed guns such as the Yak-9UT's N-37 and NS-45.
- A dedicated **Flight Configure** window with starting fuel in minutes, gun-ammunition belts unlocked by the current Belt Pack configuration, and exact flare/chaff sliders for every installed dispenser, including flare-only/chaff-only stations and modules such as BOL, BKO, and AMASE.
- Editable internal bomb bays for aircraft such as the B-52H and Tu-95M.
- **Ground Configure** supports four ammunition types with per-type sliders constrained by the vehicle's real total capacity. Foreign-shell injection and projectile, cannon, and mobility values use editable `kg`, `m/s`, `mm`, `s`, `hp`, and `km/h` fields with per-field and one-click stock reset.
- Ground presets can bind a custom `.blk` reticle found in the current War Thunder `UserSights` folders. The selected reticle is copied to the generated vehicle folder and attached to that generated vehicle ID; the original sight file is never modified.
- **Map & Targets** configures all seven ground-range positions independently, includes nation/rank filters for ground and naval catalogs, and uses explicit green **PASSIVE** / red **ATTACKING** or **RETURNS FIRE** target-state controls.
- Named custom presets stored locally on the PC.
- The selected War Thunder folder is remembered, so the EXE can be kept anywhere.
- Hot mission rebuilding for aircraft, helicopters, and drones. Playable ground proxies are initialized when War Thunder starts and require one restart after changing the player tank.
- Configurable internal starting fuel and adaptive initial/respawn speeds: 1,100 km/h for modern jets, 700 km/h for early jets (Rank V or below), 450 km/h for propeller aircraft, a stationary hover for helicopters, and 100 km/h for the FPV drone.
- Explicit unlimited mission ammunition and fuel, a one-second native rearm delay for depleted weapon groups, rapid target recovery, and automatic zero-delay player respawn without the four-attempt limit.

## Installation

1. Download the latest `Universal_Test_Lab` ZIP from Releases and extract it.
2. Keep `UniversalTestLab.exe` in any folder and run it.
3. Select the War Thunder root folder once, then select **Sync Base**. The path is saved locally for future launches.
4. Move through **Choose Vehicle → Build Loadout → Configure Test**, then select **Generate Test Mission**.
5. In War Thunder, close the **User Missions** tab/window completely and open **User Missions** again. This refreshes the generated mission.
6. Launch the current **HOT UTL** mission.

For a playable ground vehicle, generate the mission first and then restart War Thunder once. The game caches the reserve-tank proxy at startup; launching a newly selected modern tank without that restart can show the previous reserve shell and disable the gun.

The executable is not digitally signed, so Windows SmartScreen may show a warning.

Simply returning to the already-open User Missions list is not enough after regeneration; the tab must be closed and opened again.

Use **Modules** beside the pylon controls to switch from all researched modifications to a stock/selective setup. Research modifications are arranged in horizontal rank columns like the in-game modification screen, and every rank has its own select/clear toggle. Use **Flight Configure** for aircraft fuel, gun belts, and countermeasures; use **Ground Configure** for shells, gun behavior, and mobility; and use **Map & Targets** for every opposition position. These settings are kept separately for each selected vehicle and are included when a custom preset is saved.

For helicopters, War Thunder uses a separate control context. Bind **Fire primary weapons**, **Fire secondary weapons**, **Switch secondary weapons**, and **Fire countermeasures** in the Helicopter controls section. The aircraft commands with the same names do not fire the turret, launch or cycle ATGMs, or release countermeasures while the helicopter HUD is active.

For a ground-vehicle preset, open **Presets** and choose **Ground User Sight** before selecting **Save Current**. Universal Test Lab scans the current War Thunder save folders (including the post-2.53 `Documents\My Games\WarThunder\Saves\<user ID>\production\UserSights` location), saves the selected sight with the preset, and binds it to each newly generated ground-vehicle ID. After mission generation, press **Alt+F9** once in the mission to reload `UserSights`. A one-time `global.blk.universal-test-lab-backup` is kept beside War Thunder's `global.blk` before the first automatic binding.

## Important injection limitation

Native weapons remain the most reliable option. Injected weapons are experimental: the receiving vehicle may not provide every seeker, radar, data-link, targeting-pod, HUD, or visual-model dependency required by a foreign weapon.

## Known beta limitations

- Playable modern tanks currently use War Thunder's documented reserve `userVehicles` proxy mechanism. The selected shells fire with their generated ballistic and penetration data, but the HUD icon, ammunition card, and kill feed can still identify every slot as the proxy tank's M74 shell.
- The generator writes the selected research modules and materializes their detected effects. War Thunder can nevertheless keep the proxy vehicle's research-system state, so equipment such as the Black Night laser rangefinder may be unavailable in the mission.
- These two ground-proxy issues are confirmed limitations of the current beta and are not considered fixed. Please avoid filing duplicate reports unless a test includes a new reproducible workaround or a generated mission from a newer release.
- Cross-vehicle weapon injection remains experimental. Visual mounting does not guarantee that the receiving vehicle supplies the required seeker, radar, data link, targeting pod, or HUD integration.

Aircraft and helicopter generation, native helicopter controls and optics, custom air loadouts, target configuration, presets, and the selected shell's actual ground-weapon behavior are the most reliable parts of the current beta.

## Generated files and complete removal

Universal Test Lab does not replace War Thunder's packed game archives. It creates the following mission and custom-content files inside the selected War Thunder folder:

- `UserMissions\Universal Test Lab\` — the starter mission, current `universal_test_lab_hot.blk`, and `usr.csv` localization.
- `content\pkg_user\levels\Clean_Testdrive.bin`, `Clean_Testdrive.blk`, and `Clean_Testdrive_map.png` — the clean test range.
- `content\pkg_user\gameData\flightModels\utl_safe_player.blk`, generated `utl_run_*_player.blk` files, and `weaponPresets\utl_run_*_loadout.blk` loadouts.
- `content\pkg_user\gameData\Weapons\rocketGuns\utl_cm\` and `utl_sam\` — generated countermeasure belts and experimental ground-SAM adapters when used.
- `content\pkg_local\gameData\units\tankModels\userVehicles\us_m2a4.blk` and `content\pkg_local\gameData\Weapons\groundModels_weapons\utl_ground\` — the playable ground-vehicle proxy and generated cannon.
- `%LOCALAPPDATA%\UniversalTestLab\` — the remembered game path and locally saved custom presets.

If a custom ground sight is attached, the application also creates a generated `UserSights\us_m2a4\` folder containing `.universal-test-lab-generated`, adds an `us_m2a4` sight entry to the account's `global.blk`, and keeps a one-time `global.blk.universal-test-lab-backup` beside it.

To remove Universal Test Lab completely:

1. Close Universal Test Lab and War Thunder.
2. Delete `UserMissions\Universal Test Lab\` and only the UTL files/folders listed above. Do not delete the complete `content\pkg_user`, `content\pkg_local`, `UserSights`, or `Saves` folders because they may contain unrelated user content.
3. Delete `%LOCALAPPDATA%\UniversalTestLab\` if you also want to remove the saved game path and presets.
4. If a custom ground sight was attached, delete `UserSights\us_m2a4\` only when it contains the `.universal-test-lab-generated` marker. Remove the `us_m2a4` entry from `tankSightSettings` in `global.blk`, or carefully compare it with `global.blk.universal-test-lab-backup`. Do not blindly restore the backup if War Thunder settings changed after it was created.

## Support the project

If Universal Test Lab is useful to you, optional support is available through the secure [Stripe payment page](https://buy.stripe.com/bJe00bbHB0GH0qI655fQI00). The repository's **Sponsor** button opens the same page.

<a href="https://buy.stripe.com/bJe00bbHB0GH0qI655fQI00"><img src="docs/support-qr.png" alt="QR code for optional project support" width="260"></a>

## Community inspiration

Universal Test Lab is an independent project by AstraSEP. Its concept was inspired by GUI and custom-mission projects shared by War Thunder community members and YouTube channels, for example Ask3lad. Those creators are sources of general inspiration and are not contributors to Universal Test Lab.

## Building from source

Requirements:

- Windows
- .NET Framework 4.x
- PowerShell

Build and run both the mission-core and WPF UI smoke tests:

```powershell
.\Build.ps1 -SelfTest
```

The compiled application is written to `dist\UniversalTestLab.exe`. WPF is included with the supported Windows/.NET Framework runtime, so no separate UI runtime or browser component is required.

`Build-Catalog.ps1` regenerates catalog TSV files from locally extracted War Thunder resources. Those extracted source archives are intentionally not included in this repository.

## Contributing

Bug reports and feature proposals are welcome through GitHub Issues. See [CONTRIBUTING.md](CONTRIBUTING.md) before submitting code changes and [SECURITY.md](SECURITY.md) for security reports. The [release checklist](docs/RELEASE_CHECKLIST.md) records the checks required before publishing a beta build.

## Legal and third-party software

Universal Test Lab is an independent fan-made project and is not affiliated with or endorsed by Gaijin Entertainment. War Thunder and related names and assets belong to their respective owners.

The application embeds [`wt_ext_cli`](https://github.com/Warthunder-Open-Source-Foundation/wt_ext_cli) under the Apache License 2.0. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and [resources/WT_EXT_LICENSE.txt](resources/WT_EXT_LICENSE.txt).

The project source is available under the [MIT License](LICENSE). Third-party components and game-derived data or resources are excluded from that license unless their own notice says otherwise.
