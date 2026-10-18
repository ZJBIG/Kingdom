# Round7 — production budget correction pipeline

## Scope
This round follows the updated goal: prioritize coherent runtime systems over isolated cleanup. BuildingManager's correction lower bound, candidate checks and commit were refactored together. No production definitions, save schema, asset/GUID, cost, binary search step count, tolerance or resource capacity rule changed.

## Change
- Build a reusable affected-resource input list once per correction, after selected contributions and reward are known, preserving original resource order. Lower-bound calculation and all candidate checks use this list. Selected production/consumption and external production are fixed for that search, so they are no longer re-read/recomputed for every candidate.
- Every correction rebuilds its inputs. Candidate rates, food availability, happiness, power/logistics feasibility and Amount*(Efficiency*scale) quantization remain dynamic and preserve original arithmetic grouping.
- Extract shared game-side rate deltas. Normal updates still apply resource then game effects; correction commit skips resource deltas that were immediately overwritten by ResetBuildingRates and complete active-building re-sum. Food/capacity/population/power/logistics/military effects and final positive re-sum remain.
- ResourceState.Version has no consumer requiring a particular increment count. Final changed rates still invalidate signatures. Intermediate redundant version increments decrease.
- Production source 2400 -> 2432 lines; added 2 test methods / 5 cases (four benchmark variants plus two-separate-cycle behavior case). This intentionally adds a bounded input structure to remove repeated runtime work.

## Correctness and verification
- before.xml and verified.xml are initial 14-case baseline / 271-case broader checks, both passing. Their original benchmark placed idle states after affected inputs, often skipped by the old early return; timing differences were too small for a performance claim.
- Benchmark was extended to retain that scenario and add idle states before the affected inputs. ordered-before.xml: 15/15 budget tests on original production bytes. ordered-verified.xml: 272/272 related EditMode cases, 2.5513403 seconds, real Unity compilation, normal exit/runner0.
- New correctness case covers two disconnected lossy cycles, 256 idle states, inventories zero -> sufficient -> zero and repeated corrections/ticks. Existing cases cover tiny inventory, food/happiness feedback, resource/flow cycles, positive authored Ultra work, research power, saves and online/offline simulation.
- Developer build compile.log: exit0 / 0 errors / 6 existing warnings. Final expanded benchmark compiled in real Unity. Targeted diff check passed; source-inputs.json and round7-only.patch identify final inputs.
- Two independent read-only reviews checked arithmetic grouping, per-correction lifetime, Version consumers and retained positive re-sum.

## Measured timing
All times ms per PrepareTickResourceSatisfaction call. Same fixture/input/order before and after, 4 warmups, median of 5 blocks of 16 calls; fixture creation excluded. Wall-clock Stopwatch sampling, not whole-game profiling. Scenario = idle count/correction/idle first.

| Scenario | Before | After | Reduction |
|---|---:|---:|---:|
| 0/True/False | 0.37055 | 0.327556 | 11.6% |
| 256/True/False | 1.040425 | 0.911069 | 12.4% |
| 256/True/True | 1.221031 | 0.952788 | 22.0% |
| 256/False/False | 0.315756 | 0.318631 | -0.9% |

The no-correction control is essentially unchanged; a negative reduction is a small increase, not a speedup. Correction cases improved about 12-22% in these synthetic sparse-cycle fixtures. Do not extrapolate to FPS or all content. benchmark-comparison.json retains min/max and exact medians. No GC allocation reduction claim.

## Execution notes and limits
The preparation patch was briefly applied before a hold message arrived, then restored byte-for-byte before any baseline run. Original SHA D126B9EE875ACF3CE1010CACA80F858230BFDD3CCDE581B66776A126EEAE62E9 was verified. The optimization was again temporarily replaced with those original bytes for ordered-before; final optimized SHA C0BA2CB947A2BC939E30C2F8F14E90F5417A847671844B7C0225B609A737E977 is restored and tested. ordered-before assertions passed but native exit hung to wrapper timeout/runner1; final ordered-verified exited normally/runner0.

No PlayMode/full-suite run, real player-save operation, commit/push or global performance/correctness claim. Continuous goal stays active. Continue with coherent production/progression work, reproducing failures or establishing a baseline before further changes; do not expand caches beyond their proven lifetime.
