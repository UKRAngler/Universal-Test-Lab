# Universal Test Lab v0.12.0-beta.1

This is the first public beta of the expanded Universal Test Lab mission studio. The core workflows are usable, while experimental cross-vehicle injection and modern ground-vehicle proxies still have known limitations.

## Highlights

- Aircraft, helicopter, drone, and playable ground-vehicle test missions.
- Nation, rank, and vehicle-type filters with configurable modules and loadouts.
- Native mounts plus optional experimental cross-vehicle weapon injection.
- Aircraft fuel, gun belts, countermeasure stations, ground ammunition, mobility, targets, maps, and reusable presets.
- Native helicopter optics, thermal view, weapon switching, guns, and countermeasures.
- Immediate unlimited player respawn, fast target recovery, unlimited mission fuel/ammunition, and one-second rearm after depletion.
- Single-window WPF interface with dark glass styling, overlays, DPI scaling, and a dedicated Support page.

## Known beta limitations

- Native weapons are the most reliable. Injected weapons may lack a compatible seeker, radar, data link, targeting pod, HUD integration, or visual model on the receiving vehicle.
- Modern player tanks use a reserve `userVehicles` proxy. The selected projectile can use its real generated ballistics while the HUD icon, ammunition card, or kill feed still identifies it as M74.
- Some research-only ground systems, including the Black Night laser rangefinder in current testing, may not initialize through the reserve proxy.

## Installation

1. Download and extract `Universal_Test_Lab_v0.12.0-beta.1.zip`.
2. Run `UniversalTestLab.exe` and select the War Thunder root folder.
3. Select **Sync Base**, configure a vehicle, and generate the test mission.
4. For aircraft and helicopters, close and reopen the War Thunder **User Missions** tab. For a changed player tank, restart War Thunder once so the ground proxy reloads.

The EXE is not digitally signed, so Windows SmartScreen may display a warning. Universal Test Lab is intended for local, offline User Missions.

See the included README for generated-file locations, complete removal steps, controls, and detailed limitations.
