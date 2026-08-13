# NetworkWidget

## Versioning

- Every merge to `main` bumps at least the patch version (`X.Y.Z` -> `X.Y.(Z+1)`) in `NetworkWidget.csproj`, as part of that merge - not just when cutting a release. Minor/small changes (bug fixes, small feature additions) get a patch bump specifically, not a minor version bump. Reserve a minor bump for a clearly larger feature (e.g. the auto-update/Velopack integration, 1.1.0 -> 1.2.0) or a breaking change.
- A version bump on `main` is not itself a release. Tagging (`git tag vX.Y.Z && git push origin vX.Y.Z`, which triggers the GitHub Actions release build) is a separate, explicit step - only do it when asked to ship/release/send it. Until tagged, a bumped version on `main` is "in dev."
