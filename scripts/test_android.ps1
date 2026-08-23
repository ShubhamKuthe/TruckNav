$ErrorActionPreference = "Stop"

Push-Location (Join-Path $PSScriptRoot "..\\apps\\mobile")
try {
    flutter pub get
    flutter analyze
    flutter test
}
finally {
    Pop-Location
}

