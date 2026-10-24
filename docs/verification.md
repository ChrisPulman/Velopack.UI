# Verification: package management, controls, and SafeSign

Verified locally on Windows on 23 October 2026.

| Check | Result |
| --- | --- |
| Central Package Management restore | Passed; project and shared package references have central versions. |
| Release solution build, warnings treated as errors | Passed with no warnings or errors. |
| TUnit suite | 76 passed, no failures or skips; includes compiled WPF view construction, model/persistence, file system and GitHub publishing helpers, signing configuration, credentials, preservation, and cancellation boundaries. |
| Vulnerability audit, including transitive packages | No known vulnerable packages reported by NuGet. |
| Release PowerShell scripts | All parse successfully. |
| Desktop startup | Final Release build opened successfully; theme-aware labels are readable on the dark theme. |
| Project editing | Temporary project saved; New reset its identity; canceling Save As during close retained the unsaved project and edits. |
| CrissCross numeric input | Zero clamps to the minimum of one on commit; increment changes one to two. |
| Complete local packaging | The application produced version 1.0.0 Setup, portable ZIP, full package and update indexes. The busy overlay disabled the editor and displayed progress/cancellation. |
| Update-only local packaging | The application produced version 1.0.1 full and delta packages; the update queue excluded Setup/portable files and completed every item. |
| Signed installer integration | The vpk signing template signed 197 application files and Setup. Packaged app EXE/DLL and Setup used the temporary certificate; numeric Windows integrity checks accepted them. Existing Microsoft DLL bytes and publisher were preserved. |
| Actual SafeSign 1.1.0 file provider | A temporary self-signed PFX signed EXE, DLL and PowerShell fixtures. The app wrapper preserved signed fixture hashes, signed a fresh DLL, and rejected a tampered signed script unchanged. No certificate trust-store changes were made. |

Global `vpk` was updated from 0.0.1298 to 1.2.161. Startup readiness has tests for install/update, exact version verification, concurrent callers, and retry after failure. Desktop checks also exposed and fixed recent-project preference deserialization; the saved project now reopens at startup. The final app then produced version 1.0.2 full/delta packages and completed the update queue using matching vpk 1.2.161. Tool child processes disable optional CLI telemetry/workload advertising; package signature verification remains enabled.

The machine has SDK 10.0.401, while the repository requests 10.0.402. Verification selected the installed stable SDK from an isolated temporary working directory containing a matching `global.json`; the repository SDK requirement was preserved.

The required Mtpunittestmcp and Codex Computer Run servers were unavailable. The user approved running the TUnit executable and the Computer Use desktop fallback. Reactive Memory was used; unavailable Reactive Multi Agent tools were replaced by host subagents.

Live Certum/Azure/cloud/HSM authentication, trusted timestamped release signing, remote GitHub/S3 publication, MSI deployment tooling, installation/uninstallation and installed two-version automatic updates require separate verification with provisioned services and a disposable installation environment. These checks do not establish that every remote provider or installer lifecycle function works.

SafeSign's current README differs from released 1.1.0 environment names. Compatibility aliases cover the common documented file/store/Azure names; advanced providers may require their native environment names. Automatic extra-payload signing is restricted to Windows formats whose existing signatures can be checked safely. See the README and toolkit assessment for supported formats and control behavior changes.
