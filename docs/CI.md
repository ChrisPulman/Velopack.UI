# Build and signed release automation

`BuildOnly.yml` restores and builds the solution in Release with warnings treated as errors, then runs the TUnit executable. It runs on pull requests, main/master pushes, and manual dispatch using a hosted Windows runner. PR jobs have read-only repository permissions and no signing secrets.

`BuildDeploy.yml` runs when a **stable GitHub release is published**. Tags must be `vMAJOR.MINOR.PATCH`, `VMAJOR.MINOR.PATCH` or `MAJOR.MINOR.PATCH`; version components must fit Windows assembly fields. Publish a new, increasing version from reviewed code. Prereleases are excluded. A failed workflow can be rerun from Actions after correcting configuration; asset uploads use the same release and preserve its title and notes.

The release job builds and tests, publishes a self-contained `win-x64` app, sets and checks the assembly version from the tag, and installs **vpk 1.2.161**, matching the application's Velopack package. It packs `Velopack.UI` on the Windows default channel `win` with `Velopack.UI.exe` as its entry point. It never skips Velopack's startup integration check.

The newest completed earlier stable Windows release supplies the full package for delta generation. A first release produces no delta; later releases must produce one. Listing/downloading failures stop the workflow rather than falling back to an initial release. The job verifies signatures in the newly built full package and Setup installer before uploading any assets. It uploads the full package, delta when applicable, Setup, and portable ZIP first, then `releases.win.json`. The manifest lists only assets on that release; older manifests/packages stay attached to their own releases. This supports the application's stable `GithubSource` and default `win` channel. Keep previous releases available so older installations can obtain their update paths.

## Signing setup

Provision a dedicated Windows x64 runner with PowerShell 7, GitHub CLI (`gh`), the .NET 10 SDK/runtime, and a reviewed Windows build of [SafeSign](https://github.com/glennawatson/SafeSign). The workflow does **not** download or execute an unverified signer installer. Install a specific reviewed SafeSign release, record its version and SHA256 independently during provisioning, and restrict modification of that executable. SafeSign supports Certum, local PFX files, certificate stores, Azure Artifact Signing and other cloud providers.

Create the `release-signing` GitHub environment and restrict it to trusted release tags. Add required reviewers as appropriate for your signing account. The default runner labels are `self-hosted`, `Windows`, `X64`, and `velopack-signing`. Never use this signing runner for untrusted pull requests.

Configure these GitHub Actions variables (repository variables, or environment variables except `RELEASE_RUNNER`, which must be a repository variable because it selects the runner):

| Variable | Value |
| --- | --- |
| `RELEASE_RUNNER` | Optional JSON labels, e.g. `["self-hosted","Windows","X64","velopack-signing"]`. |
| `SAFESIGN_PATH` | Absolute path to the reviewed `safesign.exe`. |
| `SAFESIGN_SHA256` | Exact 64-character SHA256 digest of that executable. |
| `SIGNING_CERTIFICATE_THUMBPRINT` | Exact 40-character SHA1 thumbprint of your code signing certificate. This identifies the certificate; signatures use SHA256. |

Configure protected `release-signing` environment secrets:

| Secret | Value |
| --- | --- |
| `CERTUM_SIMPLYSIGN_EMAIL` | SimplySign account email. |
| `CERTUM_SIMPLYSIGN_TOTP_SECRET` | TOTP **seed** (base32 or supported `otpauth://` URI) for repeatable automated signing. |
| `CERTUM_SIMPLYSIGN_OTP` | Alternative current six-digit one-time code for a manually prepared run. It can expire before signing starts. |

For Certum, configure the email and one of the TOTP seed or current OTP. SafeSign also supports refresh/access tokens through its provider environment variables. SafeSign 1.1.0 uses `SAFESIGN_CERT_FILE` / `SAFESIGN_CERT_PASSWORD`, `SAFESIGN_CERT_THUMBPRINT`, and `AZURE_ARTIFACT_SIGNING_*`; the release script maps the workflow aliases below to those native names. Keep credentials in protected secrets; they pass through environment variables, never command-line arguments. The workflow uses the scoped `GITHUB_TOKEN` for uploads; no GitHub PAT is required.

Set `SAFESIGN_PROVIDER` to a SafeSign provider ID (default `certum`) and `SIGNING_TIMESTAMP_URL` to the provider's RFC 3161 authority (default `http://time.certum.pl`). For `file`, configure `SAFESIGN_FILE_PATH` and secret `SAFESIGN_FILE_PASSWORD`. For `store`, configure `SAFESIGN_STORE_THUMBPRINT`. For `azure-artifact-signing`, configure `AZURE_CODESIGNING_ENDPOINT`, `AZURE_CODESIGNING_ACCOUNT`, `AZURE_CODESIGNING_PROFILE`, `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and secret `AZURE_CLIENT_SECRET`. Other providers can use their documented environment variables on the dedicated runner; add workflow mappings when supplying them as GitHub secrets.

Unsigned supported additional payload files are signed before packaging, including nested installers and scripts. Existing valid or untrusted publisher signatures are preserved; damaged or unsupported signatures stop the release. Velopack's custom signing template signs installer artifacts and any remaining unsigned PE files. Newly signed files must have a valid timestamped signature from the configured certificate.


Missing signing settings, wrong signer hash, authentication failures, wrong certificate, untrusted or missing signatures, or absent timestamps stop the release. There is no unsigned fallback. The Velopack signing template validates each file it signs; the distributed application's EXE/DLL and Setup are checked again against the configured certificate. Already trusted third-party binaries can retain their original publisher signatures under Velopack's signing behavior.

If no dedicated runner is available, the release job waits for one; it cannot silently publish unsigned binaries. With a configured runner but missing credentials it fails. A published release can remain temporarily without update assets while its job builds or if the job fails. Installers and update manifests become available only after successful verification and upload.

## Verification and limitations

PowerShell script parsing and missing-configuration rejection can be checked locally without signing credentials:

```powershell
Get-ChildItem .github/scripts/*.ps1 | ForEach-Object {
    $tokens = $null; $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors) { throw ($errors.Message -join [Environment]::NewLine) }
}
. .github/scripts/Signing.ps1
Assert-SigningConfiguration # deliberately fails when signing is unconfigured
```

The workflow requires a real configured signing provider and provisioned runner for end-to-end verification. Local syntax checks do not demonstrate successful cloud signing, GitHub publication, installation, or two-version automatic updates. Validate a first signed release by checking Setup's Authenticode signer/timestamp, installing it, publishing a higher version, and confirming the installed application downloads and applies that version.

Primary references: [Velopack custom signing](https://docs.velopack.io/packaging/signing), [vpk 1.2.161 signing implementation](https://github.com/velopack/velopack/blob/1.2.161/src/vpk/Velopack.Packaging.Windows/CodeSign.cs), and [SafeSign providers, formats and environment variables](https://github.com/glennawatson/SafeSign).
