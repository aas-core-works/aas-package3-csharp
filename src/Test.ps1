#!/usr/bin/env pwsh

<#
.SYNOPSIS
    This script runs all the unit tests.
#>

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot Common.psm1) -Function `
    GetSamplesDir

function Main
{
    $samplesDir = GetSamplesDir
    if(!(Test-Path $samplesDir))
    {
        throw (
            "The directory containing samples could not be found: " +
            "$samplesDir; these samples are necessary to " +
            "perform the integration tests. " +
            "Did you maybe forget to download them with DownloadSamples.ps1?"
        )
    }

    if (Test-Path env:SAMPLE_AASX_DIR)
    {
        $prevEnvSampleAasxDir = $env:SAMPLE_AASX_DIR
    }
    else
    {
        $prevEnvSampleAasxDir = $null
    }

    try
    {
        $env:SAMPLE_AASX_DIR = $samplesDir

        & dotnet test -c DebugSlow `
            /p:CollectCoverage=true `
            /p:CoverletOutputFormat=opencover
        if ($LASTEXITCODE -ne 0)
        {
            throw "The unit tests failed."
        }
    }
    finally
    {
        if ($null -ne $prevEnvSampleAasxDir)
        {
            $env:SAMPLE_AASX_DIR = $prevEnvSampleAasxDir
        }
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
