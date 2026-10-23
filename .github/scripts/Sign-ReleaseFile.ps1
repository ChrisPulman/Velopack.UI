param([Parameter(Mandatory)][string] $FilePath)

. "$PSScriptRoot/Signing.ps1"
Assert-SigningConfiguration
Invoke-ReleaseSigning $FilePath
