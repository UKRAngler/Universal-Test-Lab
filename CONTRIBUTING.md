# Contributing

Thanks for helping improve Universal Test Lab.

## Before opening an issue

- Use the latest release.
- Rebuild and launch a new mission; existing generated missions do not update automatically.
- Check the README's **Known beta limitations**. The reserve M74 HUD card and missing research-only systems on a modern ground proxy are already tracked.
- Include the Universal Test Lab release, War Thunder version, vehicle, module configuration, ammunition or pylon setup, target, and map.
- Attach screenshots and the generated mission when possible. For a ground-proxy problem, also attach the generated `content/pkg_local/gameData/units/tankModels/userVehicles/us_m2a4.blk` proxy and `utl_ground_cannon.blk`.
- Never post account credentials or private data.
- Do not upload complete extracted War Thunder archives or other copyrighted game resources.

## Code changes

1. Create a focused branch.
2. Keep UI text in English.
3. Do not commit unpacked War Thunder archives, user account data, generated missions, or local game paths.
4. Run `.\Build.ps1 -SelfTest`.
5. Describe the user-visible effect and testing performed in the pull request.

Keep pull requests focused. A change that updates generated BLK syntax should include a small self-test or fixture-level assertion where practical, plus the exact in-game behavior that was verified manually.

Cross-aircraft injection is intentionally experimental. A weapon appearing on a pylon does not guarantee that the selected aircraft provides every seeker, radar, data-link, or targeting-pod dependency.
