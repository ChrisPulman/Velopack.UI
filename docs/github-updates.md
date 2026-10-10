# GitHub release links and updates

Keep each version in its own GitHub release. GitHub's `/releases/download/<tag>/...` URLs identify assets belonging to that version.

The permanent update source for this app is `https://github.com/ChrisPulman/Velopack.UI`. Pass the repository URL to `Velopack.Sources.GithubSource`, then pass that source to `UpdateManager`. Velopack.UI already uses this configuration in `App.xaml.cs` with prereleases disabled.

```csharp
var source = new GithubSource("https://github.com/ChrisPulman/Velopack.UI", accessToken: null, prerelease: false);
var updates = new UpdateManager(source);
```

Velopack's [GithubSource implementation](https://github.com/velopack/velopack/blob/1.2.161/src/lib-csharp/Sources/GithubSource.cs) queries the repository's releases. Its [GitBase implementation](https://github.com/velopack/velopack/blob/1.2.161/src/lib-csharp/Sources/GitBase.cs) reads the channel's `releases.<channel>.json` from those releases and downloads packages from the release containing their feed entry. Keep every package referenced by a release's manifest attached to that same release. A complete publish uploads the release folder's files together. When publishing updates, include the full package so clients can update even without a usable delta chain.

For a permanent installer download link, use [latest stable setup](https://github.com/ChrisPulman/Velopack.UI/releases/latest/download/Velopack.UI-win-Setup.exe). GitHub redirects this to its latest stable release; that release must contain the matching setup asset. This installer link and a version-specific setup link are distinct from the repository URL used by `GithubSource`.

The GitHub connection editor displays all three links. Set the package title and channel consistently between releases so the setup filename remains stable. Draft releases are unavailable to public clients, and prereleases require `prerelease: true` in the updater.

Read-only release inspection on 2026-10-10 found V1.1.0 contains `releases.win.json`, `assets.win.json`, `RELEASES`, the 1.1.0 full package, portable ZIP, and setup EXE. Separate version paths are expected and do not require combining existing releases.
