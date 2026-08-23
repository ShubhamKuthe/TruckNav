$ErrorActionPreference = "Stop"

function Get-JavaExecutableInfo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$JavaExe
    )

    if (-not (Test-Path $JavaExe)) {
        return $null
    }

    try {
        $versionOutput = & $JavaExe -version 2>&1
    }
    catch {
        return $null
    }

    $versionLine = $versionOutput | Select-Object -First 1
    $versionMatch = [regex]::Match($versionLine, '"(?<major>\d+)(\.(?<minor>\d+))?')
    if (-not $versionMatch.Success) {
        return $null
    }

    $majorVersion = [int]$versionMatch.Groups["major"].Value
    if ($majorVersion -eq 1 -and $versionMatch.Groups["minor"].Success) {
        $majorVersion = [int]$versionMatch.Groups["minor"].Value
    }

    [pscustomobject]@{
        JavaExe     = $JavaExe
        JavaHome    = Split-Path (Split-Path $JavaExe -Parent) -Parent
        Major       = $majorVersion
        VersionLine = $versionLine
    }
}

function Get-JavaCandidates {
    $candidates = [System.Collections.Generic.List[string]]::new()

    if ($env:JAVA_HOME) {
        $candidates.Add((Join-Path $env:JAVA_HOME "bin\java.exe"))
    }

    $javaCommand = Get-Command java -ErrorAction SilentlyContinue
    if ($javaCommand) {
        $candidates.Add($javaCommand.Source)
    }

    $programFiles = ${env:ProgramFiles}
    $programFilesX86 = ${env:ProgramFiles(x86)}
    $localAppData = $env:LOCALAPPDATA

    $candidatePatterns = @(
        (Join-Path $programFiles "Java\*\bin\java.exe"),
        (Join-Path $programFiles "Microsoft\jdk*\bin\java.exe"),
        (Join-Path $programFiles "Android\Android Studio\jbr\bin\java.exe"),
        (Join-Path $programFiles "Tableau\Tableau Public *\bin\jre\bin\java.exe"),
        (Join-Path $localAppData "Programs\Android Studio\jbr\bin\java.exe"),
        (Join-Path $programFilesX86 "Common Files\Oracle\Java\java*path*\java.exe")
    ) | Where-Object { $_ }

    foreach ($pattern in $candidatePatterns) {
        foreach ($match in Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue) {
            $candidates.Add($match.FullName)
        }
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $candidates) {
        if ($seen.Add($candidate)) {
            $candidate
        }
    }
}

function Resolve-Java17OrNewer {
    $bestCandidate = $null

    foreach ($candidate in Get-JavaCandidates) {
        $javaInfo = Get-JavaExecutableInfo -JavaExe $candidate
        if ($null -eq $javaInfo -or $javaInfo.Major -lt 17) {
            continue
        }

        if ($null -eq $bestCandidate -or $javaInfo.Major -gt $bestCandidate.Major) {
            $bestCandidate = $javaInfo
        }
    }

    $bestCandidate
}

$resolvedJava = Resolve-Java17OrNewer
if ($null -eq $resolvedJava) {
    $fallbackInfo = $null
    $javaCommand = Get-Command java -ErrorAction SilentlyContinue
    if ($javaCommand) {
        $fallbackInfo = Get-JavaExecutableInfo -JavaExe $javaCommand.Source
    }

    if ($fallbackInfo) {
        throw "TruckNav Android release builds require Java 17 or later. Current Java version: $($fallbackInfo.VersionLine)"
    }

    throw "TruckNav Android release builds require Java 17 or later, but no suitable java command was found."
}

$env:JAVA_HOME = $resolvedJava.JavaHome
$env:PATH = "$($resolvedJava.JavaHome)\bin;$env:PATH"

Write-Host "Using Java $($resolvedJava.Major) from $($resolvedJava.JavaHome)"
Write-Host "Detected version: $($resolvedJava.VersionLine)"

Push-Location (Join-Path $PSScriptRoot "..\\apps\\mobile")
try {
    flutter pub get
    flutter analyze
    flutter test
    flutter build apk --release
}
finally {
    Pop-Location
}
