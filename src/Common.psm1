<#
.SYNOPSIS
This module contains common functions for continuous integration.
#>

<#
.SYNOPSIS
Join the path to the directory where build tools reside.
#>
function GetToolsDir
{
    return Join-Path (Split-Path $PSScriptRoot -Parent) "tools"
}

<#
.SYNOPSIS
Asserts that dotnet is on the path.
#>
function AssertDotnet
{
    if (!(Get-Command "dotnet" -ErrorAction SilentlyContinue))
    {
        if ($null -eq $env:LOCALAPPDATA)
        {
            throw "dotnet could not be found in the PATH."
        }
        else
        {
            throw "dotnet could not be found in the PATH. Look if you could find it, e.g., in " + `
               "$( Join-Path $env:LOCALAPPDATA "Microsoft\dotnet" ) and add it to PATH."
        }
    }
}

function FindDotnetToolVersion($PackageID) {
    AssertDotnet

    $version = ''

    $lines = (dotnet tool list)|Select-Object -Skip 2
    $lines += (dotnet tool list -g)|Select-Object -Skip 2
    ForEach ($line in $( $lines -split "`r`n" ))
    {
        $parts = $line -Split '\s+'
        if ($parts.Count -lt 3)
        {
            throw "Expected at least 3 columns in a line of `dotnet tool list`, got output: ${lines}"
        }

        $aPackageID = $parts[0]
        $aPackageVersion = $parts[1]

        if ($aPackageID -eq $PackageID)
        {
            $version = $aPackageVersion
            break
        }
    }

    return $version
}

<#
.SYNOPSIS
Check the version of the given dotnet tool.
 #>
function AssertDotnetToolVersion($PackageID, $ExpectedVersion) {
    AssertDotnet

    $version = FindDotnetToolVersion -PackageID $PackageID
    if ($version -eq '')
    {
        throw "No $PackageID could be found. Have you installed it? " + `
               "Check the list of the installed dotnet tools with: " + `
               "`dotnet tool list` and `dotnet tool list -g`."
    }
    else
    {
        if ($version -ne $ExpectedVersion)
        {
            throw "Expected $PackageID version $ExpectedVersion, but got: $version;" + `
                   "Check the list of the installed dotnet tools with: " + `
                   "`dotnet tool list` and `dotnet tool list -g`."
        }
        # else: the version is correct.
    }
}

<#
.SYNOPSIS
Check the version of dotnet-format so that the code is always formatted in the same manner.
#>
function AssertDotnetFormatVersion
{
    AssertDotnetToolVersion -packageID "dotnet-format" -expectedVersion "5.1.250801"
}

<#
.SYNOPSIS
Check the version of dead-csharp so that the dead code is always detected in the same manner.
#>
function AssertDeadCsharpVersion
{
    AssertDotnetToolVersion -packageID "deadcsharp" -expectedVersion "2.0.0"
}

<#
.SYNOPSIS
Check the version of doctest-csharp so that the code is always generated and checked in the same manner.
#>
function AssertDoctestCsharpVersion
{
    AssertDotnetToolVersion -packageID "doctestcsharp" -expectedVersion "2.0.0"
}

<#
.SYNOPSIS
Check the version of opinionated-csharp-todos so that the TODOs are always inspected in the same manner.
#>
function AssertOpinionatedCsharpTodosVersion
{
    AssertDotnetToolVersion -packageID "opinionatedcsharptodos" -expectedVersion "2.0.0"
}

function GetArtefactsDir
{
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $artefactsDir = Join-Path $repoRoot "artefacts"
    return $artefactsDir
}

function CreateAndGetArtefactsDir
{
    $artefactsDir = GetArtefactsDir
    New-Item -ItemType Directory -Force -Path "$artefactsDir"|Out-Null
    return $artefactsDir
}

function GetSamplesDir
{
    return Join-Path (Split-Path $PSScriptRoot -Parent) "sample-aasx"
}

Export-ModuleMember -Function `
    GetToolsDir, `
    AssertDotnet, `
    AssertDotnetToolVersion, `
    AssertDotnetFormatVersion, `
    AssertDeadCsharpVersion, `
    AssertDoctestCsharpVersion, `
    AssertOpinionatedCsharpTodosVersion, `
    GetArtefactsDir, `
    CreateAndGetArtefactsDir, `
    GetSamplesDir
