# STD-4: Path finding investigation

**Recommendation:** use shared reverse breadth-first search (BFS) distance fields on a four-neighbor grid. Start with reusable flat arrays on the main thread, rebuilding only when navigation changes. Profile on Android/iOS before adding background execution. Up to five spawns and five exits do not require 25 searches or a search per enemy.

## Current project and scope

Inspected `origin/master` at `0acdb10` on 2026-10-24: Unity 6000.6.7f1, mobile starter scene, and camera controls. `Assets/Scripts` contains only camera scripts; there is no gameplay grid, enemy/spawn system, or pathfinding implementation. The scene's empty serialized NavMesh settings and built-in AI module are not an implemented navigation system. Burst and Collections are not explicit dependencies in `Packages/manifest.json`.

This is an architecture recommendation, not implemented or benchmarked functionality. No packages, gameplay code, or test harness are added.

## Decide what an exit means

The ticket does not say whether enemies choose exits or receive assignments. Support both policies explicitly; use **nearest allowed exit** as the initial suggested default, subject to game design confirmation. Nearest means shortest traversable route, not straight-line distance. A nearest-exit rule does not distribute enemies evenly or guarantee that all exits are used.

| Gameplay rule | Shared navigation data | Examples |
| --- | --- | --- |
| Any enemy may reach any enabled exit | One BFS seeded with all exits at distance zero | One spawn/three exits; two spawns/one exit; five/five |
| Enemy has one assigned exit | One reverse BFS per enabled exit, at most five | Different waves or spawns target different bases |
| Enemy may choose a subset of exits | Reuse per-exit fields; select the reachable allowed exit with minimum distance at the enemy's tile | At most five distance lookups on selection; break ties by exit ID |

For assigned exits, preserve the assignment until an explicit gameplay rule changes it. For subsets, select at spawn or a tile boundary after a navigation update; retain the selected exit while it remains the best choice. This avoids unstable target selection between tiles. The all-exits field cannot answer reachability for a particular assigned exit: reaching exit A says nothing about exit B.

## How the field works

Represent each walkable tile as a node with unit-cost north/east/south/west edges. Initialize all distances to unreachable (`-1`), seed the chosen exit(s) with zero, and expand a FIFO queue outward. On first discovery of a tile, store distance plus one and the direction back toward its discoverer. At exits store a distinct arrived direction; unreachable tiles have no next step. Enemies share this field and follow one orthogonal tile edge at a time. Do not smooth corners into diagonal shortcuts.

BFS gives shortest paths by edge count in an unweighted graph and costs `O(V + E)` ([Boost BFS documentation](https://www.boost.org/latest/libs/graph/doc/html/graph/algorithms/traversal/breadth_first_search.html)). The reverse/multiple-seed construction here is a proposed application: an undirected grid has the same legal edges in either direction; flooding from all permitted exits measures distance to the closest one. A valid next step reduces distance by exactly one, preventing cycles within a published field.

Make results reproducible: seed exits by stable ascending ID, inspect neighbors in fixed N/E/S/W order, and keep first discovery on equal distances. For per-exit selection, compare `(distance, exit ID)`. The field is a discrete next-tile direction, not continuous vector steering. Handle spacing, animation and enemy avoidance separately; moving enemies should not continually invalidate the static walkability grid.

Use `index = x + y * width`, an integer distance array, byte direction array, byte walkability array and reusable integer queue. Check row/column boundaries when computing neighbors; `index + 1` must not wrap to the next row. Avoid one object per tile, per-enemy path lists, and allocations during ordinary queries/rebuilds.

## Other algorithms and future grids

Shared fields amortize work when many enemies have a small set of destinations. A* remains useful for occasional unique point-to-point requests; running it for each enemy would repeat work that these fields share. Its advantage depends on the workload and heuristic, so no speedup ratio is claimed ([Boost shortest-path overview](https://www.boost.org/doc/libs/develop/libs/graph/doc/html/graph/algorithms/shortest_paths/shortest_paths_overview.html)). A NavMesh or a third-party package is unnecessary for the initial exact tile-edge movement requirement.

Keep topology separate from search: integer node IDs, legal neighbor/predecessor enumeration, traversal costs, and world-to-node/node-to-world conversion. A square-grid provider supplies four neighbors today; hex or other graphs can replace that provider later. Do not impose square-grid coordinates on the search result contract. Bake neighbor data into flat buffers for any future job implementation rather than passing managed interfaces into jobs.

If terrain has different traversal costs, replace BFS with reverse Dijkstra for nonnegative weights; ordinary BFS then ceases to minimize cost ([Boost Dijkstra documentation](https://www.boost.org/latest/libs/graph/doc/html/graph/algorithms/shortest_paths/dijkstra_shortest_paths.html)). Prefer strictly positive costs so a next step strictly decreases total distance. With one-way edges or tile-entry costs, expand incoming edges and use the original forward edge cost: `D(u) = min(cost(u,v) + D(v))`. Do not accidentally charge the reverse direction's cost. Movement profiles with different clearance or terrain rules require separate compatible fields/cache entries.

## Tower placement and changing mazes

Treat placement as a transaction, including multi-tile footprints:

1. Reject footprints outside the board, on protected spawn/exit tiles, or overlapping an enemy's occupied tile or reserved movement segment. Walkability must account for enemy clearance; a point-sized path is insufficient for larger units.
2. Apply the candidate to a separate navigation snapshot and build its required fields. An out-of-bounds, blocked, or unreachable required spawn/exit relationship makes the candidate invalid. With any-exit policy every enabled spawn needs at least one reachable allowed exit; with assigned targets validate every configured spawn-target pairing. If design requires every exit to remain accessible, validate that separately with per-exit fields.
3. Check **all active enemies**, including their committed next tile/segment, against the candidate and their own exit policy. Spawn reachability alone can miss an enemy trapped in another pocket. At final commit, repeat these checks using current enemy positions; enemies may have moved while a sliced or background calculation ran.
4. Publish the grid, fields, and tower together at a simulation/tile boundary only if validation still passes. Until then the placement is a preview and the old grid remains authoritative. Reject without charging currency on failure. Never make a blocking tower live while enemies still consume an old field.

At level load, report invalid endpoints and unreachable required routes before waves start. An optional unused exit may be unreachable only if the level policy permits it. If a scripted change bypasses placement validation, stop affected enemies and report no-route; never follow an invalid direction or teleport them. Prefer the same validation for all topology changes.

Version snapshots and cache fields by topology/walkability revision, costs revision, movement profile, and goal set/policy revision. Invalidate on placement/removal, doors, terrain cost changes, exit activation or topology changes. Changing an assignment can reuse existing per-exit data. Spawn changes require renewed reachability validation even when they do not require rebuilding fields. Batch edits; begin with full rebuilds, adding incremental algorithms only if profiling justifies them.

Keep published buffers immutable while readers use them. Background/sliced results carry their input revision; discard stale results rather than overwriting newer state. Serialize commits or revalidate against the current revision. Use separate build buffers and swap the complete field set atomically on the main thread; never mix distances/directions from different versions.

## Mobile cost and execution strategy

Let `N` be tile count and `K` the number of fields. Four-neighbor grids have at most `4N` directed neighbor checks per field, so full rebuilds are `O(KN)`, `K = 1` for all exits together or at most five per-exit fields. Ordinary next-direction lookup is `O(1)` per enemy per tile transition. Up to five spawns only add reachability checks; they do not multiply rebuild cost. Different movement profiles multiply storage/work separately.

Illustrative raw-array arithmetic, **not measured device results**: one field uses `4N` bytes for 32-bit distances plus `N` for byte directions. A shared byte walkability grid and integer queue add `5N`. Thus `(5K + 5)N` bytes is about 40 KiB for one field or 120 KiB for five on a 64×64 grid; about 640 KiB or 1.875 MiB on 256×256. This excludes array headers, enemies, clearance/cost data, and snapshots. Two field sets alone add another `5KN` bytes; parallel field jobs need separate queues. Five fields on 256×256 entail at most 1,310,720 neighbor checks, plus initialization. None of these counts establishes milliseconds, battery usage, or a safe maximum map size.

Start synchronously if rebuilds fit the measured frame budget; otherwise slice a reusable queue across frames with a configurable work/time limit, keeping placement pending. At 60 FPS the whole frame budget is approximately 16.7 ms, at 30 FPS 33.3 ms; pathfinding only receives a measured fraction. A coroutine by itself does not move CPU work off the main thread.

If representative device profiling shows unacceptable rebuild spikes or placement latency, move snapshot computation to one background worker or Unity Jobs/Burst. Keep GameObjects, Transforms and scene queries on the main thread: Unity says most Unity APIs are not thread-safe ([Unity thread and continuation guidance](https://docs.unity3d.com/6000.6/Documentation/Manual/async-awaitable-continuations.html)). A worker consumes plain immutable input and returns versioned data; handle shutdown/cancellation and buffer ownership explicitly. Do not create a thread per enemy.

For Jobs, an `IJob` per field is a reasonable first candidate; independent fields may run concurrently with independent output/queue buffers. Parallelizing individual BFS frontier updates adds synchronization and is not an initial requirement. Use unmanaged input/output for Burst ([supported types](https://docs.unity.com/en-us/engine/6000.6/manual/scripting/compilation-and-code-reload/script-compilation/burst/csharp-language-support/csharp-type-support)); NativeContainers provide tracked shared memory access ([Unity NativeContainer guidance](https://docs.unity3d.com/6000.6/Documentation/Manual/job-system-native-container.html)). Schedule on the main thread, and call `JobHandle.Complete` before accessing results; doing so immediately after scheduling can stall the main thread ([Unity job lifecycle](https://docs.unity3d.com/6000.6/Documentation/Manual/job-system-creating-jobs.html)). Scheduling has overhead, so compare total cost including copies, waits and publication ([Unity's scheduler overhead analysis](https://unity.com/blog/engine-platform/improving-job-system-performance-2022-2-part-2)). No package additions or threading decision are needed for this investigation.

## Follow-up implementation validation

Before choosing execution mode, implement the small synchronous core and profile representative/worst planned grids, enemy counts and placement bursts on target Android/iOS hardware. Record rebuild duration, placement latency, frame-time spikes, allocations, peak memory, and thermal behavior over sustained play; use those results to set a budget.

Future correctness coverage should include 1→3, 2→1 and 5→5 layouts; assigned/subset exits; tied routes; disconnected/blocked endpoints; boundaries; overlapping endpoint policy; maze closure trapping an active enemy while spawns remain connected; footprint/clearance; placement removal; and stale/out-of-order rebuild completion. Verify every step is orthogonal, strictly decreases the appropriate field distance, and reaches an allowed target. Weighted/directed graphs need separate cost/reverse-edge cases when introduced. These are proposed acceptance checks, not tests claimed to have run.
