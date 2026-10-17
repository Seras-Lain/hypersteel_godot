# Vision profiles

Drop-in sheet for how an entity sees and (for the player) how the camera projects.

Not a new Resolve fork. Not a new kit. A resource you stamp on the pawn or operator the same way you stamp `EntityStats` or a perk.

**Mode** (Normal / Expanded / Halo360) is the supertype. It decides AI cone, accuracy, reaction, and whether rear exists.
**Projection** is the concrete mapping the player camera uses under that mode.

## Modes (supertypes)

| Mode | Player | NPC / AI |
|---|---|---|
| **Normal** | Rectilinear. Standard cone. No extra cost. | Default `VisionConeDeg` (~110). Full accuracy inside the cone. Blind outside. |
| **Expanded** | Significant peripheral. Mild framerate cost (wider FOV + remap). Center stays clean for aiming. | Peripheral cone ~200°. Reduced accuracy outside the normal cone. |
| **Halo360** | Confusing and hard to get used to. Interesting. No real blind spots; periphery is stylized. | No blind spots. Glowing lenses around the head. Reaction slowed. Cannot be snuck up on. |

## Projections (under the modes)

### Normal
- **Rectilinear** — classic perspective. Straight lines stay straight.

### Expanded
- **ExpandedEdgePannini** — rectilinear in the center; linearly interpolates to Pannini on the vertical edge bands (default 0–10% and 90–100% of screen height). Vertical-only so the aiming plane stays honest.
- **Pannini** — full Pannini. Verticals and lines through screen center stay straight. Good at 120–150°.

### Halo360
- **WarpEdgeBubble** — protected center oval is rectilinear; outside the oval the mapping hyper-compresses into a fisheye bubble.
- **FisheyeEquidistant** / **FisheyeStereographic** / **FisheyeEquisolid** — classic fisheye mappings.
- **Equirectangular** — full panorama unwrap.
- **SphericalHalo** — Devil Daggers / Hyper Demon style. Periphery is pulled into the frame without crushing the center the way classic fisheye does. Up to ~180° usable.

## Player side

Camera projection is driven by `projection` + `projectionStrength` (and `edgeBand` / `bubbleInner` where relevant).
Actual shader or compositor is later; the profile only names the mapping and the knobs.

Expanded is the practical high-awareness option. Halo is the stylistic one.

## Entity side (simplified)

Same resource, fewer knobs. The brain reads:

- Effective vision cone (from Mode)
- Accuracy multiplier when the target is outside the normal cone but inside the peripheral (or anywhere for Halo)
- Reaction time multiplier (Halo slows it)
- Whether rear is visible at all (Halo = yes)

No multi-camera for AI. Just cone math and a resolution/accuracy scalar.

## Stacking

One profile per pawn. Perks that touch vision (Tunnel, Spare eye, etc.) still multiply the numbers the profile already set. Do not invent a second vision system.
