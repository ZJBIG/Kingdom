# Research queue automatic payment

## Contract

The head of `ResearchManager`'s research queue must automatically attempt to
pay its remaining resource cost whenever the queue is evaluated. The player
does not need to press a separate payment button for a queued research.

The queue-head evaluation is the single owner of this attempt:

```text
HandleResearchAction / EnqueueResearch / SimulationManager.Tick
    -> TryStartNextQueuedResearch
    -> TryPayResearchCost(ResearchState)
    -> ResourceManager.TryApplyAtomicPayment
    -> TryStartResearchNow
```

`ResearchManager.Tick` must call `TryStartNextQueuedResearch` when there is no
active research. It must not maintain a second payment path for the same queue
head.

## Payment rules

- Payment uses the real `Research.ResourceRequirements` and
  `ResourceManager` inventory.
- Payment is atomic: if the complete remaining cost is unavailable, no resource
  is deducted and `ResearchState.CostPaid` remains false.
- An unpaid queue head remains `WaitingResources` and blocks later queue items.
- Once the complete cost becomes available, the next queue evaluation pays it
  and starts the head automatically.
- A successful payment must not reorder the queue or pay a later item.
- A missing `ResourceManager` is a failed attempt, not an exception or a fake
  payment.

Legacy-save policy: if a save contains a valid `PaidResourceCosts` ledger, the
ledger is restored as historical credit and is not recalculated from current
costs. An older save with no ledger but an explicit `CostPaid=true` is restored
as historically fully paid; a save with no ledger and `CostPaid=false` cannot
recover an exact partial amount, so the loader does not invent one. The loader
never infers or refunds a missing amount, especially not by temporarily
exceeding Food capacity. Invalid or unknown ledger entries reject the
candidate save and allow the normal backup fallback to run.

## UI boundary

The research tree and detail panel issue research actions and display state.
They must not implement a second payment algorithm or subtract resources
directly. `PayResearchCost` remains an explicit diagnostic/detail API, while
queued research uses the automatic queue-head path above.

## Regression requirements

Tests must cover:

1. a ready queue head is paid and started by a research action;
2. an underfunded queue head remains waiting without inventory mutation;
3. after resources become sufficient, a later queue evaluation pays the head;
4. later queued research is not paid before the head.
