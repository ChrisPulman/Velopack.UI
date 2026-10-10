param([Parameter(Mandatory)][string] $FilePath)

. "$PSScriptRoot/Signing.ps1"
Assert-SigningConfiguration
& $env:SSIGN_PATH $FilePath
if ($LASTEXITCODE -ne 0) { throw 'Certum signing failed.' }
Assert-ReleaseSignature $FilePath
