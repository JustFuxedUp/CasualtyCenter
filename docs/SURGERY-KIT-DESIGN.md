# Surgery kit concept — design note, not implemented

## User proposal

A craftable surgery kit has a fixed unit weight and effectively infinite
encumbrance reduction for its contents: the kit's own weight remains, while
stored supplies do not add carrying weight under this proposed model.

Players can place vanilla or modded items and liquids inside. These contents can
unlock procedures, satisfy their requirements, or contribute to treatment effects.
The kit could become the common supply source as the procedure list expands.

Two interaction options remain open:

- Drag a tool or the kit onto a limb to open the applicable surgery menu.
- Open the limb's surgery menu normally and check tools/supplies available to the player or kit.

The health-dummy procedure menu should support either approach without requiring
another standalone limb selector. No access or inventory rule is finalized here.

## Later design decisions

- Recipe and fixed weight; whether zero contents encumbrance needs capacity restrictions.
- Which inventory locations count as accessible and whether tools must be equipped.
- How item capabilities identify compatible vanilla/modded tools and substitutes.
- Liquid storage, quantity, mixtures, cleanliness and consumption rules.
- At what stage supplies are reserved or consumed, and what cancellation returns.
- Multiplayer authority and inventory synchronization before shared-patient surgery.

This note does not add a container, recipe, weight modifier, dependency, inventory
scan or supply cost. The free test kit remains in place while native minigame
integration is the priority.
