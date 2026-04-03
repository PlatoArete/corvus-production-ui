# Corvus Production UI Redesign Tracker

## Purpose

This file tracks implementation status for the Corvus UI redesign.

Use it together with:

- `meta/ui-redesign-brief.md`

The brief defines the design direction.
This tracker records what has been decided, what is in progress, and what remains.

## Current Status

Design direction:

- established

Implementation:

- not started

## Current Decisions

Confirmed:

- the target aesthetic is industrial command software, not vanilla RimWorld mimicry
- the UI should be quieter, cleaner, and more deliberate
- accent usage should be sparse, with cyan as the primary active-state color
- the redesign should improve usability, not just appearance
- implementation should be incremental, not a rewrite
- UI animation and hover state should live in the window layer, not data classes like `RecipeInfo`
- the current `ProductionWindow` structure is a valid base and should be evolved rather than replaced

Not chosen yet:

- exact final palette values
- exact compact button dimensions
- whether the first implementation pass uses only procedural drawing or also a subtle texture overlay
- whether to introduce lightweight icons beyond existing RimWorld affordances

## Phases

### Phase 1: Style Foundation

Status:

- pending

Tasks:

- create `Source/CorvusProductionUI/CorvusStyle.cs`
- define base colors
- define panel fill and border helpers
- define separator helpers
- define compact button drawing helpers
- define row hover drawing helpers
- add custom window background rendering to `ProductionWindow`

Exit criteria:

- the window has a coherent Corvus visual base
- no major layout changes yet

### Phase 2: Control Language

Status:

- pending

Tasks:

- replace bulky `Add Bill` row action with a compact action button
- standardize row action button sizes
- standardize hover behavior across controls
- add tooltips where icon-only controls are introduced
- reduce visual weight of secondary actions

Exit criteria:

- row actions feel cleaner and more consistent
- no loss of discoverability

### Phase 3: Layout Refinement

Status:

- pending

Tasks:

- refine filter bar grouping and spacing
- improve left/right pane hierarchy
- improve recipe row information hierarchy
- improve bill row anatomy
- reduce boxy/form-like presentation

Exit criteria:

- scan speed is improved
- the screen feels more like a command dashboard than a form

### Phase 4: Polish

Status:

- pending

Tasks:

- tune motion and hover response
- add subtle surface finish if needed
- refine badges/status chips
- evaluate optional logo/header treatment

Exit criteria:

- the interface feels distinct and polished without becoming noisy

## Immediate Next Actions

Recommended next step:

- implement Phase 1 by creating `CorvusStyle.cs`

After that:

- apply the style foundation to `ProductionWindow`
- then move to compact row actions

## Notes For Future Sessions

If resuming later:

1. read `meta/ui-redesign-brief.md`
2. read this tracker
3. inspect `Source/CorvusProductionUI/CorvusProductionUI.cs`
4. continue from the next unchecked phase

## Change Log

### 2026-04-03

- created redesign brief
- created implementation tracker
- confirmed redesign direction is desirable
- agreed to prioritize a quiet industrial command aesthetic over vanilla mimicry
