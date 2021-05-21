<#
.SYNOPSIS
This script checks that all the TODOs in the code follow the convention.
#>

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot Common.psm1) -Function `
    AssertDotnet,  `
    AssertDotnetToolVersion

function Main
{
    AssertDotnetToolVersion -packageID "opinionatedusings" -expectedVersion "1.0.0-pre2"

    Set-Location $PSScriptRoot
    Write-Host "Inspecting the using directives in the code..."
    dotnet opinionated-usings `
        --inputs '**/*.cs' `
        --excludes 'packages/**' '**/obj/**' '**/bin/**'
    if($LASTEXITCODE -ne 0)
    {
        throw (
            "The opinionated-usings check failed. " +
            "Please have a close look at the output above, " +
            "in particular the lines prefixed with `"FAILED`"."
        )
    }
}

$previousLocation = Get-Location; try { Main } finally { Set-Location $previousLocation }
