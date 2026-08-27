# Kingdom engineering rules

## Must

- Treat the current repository and Unity assets as authoritative.
- Preserve State/Manager/Simulation/UI boundaries.
- Preserve `.meta` files and asset GUIDs.
- Fix simulation correctness before visual redesign.
- Validate transactions fully before mutation.
- Use explicit `deltaSeconds`.
- Keep resource and food amounts nonnegative.
- Use stable IDs in saves.
- Use parseable ExpantaNum `ToString()` for persistence.
- Run available compile/tests after each patch.
- Add PlayMode tests before switching viewers to `SetActive(false)`.

## Must not

- redo BigNumber or Pair migration;
- create BigNumber compatibility aliases;
- add gameplay/UI APIs to ExpantaNum;
- make UI activation or transform position affect gameplay;
- parse localized text to determine state;
- add per-card Update/coroutines;
- hand-calculate dynamic card heights after layout migration;
- store derived rates/efficiency/UI caches in save data;
- install packages or upgrade Unity without permission;
- claim visual, Inspector, PlayMode or performance validation without evidence.

## Default product decisions

Until explicitly changed:

- Food remains a special GameState value.
- UI visibility does not pause gameplay.
- Music continues while the setting UI is closed.
- Offline progress is not expanded during UI redesign.
- Paid/current research is not given a newly invented refund rule.
- Population leaves only when food is unavailable while Food/s remains negative;
  reducing housing capacity does not retroactively remove existing population.
  The unused capacity-departure helper is retained for a future product decision,
  but is not part of the current runtime contract.
- No automatic push or PR.
