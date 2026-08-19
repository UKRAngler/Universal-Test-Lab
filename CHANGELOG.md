# Changelog

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
