# Editor smoke results — not mobile performance

Unity 6000.6.7f1 / Apple M6 / Mono. All measured rebuilds allocated zero managed bytes. One run only; no mobile, battery or thermal inference.

| Grid | Layout | Fields | Median rebuild (ms) | p95 (ms) | Max (ms) |
| --- | --- | --- | --- | --- | --- |
| 32×32 | open | 1 | 0.027 | 0.030 | 0.032 |
| 32×32 | open | 5 | 0.137 | 0.138 | 0.146 |
| 32×32 | maze | 1 | 0.024 | 0.024 | 0.027 |
| 32×32 | maze | 5 | 0.122 | 0.123 | 0.125 |
| 64×64 | open | 1 | 0.110 | 0.113 | 0.161 |
| 64×64 | open | 5 | 0.526 | 0.534 | 0.549 |
| 64×64 | maze | 1 | 0.098 | 0.099 | 0.101 |
| 64×64 | maze | 5 | 0.463 | 0.469 | 0.697 |
| 128×128 | open | 1 | 0.423 | 0.428 | 0.482 |
| 128×128 | open | 5 | 2.087 | 2.537 | 3.157 |
| 128×128 | maze | 1 | 0.368 | 0.372 | 0.377 |
| 128×128 | maze | 5 | 1.823 | 1.846 | 1.967 |
| 256×256 | open | 1 | 1.668 | 1.689 | 1.714 |
| 256×256 | open | 5 | 8.525 | 8.590 | 8.616 |
| 256×256 | maze | 1 | 1.442 | 1.475 | 1.581 |
| 256×256 | maze | 5 | 7.379 | 7.435 | 7.512 |

Raw timing, allocation and frame samples: [editor JSON](pathfinding-benchmark-editor.json).

Validation: 16 cases × 60 measured samples after 10 warmups each; statistics and report metadata checked. The Unity editor also logged an unrelated SearchDatabase startup indexing exception. The batch-mode Game-view capture did not produce a screenshot; the JSON is the measurement evidence. Physical-device testing is deferred per user instruction.
