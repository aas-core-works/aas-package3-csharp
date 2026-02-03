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

function FindDotnetToolVersion($PackageID) {
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
    GetArtefactsDir, `
    CreateAndGetArtefactsDir, `
    GetSamplesDir
