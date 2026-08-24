# Beta release checklist

Use this checklist before publishing a Universal Test Lab beta release.

## Repository

- [ ] The version and date are recorded in `CHANGELOG.md`.
- [ ] README features and known limitations match current in-game testing.
- [ ] `LICENSE`, `THIRD_PARTY_NOTICES.md`, and `resources/WT_EXT_LICENSE.txt` are included.
- [ ] No local game paths, saves, account data, generated missions, diagnostics, or extracted game archives are staged.
- [ ] The GitHub Actions build passes on `main`.

## Build

- [ ] Run `.\Build.ps1 -SelfTest` on Windows.
- [ ] Run `.\Package-Release.ps1 -Version v0.12.0-beta.1` and inspect the ZIP contents.
- [ ] Launch the resulting `dist\UniversalTestLab.exe` on a clean path outside the War Thunder folder.
- [ ] Confirm the game folder is selected, displayed, and remembered.
- [ ] Record the SHA-256 checksum beside the release download.

## In-game smoke test

- [ ] Generate and launch one native fixed-wing mission.
- [ ] Generate and launch one injected fixed-wing loadout.
- [ ] Generate and launch one helicopter mission; verify optics, thermal view, gun, secondary-weapon switching, and countermeasures.
- [ ] Generate and launch one ground mission after restarting War Thunder; verify cannon, machine guns, respawn, and actual projectile behavior.
- [ ] Confirm that the User Missions tab must only be closed and reopened for ordinary mission refreshes.
- [ ] Confirm presets, Support links, Map & Targets, and custom UserSight binding.

## Current expected beta limitations

- Modern ground vehicles use a reserve `userVehicles` proxy. The selected projectile can have correct real ballistics while the HUD icon, stat card, or kill feed still says M74.
- Some module-only ground systems, including the Black Night laser rangefinder in current testing, may not initialize through the reserve proxy.
- Cross-vehicle weapon injection may lack a compatible seeker, radar, data link, targeting pod, HUD entry, or visual model.

Do not describe any item above as fixed until it has been reproduced successfully in a fresh in-game test.
