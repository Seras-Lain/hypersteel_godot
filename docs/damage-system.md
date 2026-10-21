# Hypersteel damage, armor, status

Locked 2026-09-15. Companion to `docs/entity-systems.md`.
Systems stay universal. Resistances, armor, feelings, and threshold reactions are **sheets + local hooks**, never `if (ryko)` inside Resolve.

---

## Auto-composition by component flag

**Yes, with a closed list. No reflection magic.**

A pawn sheet (`ActorKit` / flags on `HealthRuleConfs` or a sibling `ComponentSet` Resource) lists which appliances exist:

```
hasHealth = true
hasFeelings = true      // flinch / pain
hasStatus = true        // tags + DoT clocks
hasMorale = false       // AI only; drone no
critSockets = [Head, Fuel]
```

`ActorEntity._Ready` (or a one-shot `KitComposer`) instantiates only those components as children if they are missing. That is Unreal “add component if tag” without scanning every C# type.

**Good**
- One soldier prefab, two sheets (hasFeelings on / off).
- Drone sheet: health + status, no feelings, no morale.
- Ryko sheet: health + status + feelings; morale unused.

**Bad**
- String-to-type factory (`"FeelingsComponent"` → `Type.GetType`).
- Flags that invent new systems at runtime.
- A global EventBus every pawn must unsubscribe from.

Semi-special properties (armor attributes, status tags, hot/cold/wet) are **data on the sheet or on the plate**, stacked by the same Resolve function. They are not extra subclasses.

Stack rule for armor attributes: at most **two** occurrences. Second occurrence is **half** the first.

---

## Godot hooks vs Unreal interfaces

Unreal: `IDamageable` cast on the actor, `ReceiveDamage(FDamageEvent)`.

Godot has no UINTERFACE. Use **one typed door + signals**, not `Call("hurt")` on random nodes.

| Unreal | Godot (this project) |
|---|---|
| `Implements<IDamageable>()` | collider owner is `ActorEntity` or has `HealthComponent` |
| interface method | `actor.Hurt(DamagePacket)` |
| multicast delegate / Event | Godot **signal** on the component |
| Gameplay Tag | `StatusTag` bit/flag on `StatusState` |
| Gameplay Effect | packet channel + duration on `StatusState` |

C# interfaces (`IHurtbox`) are fine **inside** C# when you already hold the object. Do not interface-cast `Node` from a physics hit. Walk:

```
Area/body hit
  → HurtBox (knows segment / bone / plate)
  → ActorEntity.Hurt(packet)
  → HealthComponent / StatusComponent
  → signal Damaged(result)
  → THIS entity's hooks (bands, flinch, BT event)
```

Signals to use (local, not autoload):

- `HealthComponent.Damaged(result)`
- `HealthComponent.Died`
- `HealthComponent.BandChanged(old, new)` is **optional to emit from Health**; the **entity** decides whether to care. Preferred: entity computes bands on `Damaged`.
- `StatusComponent.TagAdded / TagRemoved`
- `FeelingsComponent.Flinch(intensity)`

Projectile never talks to AI. Soldier `_Ready` connects `Damaged` → `OnDamaged`. Drone does not connect. That is the observer rule.

---

## Damage packet

One hit is a **packet**, not a single float. Replaces the old single-kind `Hit` over time.

```
DamagePacket
  name                // ".50 BMG uranium"
  source              // Projectile | Fist | Blade | Beam | Splash | Contact | Environment
  ap                  // armor piercing level (int)
  kinetic             // magnitude (same unit as HP)
  kineticFlags        // Piercing, Blunt, Fragmenting, Hypervelocity, Cutting
  channels[]          // extra typed magnitudes (thermal, toxic, ion, …)
  special             // optional recipe (shrapnel, stick, chain)
  instigator
  point, normal
  bone / segment
```

`source` picks **which kinetic story** this is (bullet vs fist). It does not stack three kinetic sources on one packet.

### Example: .50 BMG uranium

- kinetic 34000, flags: Piercing
- special Shrapnel: 6 fragments, fragmenting, inherit DoT, piercing
  - if the round **exits** (overpen / cavitation through back armor): spawn 6 world fragments
  - if it **does not** exit: occupant takes 6 virtual shrapnel ticks
    `each = (kinetic / n) * (1 / n)` → `(base / 6) * (1/6)` of kinetic, same type + inherited DoT
- DoT: thermal 3000 (pyrophoric), toxic 450
  - Ryko heal is 1000/s; toxic 450 means **4.5% heal cut** → `heal *= (1 - toxic/10000)` if toxic is stored in the same units as 450 → 4.5%. Sheet documents the scale: toxic channel `/10000 = heal mul penalty`.

Shrapnel spawned into the world is a new packet with scaled kinetic, same AP family unless specified, inherited DoT channels, no infinite recurse (special.shrapnelGeneration = false on children).

---

## Channels (typed magnitudes on the packet)

Not a bool list. A channel is a name + amount + optional submode.

| Channel | What Resolve does |
|---|---|
| Kinetic | AP ladder vs plate, then occupant. Flags change bounce/stick/gib. |
| Thermal | Adds heat to **segment**. Ignition when `segmentTemp >= ignitionTemp` → Burning. |
| Pyrophoric | One-shot heat + spark splash (small neighbor heat). |
| Hot | Heat, no splash. |
| Incendiary | Contact DoT while overlapping; **sticks** if target armor level **>** this AP instead of bounce. |
| Lightning / Ion | Charge + shock. Above thresholds: chain lightning and/or Zap. Wet/Frozen targets also **store** a capacitor pulse (see below). |
| Plasma | Heat + ion. |
| Laser | Cutting + burn. Reflects off Mirror / metal. Ignores plasma shields. |
| Cutting | With kinetic: blades. |
| Toxic | Heal-rate penalty + optional flesh DoT. |
| Acid | Lowers armor **level** by `1 * acidLevel` (float, floored for ladder). |

Environmental / stance channels on the **target** (status, not on the incoming bullet unless the bullet applies them):

| Tag | Effect |
|---|---|
| Sanded | Shorter fire duration. |
| Hot (body) | Weapons overheat faster. Energy weapons stronger. Kinetic output from this entity reduced (before target embrittlement mul). |
| Cold (body) | Weapons do not overheat. Stamina up, heat buildup down. Own kinetic down; own energy up. Incoming kinetic vs cold metal target uses embrittlement mul. |
| Frozen | Stronger Cold. Brittle. Counts as a **capacitor** with Wet (below). Thaw → Wet briefly. |
| Wet | More shock taken; cannot be Charged. Huge fire resist. **Capacitor** (below). |
| Oiled | Slip + flammable. |
| ExplosiveCoated | Detonate if Burning, overwhelming blunt, or Shocked. |
| Bleeding | Flesh only. HP leak. |
| LeakingFuel / Coolant / Power | Machine leak; fuel leak can kaboom at crit socket. |
| Stunned / Paralyzed | AI nervous skip for N ticks. |
| Zapped | Electronics scramble (taser/lightning). Distinct from stun. |
| Blinded / Deafened | Flashbang. |
| Ragdolled | Physics takeover. |
| Liquefied | Flesh only. Lethal. Never bots. |
| Stimmed | Adrenaline: fight harder, bleed faster, stun/poison resist scales with level. Can fight while bleeding out. |
| Tranqued | Inverse of Stimmed. |
| Amped | Stimmed for robots. |
| Enraged | Status tag exists so zones/context can apply it. AI BT decides when. Not applied by Health. |
| Encourage / Terrified | **Not** this table. Morale table on AI. |

Flashbang: Blinded + Deafened + Paralyzed/Stun.
Taser: Zapped + Ion + Paralyzed. No blind/deaf.

### Wet / Frozen capacitor

Wet or Frozen bodies **store Ion / Lightning** instead of only taking it once.

On an Ion/Lightning packet (and Plasma that writes ion) while Wet or Frozen:

1. Resolve the hit as usual (Wet already multiplies shock taken).
2. Write a **stored charge** on that segment = a fraction of the incoming ion magnitude (sheet knob; default ~50%). Frozen stores more than Wet.
3. After a **short delay** (~0.4–0.8 s, sheet), the body **re-emits** a shock pulse:
   - self: second Ion tick + possible Zap / brief Paralyze
   - neighbors in a small radius / contact: weaker Ion splash
4. Stored charge dumps once. A new ion hit while still Wet/Frozen can refill, but **does not stack unbounded** — cap at one stored pulse per segment.
5. Wet still **blocks Charged** as a plate state. Capacitor is not Charged; it is a delayed re-emit. They are different flags.
6. If ExplosiveCoated is also on the body, the re-emit counts as Shocked for the coat cook-off.

Ice grenade → lightning → plasma bloom is this rule plus Plasma on a body that just dumped charge, not a special case in Resolve.

---

## AP vs armor ladder

`diff = armorLevel - ap` (higher armor = harder).
Use **effective** armor after attributes / acid / shatter / heat.

| Outcome | Condition | Kinetic that lands |
|---|---|
| Null | ap ≤ armor − 8 | 0. Impact sound only. |
| Bounce | ap ≤ armor − 4 (and not Null) | 20% + 5% per level closer to Damage band; −5% per extra level toward Null. Clamp ≥ 0. |
| Damage | \|ap − armor\| ≤ 2 | 100% |
| Overpen | ap ≥ armor + 3 | 130% at armor+1 equivalent step, **−10% per AP level above the start of overpen**. Document implementation: `mul = 1.30 - 0.10 * (ap - (armor + 3))`, floor 0.1. |
| Cavitation | hypervelocity **and** ap ≥ armor + 6 | Instant gib/crit that part. Shrapnel from the body: each fragment `(limbHpBefore / n) + 0.05 * packet.kinetic`. |

Incendiary exception: if armor **>** ap, **stick** instead of Bounce when the incendiary flag is set.

### Armor levels (int)

| Lvl | Name | Meaning |
|---|---|
| 0 | Unarmored | Thin tissue (fingers). |
| 1 | Durable | Unarmored human chest. |
| 2 | Trivial | ~3 mm aluminum. |
| 3 | Light | Kevlar-class fabric. |
| 5 | Plate | Real Level IV plates. |
| 6 | Heavy / light vehicle | SiC composite; breaks normal rifle. |
| 7 | Vehicle | ~.50 resist. |
| 8 | Tank | ~20 mm. |
| 9 | Medium tank | ~40 mm. |
| 10 | Heavy tank | Bounces .50. |
| 11 | Bunker | Stops ~.40 class; still eats significant damage from it. |
| 12 | World class | ~80 mm graphene-class. |

Level 4 is unused as a named rung; do not invent mid-rungs in Resolve. Sheets may still store 4 if a designer needs it — ladder math does not care about the names.

---

## Material / plate attributes

Applied on **inherent segment armor** and/or **external plates**. Multiple allowed unless a conflict row forbids it. Track conflicts on the sheet.

Second copy of the same attribute = half effect.

| Attribute | Notes |
|---|---|
| MultiLayerComposite | Less shrapnel. DoT **strength** up, duration not. Incoming AP −1. |
| SolidMetal | Partial laser reflect. Worse embrittlement and heat. Ion/shock: 40% → thermal, 40% reflect, 20% DoT. |
| PlasmaShield | Massive kinetic resist. Lasers and DoTs pass. Plasma (no ion) **feeds** it. HP bleeds fast. External lightning can explode it. Contact damage type Lightning; damages self too. Stops AP ≤ 5 rounds dead (Null-equivalent for those). |
| Mirror | Full laser resist. Energy/DoT reflected not absorbed. Weak to kinetic unless other attrs. |
| ShearThickPadded | Less kinetic; **increases** incoming AP unless also Composite or Metal. Less thermal embrittle/heat. Mild shock resist. Less fall/impact, stun, shockwave, ragdoll time, flinch/stumble/recoil. |
| NanotubeFiber | Kevlar-like. Flammable. Helps vs cold, energy, impact mildly. 30% chance incoming AP −1. Less shrapnel. |
| CeramicPlate | High AP vs first hits; brittle. After shatter, leftover padding only. Heat + kinetic spike → Shatter. Poor vs blunt shockwave. |
| Flesh | Energy resists better than ballistic; both still injure. DoTs apply. Liquefy/Bleed legal. |
| FleshBone | As Flesh + local armor +1 vs pierce on that socket (ribs, skull). |
| FleshBorged | Flesh + Metal or Composite plate on top; bleed reduced; Zap/Amped legal. |
| MetalFoamPadded | Kinetic/fall down; heat soaks; worse ion coupling than solid metal; mild AP −0.5 (round at ladder). |
| MagneticWhipple | Layers of thin metal pushed out by a Powered Magnet. Low AP addition over Frame, but good at protecting Frame from Ions and Projectiles. Needs power to retain shape. |

### Weakening (on the plate/segment)

| State | Effect |
|---|---|
| Acid | armorLevel − 1*level (float). |
| ThermalEmbrittlement | ΔT ≥ 300° in ≤10 s → plate can shatter off under enough kinetic. |
| Shattered | Off or hanging. 20% effective. **Stops reducing AP**. Only padding/kevlar-like remainder counts. |
| Sliced | Fabric no longer reduces AP. Worse vs shrapnel, impact, thermal. |
| Charged | Contact damage on bump. Extra power budget. Plasma shields flicker / may explode. Discharge hurts both. |

### Context (not official tags; derived from temp/wet)

- **Armor hot:** easier to pierce (treat as AP+ or armor− for ladder — implement as incoming AP +1 at high temp). Plasma plate **fails**. Contact burn. DoT ticks **faster** with temp (chemistry). Kinetic **non-pierce** reduced if metal. Shrapnel spawn count − 10%*(tempC/100). Armor **level number** not lowered.
- **Armor cold:** plasma shield takes less. Normal plates take more kinetic.

Conflicts to flag on the sheet (Resolve uses first-wins or explicit deny):
Wet vs Charged (Wet wins: cannot charge).
Hot vs Cold body.
Stimmed vs Tranqued.
Mirror vs “absorb energy” fantasy attrs — Mirror reflect wins for lasers.
PlasmaShield vs SolidMetal on the **same** layer — illegal; they are different layers (shield then plate).
Wet/Frozen capacitor is **not** Charged. Do not collapse them.

---

## Plates then occupant (inward)

Each segment:

1. External plate(s) in order (shield → ceramic → composite → fiber).
2. Inherent body armor (Ryko: nanotube everywhere; plates are Composite+Metal on top).
3. Occupant HP for that segment.

Plate **spends its own HP** to cut what reaches the next layer. Overpen / cavitation can skip or destroy a layer and keep going.
Plasma shield layer: kinetic ≤ AP5 dies here; lasers/DoT skip this layer.

Ryko example:
- Inherent: NanotubeFiber all segments.
- Worn plates: MultiLayerComposite + SolidMetal (second attr full; a third Composite would be half).
- Hit on chest: shield? → plate HP → nanotube → torso HP.

---

## Crit sockets

Tagged on segments. Enough pierce / destroy → special, implemented as **data on the sheet** + entity hook if story-specific.

| Socket | Default |
|---|---|
| Brain | Pierce → kill. |
| Eye | All eyes gone → Blinded. One eye → FOV cut. |
| LegActuator | Slow or no walk. |
| Fuel | Kaboom packet, Explosive source. |
| Battery / Core | Zap + Amped drop / kill bots. |
| Coolant | Overheat spiral. |
| WeaponHand | Drop / accuracy break. |

Flesh-only sockets ignore bots; bot sockets ignore flesh.

---

## Resolve pipeline (single function)

1. Stamp segment from bone map.
2. Build effective armor (attrs, acid, shatter, sliced, temp context).
3. Walk layers inward. For each layer: AP ladder for **kinetic**; other channels follow material (laser vs mirror, DoT vs plasma shield, etc.).
4. Apply specials (stick, shrapnel, cavitation gib).
5. Add heat / ion / toxic to segment StatusState.
6. Ignition / charge / coated-explode thresholds. If Wet or Frozen and ion landed, **arm capacitor** (do not set Charged).
7. Write HP. Emit `Damaged(result)` once.
8. Entity hook: bands, feelings, BT.
9. Status ticks later in `_PhysicsProcess` on StatusComponent only (DoT expire faster when hot). Capacitor dump fires here after delay.

Resistances = **multipliers on the actor sheet** (`resistKinetic`, `resistThermal`, …`) applied after the layer walk, not new code paths.

---

## Composition reminder

- Health does numbers and layers.
- Status does tags, heat, DoT clocks.
- Feelings does flinch from `result` + durability / `attackRelativeHealthLossPercent` / resistance.
- Entity does medium→low and AI critical.
- Morale is a different table.

Auto-compose from the sheet flags. Do not put feelings logic in Health.
