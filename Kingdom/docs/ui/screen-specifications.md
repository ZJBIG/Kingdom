# Screen specifications

## HUD and navigation

HUD shows kingdom name, date, technology, food amount/net rate, productivity, territory and save feedback. Navigation uses explicit MainTab values and never parses title text.

## Resource screen

Default row:
`icon | name | amount | net rate | state | expand`

Expanded content:
description, production rate, consumption rate and later source/use summaries.

## Building screen

Each building must expose:
- name, amount and efficiency;
- auto-build toggle and progress;
- single cost, owned amount and total requested cost;
- space/build effort/productivity effects;
- generation/consumption;
- build quantity and commands;
- explicit failure reason;
- separate confirmed “deconstruct all”.

A list + details design is preferred when card density becomes too high.

## Research screen

- graph viewport with node and line layers;
- detail panel outside the graph;
- explicit node states;
- progress, requirements, queue position and unlocks;
- selected/prerequisite/next relationships with stable visual semantics.

## Settings screen

Settings can be disabled. MusicManager remains active. Music UI refreshes only while open. Include manual save and save feedback.

## Overlay

Shared Tooltip, Toast and ConfirmDialog live under an Overlay root and are not duplicated per page.
