# Changelog

## v0.12.0-beta.2 — 2026-08-27

### Added

- Added an optional **Combined Battles — Domination** solo sandbox. It currently includes 48 datamine-backed map layouts with side selection, two ground spawns, an airfield and air start for aircraft, and near/far helicopter pads. Native A/B/C zones and the two teams' relevant respawn locations are shown on the tactical map for orientation, while the generator adds no AI units, active capture logic, score, or match progression.
- Added `Build-CombinedMaps.ps1` to regenerate the embedded spawn and realistic A/B/C marker catalog from extracted `mis.vromfs.bin` missions while excluding maps without a complete two-sided spawn set.

### Fixed

- Combined-battles capture points remain visible on the tactical map for every vehicle, but their rapidly changing world-distance labels are now limited to ground vehicles, matching normal combined battles.
- Combined-battles aircraft missions now register a 40 km aviation-only map area centered between the native airfields and air spawns. The tactical map can zoom out to the full aviation scale instead of remaining constrained to the ground-battle view; this map area does not create an out-of-bounds death zone.

## v0.12.0-beta.1 — 2026-08-24

### Added

- Added 1,249 playable ground vehicles with an M1A2 SEP v3 preview, nation/rank/type filtering, research-module selection, and locally generated custom tank definitions.
- Added an **Event / Experimental** vehicle nation containing game-file-only units such as Goliath 303a and the V-1 (Fi 103).
- Added **Ground Configure** with four ammunition types, total native vehicle capacity, dynamically constrained per-type count sliders, native or cross-vehicle projectile injection, and direct real-value controls for projectile mass, muzzle velocity, explosive filler, penetration, reload, recoil, engine power, vehicle mass, and forward/reverse speed.
- Expanded **Map & Targets** to configure all seven ground-range positions independently. Ground and naval catalogs now have nation/rank filters, and ships can be set to remain passive after being attacked.
- Added 114 playable helicopters with their native editable weapon stations and a vehicle-type browser filter.
- Added a **Modules** window that can use all research modifications or apply any selective set found in the current vehicle definition. Modules appear in horizontal in-game-style rank columns; alternative module groups remain mutually exclusive and the settings are stored with custom presets.
- Added **Flight Configure** with internal starting fuel in minutes and exact flare/chaff sliders for every installed launcher. Flare-only, chaff-only, BOL, BKO, MAW, and mixed dispensers are configured separately.
- Added selectable cannon ammunition belts to **Flight Configure**. Choices follow the current Belt Pack research-module configuration, are written to the player's mission ammunition groups, and are saved in custom presets.
- Added a dedicated **Map & Targets** window for air, ground, naval, and hostile air-defence targets.
- Added an application-styled mission-generated confirmation window with the required User Missions refresh instructions.
- Added a **Ground User Sight** selector to ground-vehicle presets. It discovers custom `.blk` reticles in current and legacy `UserSights` locations, stores the selection in the preset, copies it into the generated vehicle folder, binds it to the generated vehicle ID, preserves a one-time `global.blk` backup, and adds the required Alt+F9 reload reminder.

### Changed

- Reworded the injection limitation without referring to a user-specific weapon-selection key, clarified that community GUI projects are general inspiration rather than project contributions, and documented every generated file location with safe manual removal steps.

- Removed the experimental helicopter gun dispersion/recoil controls and private copied-gun generation. Helicopters now always retain their native cannon definition.
- Custom aircraft and helicopter loadouts retain the native aircraft-definition station order. This restores the mirrored helicopter pylon order used by the earlier working generator.

- Ground vehicle names now prefer the short shop label, so entries such as M1A2 SEP V2 no longer expose long military catalog descriptions. Projectile names and types are normalized into readable labels such as `120 mm M829A2` and `APFSDS` instead of internal underscored identifiers.
- Corrected the USA catalog label to **M1A2 SEP V3** and made the catalog builder preserve that spelling on regeneration.
- Countermeasure stations now show readable dispenser names without exposing internal emitter identifiers such as `emtr_flare3`; duplicate dispensers are numbered automatically.
- Ground research modules are filtered and assigned to in-game-style Rank I–IV columns. Duplicate/internal blocks are hidden, every rank has a select/clear toggle, and the inactive single-page **MODIFICATIONS** navigation button is removed.
- Ground parameter multipliers were replaced in the interface by typed physical values with individual stock reset controls and a one-click **Reset all to current stock** action.
- Modules content is rebuilt as a rounded inset glass card with a separate rounded selection panel, module viewport and footer, so no square section or control is pressed against the overlay edges.
- Player death no longer enters the four-attempt/60-second flow: the generated mission uses manual, automatic zero-delay respawn. Aircraft retain their class-aware respawn speed, while ground vehicles return directly to the range.
- Range targets now recover after roughly a quarter-second, the APS-test ATGM carrier is included in restoration, and the engine's native player rearm delay is set to one second.
- Dropdown height is measured from its rendered items, removing the extra empty row shown at the bottom of short lists.
- Replaced the WinForms user interface with a hardware-rendered, per-monitor-DPI-aware WPF shell while preserving the tested mission-generation core and embedded catalogs.
- Added custom frameless window chrome with stable maximize/restore, DWM backdrop integration, rounded glass-style cards, and a responsive **Vehicle → Loadout → Scenario** workspace.
- Replaced native system combo boxes and scroll bars with fully themed dark WPF templates. Rank and vehicle filters now keep usable widths at every supported window size, and dropdown menus no longer open as white system lists.
- Rebuilt every modern app dialog as a modal overlay inside the main window. Opening Modules, Flight/Ground Configure, Map, Presets, About, confirmations, errors, or the generated-mission notice now blurs and dims the workspace instead of creating another taskbar window.
- Removed the duplicated purple title strip from every overlay. Dialogs now use one rounded glass surface with a solid red floating close control and no dark overlay on the button.
- Base-install notices, game-resource errors, mission-generation errors and injection confirmations now use the same blurred in-app overlay design instead of native white Windows message boxes.
- The weapon table now groups entries under visible weapon-type section headers and rounds the Mode header only while its vertical scrollbar is present.
- Weapon-table headers now use a fully themed static template, preventing the Windows hover highlight from turning Weapon, Type, Ammo, Mass, or the unrounded Mode header light blue.
- Removed the duplicated DWM client-glass strip below the accent title bar, reduced Modules to a work-area-safe layout, and added a protected bottom scroll inset above Flight Configure actions.
- Added a WPF UI smoke test covering dark dropdown creation, per-monitor DPI, maximize, restore, and responsive layout bounds.
- Helicopters now spawn from a stationary hover. The fixed-wing mission speed field was producing destructive overspeed on helicopter usermodels even when set to 180.
- Rebuilt the air-vehicle, pylon, and weapon catalogs from the current game files.
- Replaced the ambiguous **HOSTILE TARGETS** control with explicit ground-target and ship reaction states. Passive states are green; actively attacking or returning-fire states are red and spelled out on the buttons and scenario summary.
- Renamed the user-facing **Flight Systems / Vehicle Systems** control and dialog to **Modules** throughout the application. Rewrote About around the project, AstraSEP, open-source information, community inspiration and optional support; Ask3lad is presented only as one example of a wider custom-mission GUI community.
- Removed the external inspiration-video link from both About implementations and the README while retaining a short plain-text community acknowledgement.
- Changed only the main workspace brand line to **U.T.L. by AstraSEP** while retaining the Universal Test Lab application name, executable filename and descriptions. The Windows executable now embeds a multi-resolution rounded `U` icon matching the app's title-bar badge.

### Fixed

- Fixed ground ammunition-container discovery operating inside the first cannon block instead of at the cannon file's root. Generated mission slots can now receive the resolved named container (for example `120mm_britain_L27_APDSFS`) instead of always falling back to the nested projectile ID. The remaining in-game HUD behavior stays listed as a beta limitation until it is confirmed in a fresh game test.
- Fixed `Build.ps1 -SelfTest` accepting a crashed GUI executable as a successful test. Core and WPF smoke tests now run as waited processes and fail the local build or GitHub Actions job on any non-zero exit code.

- Restored the full generated helicopter model after the include-only experiment removed thermal imaging. Helicopter missions now keep materialized thermal optics, the native pylon order, and the complete weapon research set needed for SAL/IR stores to appear in the secondary-weapon selector.
- Rebuilt the playable ground block around the known-working reserve proxy while preserving the included vehicle's native weapon controller. The generator now resolves complete named ammunition containers where available; however, War Thunder still presents the reserve M74 icon/card for some modern-tank proxy loadouts. This remains a known beta limitation even when the selected projectile's real ballistics are active.
- Helicopter loadouts now match native War Thunder presets: only external stations are serialized, while turret/fixed guns and countermeasure launchers remain implicit in `commonWeapons`. This restores third-person gun and missile triggers.
- Flight Configure now lists the complete separate helicopter control set: fire primary weapons, fire secondary weapons, switch secondary weapons, and fire countermeasures. The secondary selector requires both its fire and switch commands; aircraft bindings do not activate these helicopter actions.
- Generated helicopter usermodels now explicitly use `expClass:t="exp_helicopter"`. This makes War Thunder initialize the helicopter HUD and helicopter keybind context required for the gunner/targeting optic, sight stabilization, and third-person point-of-interest designation.
- Helicopter thermal sights supplied by research modules are now materialized into the active generated `nightVision` configuration, allowing the targeting optic to switch from daylight view to thermal imaging.
- Aircraft and helicopters are again written directly into the playable `You` unit. Removed the unreliable startup `changeUnit` bridge through a Typhoon, which could leave every selected air vehicle flying as the same Typhoon AESA with AIM-120s.
- Mission-level unlimited fuel and ammunition are enabled explicitly, and the player's native per-weapon rearm time is forced to one second without periodically restoring the whole vehicle or resetting active seekers.
- Ground vehicles retain the selected tank preset and native included cannon while exposing the selected shell IDs only as mission ammunition metadata. Removed the second startup `changeUnit` pass that could discard the cannon and machine guns after the playable tank had already loaded.
- Rotated the playable tank 180 degrees from its previous heading and moved both its initial and respawn positions another 20 metres away from the target tanks.
- Removed the million-round **Unlimited countermeasures** mode. Legacy presets are normalized to finite per-launcher values, preventing extreme countermeasure mass from corrupting aircraft handling and structural behavior.
- Removed the `noAmmo`/`unitRestore` player trigger because empty weapon channels could activate it during aircraft initialization, leave countermeasures empty, and show an endless reload loop. The mission now sets `rearmTimeOnField` once at initialization and lets War Thunder's native unlimited-ammunition system handle replenishment without reinitializing the vehicle.
- Ground usermodels now use Ask3lad's exact `userVehicles/us_m2a4` native-include proxy. This preserves War Thunder's real gun, machine-gun, shell and controller registration; custom mobility and weapon changes are layered on as BLK overrides instead of replacing the complete unit.
- Player ground vehicles now receive a fully trained crew and the selected Modules configuration. Detected module `effects` are also materialized into the generated proxy, but War Thunder may still retain the reserve vehicle's research-system state; the Black Night laser rangefinder is a confirmed example and remains a known beta limitation.
- The generated reserve proxy now exposes a stable copy of the included vehicle's real cannon and preserves complete foreign ammunition containers during projectile injection. The cannon and machine-gun controller remain operational, while HUD shell identity is still controlled by the reserve proxy in affected vehicles.
- Moved the initial and post-death ground spawn to the test-range hangar start, facing the T-34/Sherman line rather than the distant legacy position.
- Rewrote unlimited player respawn and APS-carrier recovery into the expanded field layout emitted by War Thunder Mission Editor. Removed unsupported `unitRespawn` options that caused the generated mission to disappear from User Missions while retaining immediate unlimited respawn.
- Generated missions now always overwrite the stable `universal_test_lab_hot.blk` path instead of creating a new timestamped filename and deleting the previous one. War Thunder can no longer keep a cached User Missions entry that points to a mission file removed by the next generation.
- Fixed newly generated ground missions being rejected by War Thunder with `Can't open file` and disappearing from User Missions. Custom ground units and cannons use the documented `content/pkg_local` hierarchy; maps remain in `content/pkg_user`.
- Fixed ground-vehicle generation failing with “Extracted game resource was not found” even when the War Thunder directory was correct. VROM resource paths are now normalized to the archive's lowercase form before extraction, including mixed-case cannon and projectile paths from the live catalog.
- Rank-level **SELECT/CLEAR RANK** controls are now anchored to the bottom of every Modules column, independent of how many module entries that rank contains.
- Ground-ammunition sliders now keep a stable full-capacity scale. Editing one ammunition slot no longer shifts the other thumbs; each slot shows its current count and dynamically allowed maximum, and over-capacity input is clamped only on the slot being edited.
- Fixed the Game Directory field hiding its value because its inherited vertical padding was taller than the field. Folder selection now synchronizes the visible WPF field immediately, keeps the dialog owned by the main window, and saves every valid selected path for the next launch.

## v0.11.2 — 2026-08-19

### Changed

- Added aircraft-aware initial and respawn speeds: 700 km/h for early jets at Rank V or below and 450 km/h for propeller aircraft.
- Modern jets keep the existing 1,100 km/h profile, while the FPV drone keeps its dedicated 100 km/h profile.
- The application detects jet aircraft from their War Thunder unit definition, so rank alone cannot misclassify high-rank propeller aircraft as jets.

## v0.11.1 — 2026-08-19

### Fixed

- Fixed the CW-21 fatal error on mission start. Older aircraft without an explicit `fmFile` now retain a reference to their original War Thunder flight model instead of making the game search for a nonexistent generated FM file.
- Applied the same compatibility fix generically to other legacy aircraft with the same BLK structure.

### Changed

- Universal Test Lab now remembers the selected War Thunder root folder in `%LOCALAPPDATA%\UniversalTestLab\game_folder.txt`. The application EXE can be stored and launched from any folder.
- The post-generation message now explicitly instructs the user to close and reopen the War Thunder **User Missions** tab before launching the refreshed mission.
