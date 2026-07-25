# Kingdom3 acceptance checklist

## Compile and static

- Unity 2022.3.62f2c1 opens the project.
- no C# compile errors;
- no missing script references;
- no `BigNumber`;
- no `Pair<Resource,string>`;
- no `Transform.GetChild(index)`;
- Managers contain no UI references.

## Simulation

- partial inventory produces partial efficiency in the same tick;
- actual consumption never exceeds availability;
- resource amount never ends below zero;
- insertion order does not affect results;
- food integrates every tick;
- calendar advances independently;
- total-time result is frame-rate independent;
- auto-build uses `TryBuild`.

## Transactions

- build 1, 10 and maximum charge exact costs;
- insufficient conditions cause no partial mutation;
- deconstruct clamps before returns;
- research cost pays once.

## Save/load

- one versioned root;
- temp + backup path;
- main semantic failure can fall back to backup;
- repeated Load is idempotent;
- stable IDs only;
- derived rates rebuild;
- ExpantaNum round-trips.

## UI lifecycle

- no Viewer Update refresh loops;
- no HUD/MusicViewer refresh coroutines;
- one bounded UI refresh manager;
- disabled main viewers do not refresh;
- disabled viewers do not stop simulation;
- settings disabled does not stop music;
- re-enabled viewer displays latest State;
- no duplicate cards/subscriptions after repeated enable/disable.

## Layout and navigation

- no manual dynamic card height formulas;
- no hidden card reparenting;
- no off-screen page hiding;
- main navigation uses explicit MainTab and SetActive;
- ResearchLineView caches components;
- Scene/Prefab references are valid.

## Visual design

- shared theme tokens and component states;
- HUD/navigation/resource/building/research/settings match the same system;
- common buttons and dialogs are reused;
- failure reasons are explicit;
- important state is not color-only;
- 1920x1080, 2560x1440 and 1366x768 pass.

## Performance

- one gameplay simulation tick;
- one ordinary UI refresh scheduler;
- hidden pages produce no continuous TMP/Layout work;
- Tick has no repeated Parse/LINQ/temp collections;
- one-hour run has no sustained memory growth.

## Evidence

Record Unity executable, command, exit code, log, XML, Console, screenshots and Profiler captures. If Unity was not run, state `未执行真实 Unity 编译。`
