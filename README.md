# EmirScreen Android
Android-only companion for the official spacedesk Viewer.

Features:
- Opens installed spacedesk Viewer (package ph.spacedesk.beta).
- Displays Android network transport and reported bandwidth estimates.
- Displays Android thermal status and battery temperature.
- Displays CPU time consumed by EmirScreen itself.

Limitations:
- Does not modify spacedesk internals, its decoder, or FPS.
- Cannot guarantee higher FPS or turn a 60 Hz panel into 180 Hz.
- Android bandwidth figures are estimates, not a throughput benchmark.
- No root, accessibility, VPN, location, or device-admin permissions.

Build: Android Studio, Gradle sync, then Build > Build APK(s).
Minimum Android: 6.0 (API 23). Target SDK: 35.
