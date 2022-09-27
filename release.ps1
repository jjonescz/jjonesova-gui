[CmdletBinding(PositionalBinding=$false)]
param (

)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

Write-Output "Working directory: $pwd"

# Load current Git tag.
$tag = $(git describe --tags)
Write-Output "Tag: $tag"

# Parse tag into a three-number version.
$version = $tag.Split('-')[0].TrimStart('v')
$version = "$version.0"
Write-Output "Version: $version"

# Clean output directory.
$projDir = "src/JonesovaGui"
$outDir = "$projDir/bin/publish"
if (Test-Path $outDir) {
    Remove-Item -Path $outDir -Recurse -Force
}

# Publish the application.
Push-Location $projDir
try {
    Write-Output "Restoring:"
    dotnet restore -r win-x64
    Write-Output "Publishing:"
    MSBuild /v:m /target:publish /p:PublishProfile=ClickOnceProfile `
        /p:ApplicationVersion=$version
}
finally {
    Pop-Location
}
