# Building TruckNav

## Local Toolchain

Validated on August 24, 2026 with:

- .NET SDK `8.0.424`
- .NET SDK `9.0.317`
- Flutter `3.44.4`
- Dart `3.12.2`
- Java 17 runtime from Tableau Public 2026.1 (used for the validated Android release build)

## Windows Connector

From the repository root:

```powershell
pwsh ./scripts/build_windows.ps1
```

The script performs:

- `dotnet restore`
- `dotnet build -c Release`
- `dotnet test -c Release`
- `dotnet publish -c Release`

## TruckNav Mobile

From the repository root:

```powershell
pwsh ./scripts/build_android.ps1
```

The script performs:

- `flutter pub get`
- `flutter analyze`
- `flutter test`
- `flutter build apk --release`

Android release packaging requires Java 17 or later for the Gradle build.

The validated release APK is written to `apps/mobile/build/app/outputs/flutter-apk/app-release.apk`. On this machine, `scripts/build_android.ps1` automatically used `C:\Program Files\Tableau\Tableau Public 2026.1\bin\jre` because the default `JAVA_HOME` still pointed to Java 8.
