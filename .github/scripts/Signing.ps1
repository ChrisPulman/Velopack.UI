Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-SigningConfiguration {
    if (-not $IsWindows) { throw 'Release signing requires Windows.' }
    foreach ($name in @('SSIGN_PATH', 'SSIGN_SHA256', 'SIGNING_CERTIFICATE_THUMBPRINT', 'CERTUM_EMAIL')) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            throw "Required signing configuration is missing: $name. Unsigned releases are prohibited."
        }
    }
    $hasSeed = -not [string]::IsNullOrWhiteSpace($env:CERTUM_OTP)
    $hasCode = -not [string]::IsNullOrWhiteSpace($env:CERTUM_TOKEN)
    if ($hasSeed -eq $hasCode) { throw 'Configure exactly one of CERTUM_OTP (TOTP seed) or CERTUM_TOKEN (one-time code).' }
    if ($env:SSIGN_SHA256 -notmatch '\A[0-9a-fA-F]{64}\z') { throw 'SSIGN_SHA256 must be a SHA256 digest.' }
    if ($env:SIGNING_CERTIFICATE_THUMBPRINT -notmatch '\A[0-9a-fA-F]{40}\z') { throw 'SIGNING_CERTIFICATE_THUMBPRINT must be a certificate SHA1 thumbprint.' }
    if (-not [IO.Path]::IsPathFullyQualified($env:SSIGN_PATH) -or -not (Test-Path -LiteralPath $env:SSIGN_PATH -PathType Leaf)) {
        throw 'SSIGN_PATH must point to a preinstalled, reviewed ssign executable using an absolute path.'
    }
    if ((Get-FileHash -LiteralPath $env:SSIGN_PATH -Algorithm SHA256).Hash -ne $env:SSIGN_SHA256) {
        throw 'ssign executable hash does not match SSIGN_SHA256.'
    }
}

function Assert-ReleaseSignature([string] $Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Thumbprint -ne $env:SIGNING_CERTIFICATE_THUMBPRINT -or
        $null -eq $signature.TimeStamperCertificate) {
        throw "Valid timestamped signature from the configured certificate is required: $Path ($($signature.Status))."
    }
}
