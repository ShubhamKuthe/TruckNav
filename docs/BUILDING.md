# Building TruckNav

## Local Toolchain

Validated on August 23, 2026 with:

- .NET SDK `8.0.424`
- .NET SDK `9.0.317`
- Flutter `3.44.4`
- Dart `3.12.2`
- Java `1.8.0_491`

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

On the current validation machine on August 23, 2026, only Java 8 was installed, so `flutter build apk --release` could not complete even though `flutter analyze` and `flutter test` passed.
