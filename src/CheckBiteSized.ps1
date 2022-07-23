#!/usr/bin/env pwsh
<#
.SYNOPSIS
This script checks that the C# files are "bite sized": no long lines, not too long.
#>

Import-Module (Join-Path $PSScriptRoot Common.psm1) -Function `
    AssertDotnetToolVersion

function Main {
    AssertDotnetToolVersion -PackageID "BiteSized" -ExpectedVersion "2.0.0"

    Set-Location $PSScriptRoot

    dotnet bite-sized `
        --inputs "**/*.cs" `
        --excludes `
            "**/obj/**" `
            "packages/**" `
        --max-lines-in-file 2000 `
        --max-line-length 90 `
        --ignore-lines-matching '[a-z]+://[^ \t]+$'

    if($LASTEXITCODE -ne 0)
    {
        throw (
            "The bite-sized check failed. " +
            "Please have a close look at the output above, " +
            "in particular the lines prefixed with `"FAIL`"."
        )
    }
}

$previousLocation = Get-Location; try { Main } finally { Set-Location $previousLocation }
