#!/usr/bin/env pwsh

<#
.SYNOPSIS
This script publishes the library to the artefacts.
#>

Import-Module (Join-Path $PSScriptRoot Common.psm1) -Function `
    AssertDotnet,  `
    CreateAndGetArtefactsDir

function Main
{
    Set-Location $PSScriptRoot

    AssertDotnet

    $artefactsDir = CreateAndGetArtefactsDir
    $outputDir = Join-Path $artefactsDir "release"

    & dotnet publish -c Release -o $outputDir
    if ($LASTEXITCODE -ne 0)
    {
        throw "doctest-csharp failed; see the log above."
    }
}

$previousLocation = Get-Location; try
{
    Main
}
finally
{
    Set-Location $previousLocation
}
