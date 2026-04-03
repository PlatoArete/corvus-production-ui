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

- Phase 1 complete

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

- complete

Tasks:

- create `Source/CorvusProductionUI/CorvusStyle.cs` [done]
- define base colors [done]
- define panel fill and border helpers [done]
- define separator helpers [done]
- define compact button drawing helpers [done]
- define row hover drawing helpers [done]
- add custom window background rendering to `ProductionWindow` [done]
- review and tune Phase 1 visuals in-game [done]

Exit criteria:

- the window has a coherent Corvus visual base
- no major layout changes yet

### Phase 2: Control Language

Status:

- in progress

Tasks:

- replace bulky `Add Bill` row action with a compact action button [done]
- standardize row action button sizes [done]
- standardize hover behavior across controls [done]
- add tooltips where icon-only controls are introduced [done]
- reduce visual weight of secondary actions [done]
- review and tune compact controls in-game [pending]

Exit criteria:

- row actions feel cleaner and more consistent
- no loss of discoverability

### Phase 3: Layout Refinement

Status:

- in progress

Tasks:

- refine filter bar grouping and spacing [done]
- improve left/right pane hierarchy [done]
- improve recipe row information hierarchy [done]
- improve bill row anatomy [done]
- reduce boxy/form-like presentation [done]
- review and tune Phase 3 layout in-game [pending]

Exit criteria:

- scan speed is improved
- the screen feels more like a command dashboard than a form

### Phase 4: Polish

Status:

- in progress

Tasks:

- tune motion and hover response [done]
- add subtle surface finish if needed [done]
- refine badges/status chips [done]
- evaluate optional logo/header treatment [done]
- review and tune Phase 4 polish in-game [pending]

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
- implemented initial `CorvusStyle` helper
- applied initial Corvus visual layer to `ProductionWindow`
- confirmed successful Debug build
- confirmed in-game that Phase 1 loads correctly and the visual change is immediate
- implemented compact control language for recipe and bill rows
- confirmed successful Debug build after Phase 2 changes
- implemented layout refinement pass for filters, pane balance, and row hierarchy
- confirmed successful Debug build after Phase 3 changes
- implemented polish pass with surface finish, header branding, and clearer state badges
- confirmed successful Debug build after Phase 4 changes
