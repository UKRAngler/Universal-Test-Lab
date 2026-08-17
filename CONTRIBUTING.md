# Contributing

Thanks for helping improve Universal Test Lab.

## Before opening an issue

- Use the latest release.
- Rebuild and launch a new mission; existing generated missions do not update automatically.
- Include the selected aircraft, pylon number, weapon, target, map, and War Thunder version.
- Attach screenshots or the relevant generated mission details when possible.
- Never post account credentials or private data.

## Code changes

1. Create a focused branch.
2. Keep UI text in English.
3. Do not commit unpacked War Thunder archives, user account data, generated missions, or local game paths.
4. Run `.\Build.ps1 -SelfTest`.
5. Describe the user-visible effect and testing performed in the pull request.

Cross-aircraft injection is intentionally experimental. A weapon appearing on a pylon does not guarantee that the selected aircraft provides every seeker, radar, data-link, or targeting-pod dependency.
