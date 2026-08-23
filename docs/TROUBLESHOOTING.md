# Troubleshooting

## Missing Design Files

If the approved design reference HTML files are not present in `design-reference/`, do not treat the current app shell as final UI.

Required files:

- `design-reference/trucknav-mobile.html`
- `design-reference/trucknav-connector.html`

## Windows Build Issues

- Ensure .NET 8 SDK is installed.
- Use PowerShell from the repository root.
- If WPF fails to restore after package changes, rerun `dotnet restore apps/windows/TruckNav.Connector.sln`.

## Flutter Build Issues

- Confirm `flutter --version` works.
- Run `flutter doctor` if Android tooling is incomplete.
- Android release packaging requires Java 17 or later. If `flutter build apk --release` reports that Gradle is running on JVM 8, install or point Flutter/Gradle at a standalone JDK 17+ runtime or the Android Studio JDK.
- `scripts/build_android.ps1` can fall back to other Java 17+ runtimes found on the machine, including the Tableau Public JRE that happened to exist on this workstation on August 23, 2026. That fallback is an environment convenience, not a TruckNav dependency.
- If `flutter build apk --release` fails because of local Android SDK setup, run `flutter analyze` and `flutter test` first to verify application code independently.

## Local Network / Firewall Checks

- The Windows Connector binds on the LAN with `ListenAnyIP(0)` and logs the selected listener port plus reachable LAN interfaces at startup.
- The application does not auto-create broad Windows Defender Firewall rules and does not require administrator elevation for normal startup.
- If Android never sees ROCK but Windows logs show the Connector started and mDNS advertisement began, verify the app is allowed on the current Private network profile.
- If discovery works but connect stalls, check that the Android app still has local network access and that the Windows listener port is not blocked on the current machine profile.
