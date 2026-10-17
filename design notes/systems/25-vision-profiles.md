# Vision profiles

Drop-in sheet for how an entity sees and (for the player) how the camera projects.

Not a new Resolve fork. Not a new kit. A resource you stamp on the pawn or operator the same way you stamp `EntityStats` or a perk.

## Modes

| Mode | Player | NPC / AI |
|---|---|---|
| **Normal** | Rectilinear. Standard cone. No extra cost. | Default `VisionConeDeg` (usually ~110). Full accuracy inside the cone. Blind outside. |
| **Expanded** | Significant peripheral vision. Mild framerate cost (wider FOV + projection remap). Center stays clean for aiming. | Peripheral cone opens to ~200°. Reduced resolution / accuracy outside the normal cone. |
| **Halo360** | Confusing and hard to get used to. Interesting. Full spherical-ish awareness; center still usable but the edges pull in side/rear information. | No blind spots. Represented as several glowing lenses around the head. Reaction speed slowed a bit. Cannot be snuck up on. |

## Player side

- Camera projection is driven by the profile (rectilinear / expanded blend / halo). Actual shader or compositor is later; the profile only names the mode and the FOV/strength knobs.
- Expanded is the practical high-awareness option. Halo is the stylistic one.

## Entity side (simplified)

Same resource, fewer knobs. The brain reads:

- Effective vision cone
- Accuracy multiplier when the target is outside the normal cone but inside the expanded one
- Reaction time multiplier (Halo slows it)
- Whether rear is visible at all (Halo = yes)

No multi-camera for AI. Just cone math and a resolution/accuracy scalar.

## Stacking

One profile per pawn. Perks that touch vision (Tunnel, Spare eye, etc.) still multiply the numbers the profile already set. Do not invent a second vision system.
