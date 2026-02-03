#!/usr/bin/env pwsh

<#
.SYNOPSIS
This script runs all the pre-merge checks locally.
#>

$ErrorActionPreference = "Stop"

function Main
{
    Set-Location $PSScriptRoot
    & dotnet run --project CheckScript
}

$previousLocation = Get-Location; try
{
    Main
}
finally
{
    Set-Location $previousLocation
}
