# Velopack.UI — by Chris Pulman

<img alt="Velopack.UI Application Logo" src="src/Velopack.UI/Images/Application.png" width="128" />

A simple, focused desktop UI for building and publishing .NET application installers powered by Velopack.

Velopack.UI helps you:
- Configure metadata (App ID, Version, Title, Description, Authors)
- Curate installer content via drag and drop (tree view)
- Generate delta packages and optional MSI installers (x86/x64)
- Configure code signing once and reuse safely
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
- Add the Velopack NuGet package to your project `<PackageReference Include="Velopack" Version="0.0.1298" />`.
- Add an `UpdateManager` to your app to handle updates.
- Add the Velopack bootstrapper/setup to your project `VelopackApp.Build().Run()` [see Velopack docs](https://docs.velopack.io/getting-started/csharp).
- Collect the output: the main EXE plus all required DLLs/content.

### Quick start for Velopack.UI
1) Prerequisites
   - Windows 10/11
   - .NET 9 Desktop Runtime (for running) and SDK if you build Velopack.UI from source
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
   - Delta packages, MSI generation (x86/x64), and signing.
9) Publish
   - Publish Only Update Files → push update artifacts only.
   - Publish Complete Setup → push full bootstrapper/setup as well as update artifacts.
10) Always publish to the same content folder to maintain update continuity.
   - Increment the version for each release. Do not reuse versions.
   - The base folder for the installers can contain multiple apps (different App IDs).
   - Velopack.UI creates a folder per `App ID` under the content folder i.e. `AppID_Files`
   - Velopack.UI creates two folders for the releases `AppID_Files\Releases` and `AppID_Files\PackageFiles`
   - The `AppID_Files\Releases` folder contains the RELEASES file and the setup.exe, this is the folder you point your users to for downloading the installer and should be retained for future updates.
   - The `AppID_Files\PackageFiles` folder contains the project files ready for publishing, this folder can be deleted after publishing if required.


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
  - Delta package generation
  - MSI generation with bitness selection
  - Signing: Sign Params + Sign Template
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
   - Delta Packages: creates smaller update packages between versions to reduce download size.
   - MSI Generation: adds an MSI installer (choose x86/x64) alongside the standard setup.
   - Signing: provide parameters or a template controlling how binaries and packages are signed.
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

For exact configuration strings and provider‑specific options, refer to the Velopack packaging docs: [Velopack Packaging](https://docs.velopack.io/category/packaging)


### Tips, troubleshooting, and FAQs
- Drag & drop not working
  - Ensure Velopack.UI is not running as Administrator.
- Versioning mistakes
  - Don’t reuse older versions. Increment versions for each release. Changing App ID breaks update continuity.
- MSI generation
  - If MSI output is missing, verify Generate MSI is enabled and a valid bitness is selected.
- Signing failures
  - Check certificate access, passwords, and timestamp server availability. Try signing a single file with your params to validate.
- Upload errors
  - Validate credentials/permissions and that the destination path exists. Check network/firewall and retry.

---

Velopack.UI by Chris Pulman. Built to streamline packaging and publishing with Velopack for .Net applications.
