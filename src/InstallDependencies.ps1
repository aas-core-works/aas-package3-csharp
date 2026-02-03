#!/usr/bin/env pwsh
<#
.SYNOPSIS
This script runs all the pre-merge checks locally.
#>

$ErrorActionPreference = "Stop"

function Main
{
    Set-Location $PSScriptRoot

    Write-Host "Restoring the dependencies..."
    dotnet restore

    Write-Host "Restoring the tools..."
    dotnet tool restore
}

$previousLocation = Get-Location; try
{
    Main
}
finally
{
    Set-Location $previousLocation
}
