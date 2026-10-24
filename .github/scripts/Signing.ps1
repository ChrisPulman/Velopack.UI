Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-SigningConfiguration {
    if (-not $IsWindows) { throw 'Release signing requires Windows.' }
    foreach ($name in @('SAFESIGN_PATH', 'SAFESIGN_SHA256', 'SIGNING_CERTIFICATE_THUMBPRINT', 'SAFESIGN_PROVIDER', 'SIGNING_TIMESTAMP_URL')) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            throw "Required signing configuration is missing: $name. Unsigned releases are prohibited."
        }
    }
    if ($env:SAFESIGN_PROVIDER -notmatch '\A[a-z][a-z0-9-]*\z') { throw 'SAFESIGN_PROVIDER must be a provider identifier.' }
    $timestamp = $null
    if (-not [Uri]::TryCreate($env:SIGNING_TIMESTAMP_URL, [UriKind]::Absolute, [ref]$timestamp) -or $timestamp.Scheme -notin @('http', 'https')) { throw 'SIGNING_TIMESTAMP_URL must be an HTTP(S) timestamp authority URL.' }
    if ($env:SAFESIGN_SHA256 -notmatch '\A[0-9a-fA-F]{64}\z') { throw 'SAFESIGN_SHA256 must be a SHA256 digest.' }
    if ($env:SIGNING_CERTIFICATE_THUMBPRINT -notmatch '\A[0-9a-fA-F]{40}\z') { throw 'SIGNING_CERTIFICATE_THUMBPRINT must be a certificate SHA1 thumbprint.' }
    if (-not [IO.Path]::IsPathFullyQualified($env:SAFESIGN_PATH) -or -not (Test-Path -LiteralPath $env:SAFESIGN_PATH -PathType Leaf)) {
        throw 'SAFESIGN_PATH must point to a preinstalled, reviewed SafeSign executable using an absolute path.'
    }
    if ((Get-FileHash -LiteralPath $env:SAFESIGN_PATH -Algorithm SHA256).Hash -ne $env:SAFESIGN_SHA256) {
        throw 'SafeSign executable hash does not match SAFESIGN_SHA256.'
    }
}

function Invoke-ReleaseSigning([string] $Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    # Preserve signatures from other publishers. Broken signatures require investigation.
    if ($signature.Status -in @('Valid', 'NotTrusted')) { return }
    if ($signature.Status -ne 'NotSigned') { throw "Refusing to replace an invalid signature: $Path ($($signature.Status))." }
    # SafeSign 1.1 uses these names; retain the documented aliases in workflow configuration.
    $aliases = @{
        SAFESIGN_CERT_FILE = 'SAFESIGN_FILE_PATH'
        SAFESIGN_CERT_PASSWORD = 'SAFESIGN_FILE_PASSWORD'
        SAFESIGN_CERT_THUMBPRINT = 'SAFESIGN_STORE_THUMBPRINT'
        AZURE_ARTIFACT_SIGNING_ENDPOINT = 'AZURE_CODESIGNING_ENDPOINT'
        AZURE_ARTIFACT_SIGNING_ACCOUNT = 'AZURE_CODESIGNING_ACCOUNT'
        AZURE_ARTIFACT_SIGNING_PROFILE = 'AZURE_CODESIGNING_PROFILE'
    }
    foreach ($name in $aliases.Keys) {
        if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($name))) {
            [Environment]::SetEnvironmentVariable($name, [Environment]::GetEnvironmentVariable($aliases[$name]))
        }
    }
    & $env:SAFESIGN_PATH sign $Path --provider $env:SAFESIGN_PROVIDER --hash SHA256 --timestamp-url $env:SIGNING_TIMESTAMP_URL
    if ($LASTEXITCODE -ne 0) { throw 'SafeSign signing failed.' }
    Assert-ReleaseSignature $Path
}

function Assert-ReleaseSignature([string] $Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Thumbprint -ne $env:SIGNING_CERTIFICATE_THUMBPRINT -or
        $null -eq $signature.TimeStamperCertificate) {
        throw "Valid timestamped signature from the configured certificate is required: $Path ($($signature.Status))."
    }
}
