# Mobile pathfinding benchmark

## Current status

The benchmark is prepared for later on-device testing, as requested in the ticket chat. No phone was attached during preparation (`adb devices -l` was empty); full Xcode/devicectl was unavailable. The recorded editor run validates the harness and report export only. It does not establish mobile frame budgets, battery impact, thermal stability, or a safe maximum grid size.

Open `Assets/Scenes/PathfindingBenchmark.unity` and press Play, or install the standalone Android benchmark APK. This scene is separate from the primitive sandbox and is not added to the gameplay build list. It runs automatically; after completion, use **Run again** or **Copy report JSON**.

## Workloads and metrics

The matrix has 16 cases: 32×32, 64×64, 128×128, 256×256; open and deterministic alternating-gap maze layouts; one or five shared fields. Every case has five spawns and five exits. One field searches toward the nearest of all exits; five fields rebuild one field per exit. All routes are reachable. There are 10 warmup rebuilds and 60 measured rebuild sets per case, with one set per rendered frame.

Each sample records:

- Time for the entire one/five-field rebuild set using `Stopwatch.GetTimestamp`.
- Managed bytes allocated by that rebuild on its executing thread, using `GC.GetAllocatedBytesForCurrentThread`.
- Time for 10,000 pairs of distance and next-node reads from the five spawn tiles. This is a narrow field-query microbenchmark, not a full enemy simulation.
- The following Unity unscaled frame interval, including the rebuild set, query workload, benchmark GUI/rendering and frame pacing. It is not an isolated BFS duration or GPU measurement.

The JSON contains raw samples, median, nearest-rank p95 and maximum rebuild/frame timings, median query time, rebuild allocation total, and raw field-array byte counts. The fields' four integer arrays use approximately `16 * tileCount * fieldCount` bytes, excluding headers, walkability, GUI and report storage. Setup allocations, input snapshot copying and scene construction are outside the measured rebuild span. The whole benchmark app allocates report/UI/setup data even when rebuilds allocate zero bytes.

Metadata includes device model, OS, CPU, memory, GPU, Unity version, Mono/IL2CPP, editor/mobile flags, development-build flag, target frame rate and starting battery level. Confirm separately whether the target is a physical phone or emulator. Mobile-player status alone does not prove physical hardware.

## Run on Android later

The standalone package is `com.selimabidin.pathfindingbenchmark`, separate from the game. The APK uses the project's ARM64 IL2CPP configuration and non-development build options, locally debug-signed for installation. Build it with:

```sh
Unity -batchmode -quit -buildTarget Android -projectPath "$PWD" -executeMethod PathfindingBenchmarkSetup.BuildAndroid -logFile /tmp/pathfinding-android-build.log
```

Output: `Builds/Android/PathfindingBenchmark.apk` (ignored by Git). The builder supplies the benchmark scene explicitly and restores the game application ID afterward.

Connect a phone with USB debugging authorized, then:

```sh
adb devices -l
adb install -r Builds/Android/PathfindingBenchmark.apk
adb shell monkey -p com.selimabidin.pathfindingbenchmark -c android.intent.category.LAUNCHER 1
adb shell ls /sdcard/Android/data/com.selimabidin.pathfindingbenchmark/files/
adb pull /sdcard/Android/data/com.selimabidin.pathfindingbenchmark/files/ ./device-results/
```

Results are saved as `pathfinding-benchmark-YYYYMMDD-HHMMSS.json` under `Application.persistentDataPath`; the exact path appears in the app. **Copy report JSON** provides another export route if the device restricts file access. USB install/run commands have not been executed here because no phone was attached.

For comparable runs, keep the app foregrounded, record power/battery mode, charging state and ambient/device temperature, let the phone cool, and repeat. Repeat under sustained load to investigate throttling; a single matrix pass does not establish thermal behavior. Compare p95/max rebuilds with the pathfinding share of the intended tick/frame budget, leaving time for the rest of the game. Do not infer phone performance from the editor numbers or a desktop emulator.

For iOS, use this same benchmark scene in an iOS build profile and export through Unity; compiling/signing/installing requires full Xcode and a provisioned physical device. No iOS player was compiled or measured here.

## Reproduce the editor smoke check

```sh
Unity -batchmode -projectPath "$PWD" -executeMethod PathfindingBenchmarkEditorValidation.Begin -logFile /tmp/pathfinding-benchmark-editor.log
```

Omit `-quit`; the checker enters Play mode, waits for all cases, validates report counts/statistics and zero measured rebuild allocations, exports `Documentation/pathfinding-benchmark-editor.json`, then exits Unity. It attempts an editor Game-view capture; capture availability depends on the graphics environment. The report is explicitly labeled **EDITOR SMOKE ONLY - NOT MOBILE RESULTS**.

See the [editor smoke summary](pathfinding-benchmark-editor.md) and [raw report](pathfinding-benchmark-editor.json). The batch Game-view capture was unavailable; no benchmark UI screenshot is claimed.
