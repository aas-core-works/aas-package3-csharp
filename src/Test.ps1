#!/usr/bin/env pwsh

<#
.SYNOPSIS
    This script runs all the unit tests.
#>

$ErrorActionPreference = "Stop"

function Main
{
    & dotnet test -c DebugSlow
    if ($LASTEXITCODE -ne 0)
    {
        throw "The unit tests failed."
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
