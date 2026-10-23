# Mobile starter validation — 2026-10-24

Unity 6000.6.7f1 on macOS Apple Silicon. Android/iOS support, OpenJDK, Android SDK/NDK and CMake were installed through Unity Hub's `install-modules --version 6000.6.7f1 --module android ios --childModules`.

- Unity mobile configuration/import/compilation: exit 0.
- Android ARM64 IL2CPP APK build: exit 0. `Builds/Android/SuperTowerDefense.apk`, 14,305,912 bytes. APK signature verification passed (v2); Android `aapt dump badging` confirmed `com.selimabidin.supertowerdefense`, minimum API26, target API36, ARM64, and OpenGL ES 3 family. Debug signing is for local installation only.
- APK SHA256: `bde307bb5f1dcfe5587cb717f0f053ce160e24125469ac9f4cb3e3e4b4e95baa`.
- iOS Xcode project export: exit 0. `Builds/iOS/Unity-iPhone.xcodeproj` contains the package ID, iOS15 deployment target and iPhone/iPad device family. Full Xcode is absent; no iOS compile, signing or installation was performed.
- Editor camera renders at 1600×900 (16:9), 1950×900 (19.5:9), and 1200×900 (4:3) passed bounds checks. Phone/tablet evidence was visually inspected. This is editor framing evidence, not mobile GPU/runtime validation.
- `adb devices` found no connected devices. No physical-device or emulator execution, touch/safe-area checks, performance, battery, thermal or store-readiness claims are made. There is no UI/gameplay yet.

Build products are ignored by Git and remain in the validation worktree. Reproduce using the README commands. Local logs: `/tmp/std1-mobile-config-final.log`, `/tmp/std1-mobile-aspects.log`, `/tmp/std1-mobile-android.log`, `/tmp/std1-mobile-ios.log`.
