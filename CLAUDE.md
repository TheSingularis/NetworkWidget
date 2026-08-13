# NetworkWidget

## Branching and releases

- `dev` is the integration branch. All ongoing work (features, fixes) is committed and pushed to `dev`.
- `main` only ever advances together with a tagged release - `main` == the latest shipped version, always. Merge `dev` into `main` exactly when cutting a release, never as a standalone push.
- Every merge to `main` bumps at least the patch version (`X.Y.Z` -> `X.Y.(Z+1)`) in `NetworkWidget.csproj`, as part of that merge. Minor/small changes (bug fixes, small feature additions) get a patch bump specifically, not a minor version bump. Reserve a minor bump for a clearly larger feature (e.g. the auto-update/Velopack integration, 1.1.0 -> 1.2.0) or a breaking change.
- Tagging (`git tag vX.Y.Z && git push origin vX.Y.Z`, which triggers the GitHub Actions release build) happens as part of that same main-merge, only when explicitly asked to ship/release/send it - not on every dev push.
- CI/CD: pushes to `dev` trigger a quick sanity build (plain exe artifact, no packaging). Pushes to a `vX.Y.Z` tag trigger the full Velopack release build (Setup.exe, portable zip, update feed) and publish a GitHub Release.
