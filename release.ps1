[CmdletBinding(PositionalBinding=$false)]
param (

)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

Write-Output "Tag: $(git describe --tags)"
Write-Output "Working directory: $pwd"
