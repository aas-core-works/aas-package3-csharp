#!/usr/bin/env pwsh
<#
.SYNOPSIS
This script generates the documentation for developers in the docdev directory.
#>

Import-Module (Join-Path $PSScriptRoot Common.psm1) -Function `
    CreateAndGetArtefactsDir


function Main
{
    Set-Location $PSScriptRoot

    $artefactsDir = CreateAndGetArtefactsDir

    $repoDir = Split-Path -Parent $PSScriptRoot
    $docfxJson = Join-Path $repoDir "doc" "docfx.json"

    & dotnet docfx $docfxJson
    if($LASTEXITCODE -ne 0)
    {
        throw "docfx failed. See above for error logs."
    }

    $siteDir = Join-Path $artefactsDir "gh-pages" `
        | Join-Path -ChildPath "doc"

    Write-Host "The documentation has been generated to: '$siteDir'"
    Write-Host "You can serve it locally with:"
    Write-Host "dotnet docfx serve '$siteDir'"
}

$previousLocation = Get-Location; try { Main } finally { Set-Location $previousLocation }
