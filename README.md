# Velopack.UI — by Chris Pulman

<img alt="Velopack.UI Application Logo" src="src/Velopack.UI/Images/Application.png" width="128" />

A simple, focused desktop UI for building and publishing .NET application installers powered by Velopack.

Velopack.UI helps you:
- Configure metadata (App ID, Version, Title, Description, Authors)
- Curate installer content via drag and drop (tree view)
- Configure Velopack pack options including channel, runtime, release notes, delta mode, excludes, frameworks, shortcuts, portable/installer output, signing, Azure Trusted Signing, and MSI deployment tooling
- Publish releases to your chosen destination with an upload queue and progress

Important: Do not run Velopack.UI as Administrator. Windows drag and drop is restricted in elevated apps and will not work.


## Contents
- Create your .NET application to package
- Quick start for Velopack.UI
- Features overview
- Packaging workflow with Velopack.UI
- Prepare a third‑party app for packaging
- Code signing (Sign Params and Sign Template)
- Publishing destinations
- Tips, troubleshooting, and FAQs

### Create your .NET application to package
Before using Velopack.UI, ensure your application is built and ready for packaging:
- Build your app in Release mode for the target runtime (framework‑dependent or self‑contained).
- Add the Velopack NuGet package to your project `<PackageReference Include="Velopack" Version="1.2.0" />`.
- Add an `UpdateManager` to your app to handle updates.
- Add the Velopack bootstrapper/setup to your project `VelopackApp.Build().Run()` [see Velopack docs](https://docs.velopack.io/getting-started/csharp).
- Collect the output: the main EXE plus all required DLLs/content.

### Quick start for Velopack.UI
1) Prerequisites
   - Windows 10/11
   - .NET 10 SDK if you build Velopack.UI from source; release installers include the runtime
   - [Latest Velopack.UI release](https://github.com/ChrisPulman/Velopack.UI/releases/latest)
   - Your application’s build output (Release) ready to package (EXE + all dependencies)
   - A code signing solution (optional but recommended).
2) Launch Velopack.UI (not elevated)
   - Running as Admin disables drag and drop.
3) Create or open a project
   - Project menu → Create New or Open. Save regularly.
4) Add application assets
   - Use the Installer Package Content panel and drag in your built files/folders. Ensure your main executable is included at the root of the content.
5) Modify / Fill out Package Details
   - App ID, Version (or enable Set Version Manually), Title, Description, Authors. This information will be read automatically from the EXE if it exists and the fields are left blank.
6) Choose output folders
   - Output: Content Folder → where Velopack builds the nupkg content.
   - Output: Releases Folder → where final releases (RELEASES, packages, MSI) are written.
7) Configure upload location
   - Pick/edit a connection (Project → Edit Connection). See Publishing destinations for examples.
8) Adjust Velopack options
   - Channel/runtime, delta mode, release notes, framework bootstrapping, shortcuts, output suppression, signing, Azure Trusted Signing, and MSI deployment tooling.
9) Publish
   - Publish Only Update Files → push update artifacts only.
   - Publish Complete Setup → push full bootstrapper/setup as well as update artifacts.
10) Always publish to the same content folder to maintain update continuity.
   - Increment the version for each release. Do not reuse versions.
   - The base folder for the installers can contain multiple apps (different App IDs).
   - For File System output, the folder selected in Edit Connection is the project/output root.
   - Velopack.UI writes only the `.velo` project file at the root and derives two child folders from it: `PackageFiles` and `Releases`.
   - `Releases` contains Velopack output such as release indexes, nupkg files, portable zips, and setup executables. This is the folder you point users to for downloads and retain for future updates.
   - `PackageFiles` is a staging folder recreated from the original source paths stored in the `.velo` project whenever publishing runs.


### Features overview
- Package Details editor
  - App ID, Versioning (manual toggle), Title, Description, Authors
- Content authoring
  - Drag and drop files/folders into a structured tree
  - File detail pane (size, last edit, source path, icon)
- Output configuration
  - Separate content and releases output folders
- Publishing
  - Upload queue with progress and status per file
  - Two publish modes: Update-only and Complete setup
- Velopack options
  - Channel and runtime selection
  - Release notes, delta mode, package exclude regex, and framework bootstrapping
  - Portable/installer suppression and shortcut locations
  - Signing: Sign Params, Sign Template, Sign Exclude, Sign Parallel, and Azure Trusted Signing metadata
  - MSI deployment tool generation and version override
- Project management
  - New, Open, Save, Save As


### Packaging workflow with Velopack.UI
1) Define Package Details
   - App ID: A unique, stable identifier for your app (do not change once released).
   - Version: Semantic-like version string. If Set Version Manually is off, the tool may infer version from your files or previous releases; turn it on to specify explicitly.
   - Title/Description/Authors: Shown in installer metadata and release notes.
2) Curate Installer Content
   - Drag and drop your release build output. Include your main EXE at the root.
   - Keep only the files your app needs. Remove development artifacts (pdbs, test data) unless you intend to ship them.
3) Set Outputs
   - Content Folder: temporary content staging.
   - Releases Folder: final packages and RELEASES file appear here.
4) Configure Upload Location
   - Select a saved connection or Edit Connection to define a new one (see Publishing destinations).
5) Configure Velopack Options
   - Channel/runtime: define update channel and supported OS/architecture.
   - Delta Mode: choose BestSpeed, BestSize, or None.
   - Release notes/exclude/frameworks/shortcuts: maps directly to Velopack `vpk pack` options.
   - Signing: provide parameters, templates, exclusions, parallelism, or Azure Trusted Signing metadata.
   - MSI Deployment Tool: optionally emit Velopack's machine-wide deployment MSI tooling.
6) Publish
   - Use Publish Only Update Files for incremental update deployments.
   - Use Publish Complete Setup when you want to distribute the full installer as well.


### Prepare a third‑party app for packaging
Use this checklist to make an external application ready for packaging with Velopack.UI:
- Build the app in Release mode for the target runtime (framework‑dependent or self‑contained as needed).
- Collect the output: the main EXE plus all required DLLs/content.
- Ensure the app runs from a folder without installers (portable run). Fix missing dependencies.
- Choose a stable App ID (unchanging across releases) and a versioning strategy.
- Provide an application icon (ICO) and optional splash asset.
- If signing:
  - Decide the signing method (PFX + SignTool, Azure Code Signing, HSM/token, or a signing service).
  - Keep secrets out of source: prefer environment variables, CI secrets, or secure key stores.
- Decide on a publishing destination and set up credentials (see below).
- In Velopack.UI:
  1. Enter Package Details.
  2. Drag in the app’s files to Installer Package Content.
  3. Set output folders.
  4. Configure signing.
  5. Configure upload location.
  6. Publish.


### Code signing (Sign Params and Sign Template)
Velopack supports customizable signing to ensure the shipped binaries and installers are trusted by Windows SmartScreen and not tampered with. In Velopack.UI you’ll see two inputs:

[Velopack Signing Docs](https://docs.velopack.io/packaging/signing)

- Sign Params
  - A parameter string used by the underlying signing tool. Typical values include digest algorithms, certificate details, and timestamp servers.
  - Example (PFX using SignTool):
    - /fd sha256 /a /f "C:\certs\mycert.pfx" /p %PFX_PASSWORD% /tr http://timestamp.digicert.com /td sha256
  - Guidance:
    - Prefer environment variables for secrets (e.g., %PFX_PASSWORD%).
    - Use a reliable RFC‑3161 timestamp service (e.g., DigiCert, GlobalSign).
    - Ensure your certificate is valid for code signing and not expired.

- Sign Template
  - An advanced template allowing fine‑grained control over which files are signed and how. This is useful when you want to sign only specific file types (e.g., .exe, .dll, .msi) or customize commands per artifact.
  - Keep your template in source control without secrets. Substitute secrets at build time via environment variables or your CI.
  - See the official Velopack packaging docs for template structure and supported variables:  [Velopack Packaging](https://docs.velopack.io/category/packaging)

#### Tips
- Sign your binaries and also the installer artifacts to minimize SmartScreen prompts.
- If you use a hardware token or cloud signing (e.g., Azure Code Signing), adapt Sign Params/Template to your provider’s CLI.
- Test locally first; then replicate in CI using the same parameters.


### Publishing destinations
Velopack supports multiple publishing options. In Velopack.UI, set the Upload Location and Edit Connection accordingly. Common destinations include:
- GitHub Releases: publish packages, RELEASES, and setup artifacts to a GitHub repository release.
- Cloud object storage (e.g., S3, Azure Blob, DigitalOcean Spaces): host your releases behind HTTPS.
- Generic web/FTP server: publish to a web directory that your app will use for updates.
- Local/network folder: useful for testing / internal / self distribution.

GitHub Releases uses GitHub CLI authentication by default. Install `gh`, run `gh auth login --web --hostname github.com --scopes repo`, then paste a repository URL such as `https://github.com/ChrisPulman/Velopack.UI` in the GitHub Releases connection editor. Velopack.UI uses `gh release create` for a new tag and `gh release upload --clobber` for an existing tag, so personal access tokens do not need to be stored in `.velo` files.

For exact configuration strings and provider‑specific options, refer to the Velopack packaging docs: [Velopack Packaging](https://docs.velopack.io/category/packaging)


### Tips, troubleshooting, and FAQs
- Drag & drop not working
  - Ensure Velopack.UI is not running as Administrator.
- Versioning mistakes
  - Don’t reuse older versions. Increment versions for each release. Changing App ID breaks update continuity.
- MSI deployment tooling
  - If MSI deployment-tool output is missing, verify MSI Deployment Tool is enabled and the installed `vpk` version supports it.
- Signing failures
  - Check certificate access, passwords, and timestamp server availability. Try signing a single file with your params to validate.
- Upload errors
  - Validate credentials/permissions and that the destination path exists. Check network/firewall and retry.

### SafeSign signing

Enable **SafeSign**, select an installed `safesign.exe`, and choose a provider. SafeSign supports Certum SimplySign, local PFX (`file`), certificate stores (`store`), Azure Artifact Signing and other cloud/HSM providers. Configure the timestamp URL and provider environment variables described in the [SafeSign documentation](https://github.com/glennawatson/SafeSign). Clear the legacy Sign Params, Sign Template and Azure metadata options when using SafeSign.

For Certum, enter the account email and current six-digit authenticator code, or supply `CERTUM_SIMPLYSIGN_TOTP_SECRET` through the environment. Provider credentials entered for the current session are excluded from saved `.velo` projects and inherited only by child signing processes. For a PFX, use `SAFESIGN_FILE_PATH` and `SAFESIGN_FILE_PASSWORD`; for a certificate store use `SAFESIGN_STORE_THUMBPRINT`. The app maps these aliases to SafeSign 1.1.0 native `SAFESIGN_CERT_FILE`, `SAFESIGN_CERT_PASSWORD`, and `SAFESIGN_CERT_THUMBPRINT`.

SafeSign signs supported unsigned payload files and the installer, including MSI. Existing signatures are preserved; damaged signatures stop packaging. Velopack signs PE files after preparing executable resources, while additional supported payload formats are signed in the staging directory before packing. Keep signing concurrency at 1 for providers that restrict concurrent operations. Automatic payload signing covers EXE/DLL/EFI, MSI/MSP/MST, CAB/CAT and supported Windows scripts. Other SafeSign formats (including MSIX/AppX, NAVX, RPM and NuGet) require a separate workflow because this app cannot safely inspect their existing signatures.

### Application updates and CI

On startup, the application installs or updates global `vpk` to the version defined centrally for its Velopack dependency. Packaging waits for this preparation, failures report a repair command, and a later attempt can retry. This requires a .NET SDK and NuGet access when a tool change is needed. Already matching tools are reused. The background CLI processes opt out of optional telemetry and workload advertising so those requests do not delay startup.

Installed copies check this repository's stable GitHub releases at startup and download available updates through Velopack. The application asks before restarting, warns about unsaved changes, and defers restart while packaging is active. Declining keeps the downloaded update for a later launch. Running from source skips installed-application updates.

Pull requests run the Release build and TUnit suite through `BuildOnly.yml`. Signed releases use the same version for application binaries and Velopack packages. See [CI and signing configuration](docs/CI.md) for the signing runner, required credentials, release tags, and verification steps.

---

Velopack.UI by Chris Pulman. Built to streamline packaging and publishing with Velopack for .Net applications.
