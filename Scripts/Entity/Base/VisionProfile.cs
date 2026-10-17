using Godot;

namespace Hypersteel.Entity;

/// <summary>
/// Drop-in vision sheet.
/// Mode (Normal / Expanded / Halo360) is the supertype that drives AI cone / accuracy / reaction.
/// Projection is the concrete camera mapping the player uses under that mode.
/// Cone angles are adjustable but clamped per mode (rectilinear cannot go as wide as Halo).
/// Stamp on ActorEntity the same way as EntityStats.
/// </summary>
[GlobalClass]
public partial class VisionProfile : Resource
{
	public enum Mode
	{
		Normal = 0,
		Expanded,
		Halo360,
	}

	/// <summary>
	/// Concrete projection. Grouped by which Mode it belongs to.
	/// </summary>
	public enum Projection
	{
		// Normal
		Rectilinear = 0,

		// Expanded — wide but center stays honest
		ExpandedEdgePannini,   // rectilinear center; lerp to Pannini on vertical bands 0–10% and 90–100%
		Pannini,               // full Pannini (verticals + center-lines stay straight)

		// Halo360 — no real blind spots, stylized periphery
		WarpEdgeBubble,        // center oval rectilinear; edges hyper-compress into fisheye bubble
		FisheyeEquidistant,
		FisheyeStereographic,
		FisheyeEquisolid,
		Equirectangular,
		SphericalHalo,         // Devil Daggers / Hyper Demon style: periphery pulled in without crushing center
	}

	public const float MinConeDeg = 45f;

	[Export] public Mode mode = Mode.Normal;
	[Export] public Projection projection = Projection.Rectilinear;

	/// <summary>Base horizontal cone (degrees). Clamped at runtime by MaxConeForMode.</summary>
	[Export] public float coneDeg = 110f;

	/// <summary>Expanded peripheral cone. Clamped at runtime. Used by Expanded and as the soft outer for Halo.</summary>
	[Export] public float peripheralConeDeg = 200f;

	/// <summary>Accuracy multiplier when target is outside the normal cone but inside peripheral. 1 = full.</summary>
	[Export] public float peripheralAccuracy = 0.55f;

	/// <summary>Reaction time multiplier. Halo > 1 slows reaction.</summary>
	[Export] public float reactionMul = 1f;

	/// <summary>Player only: projection strength. 0 = pure rectilinear, 1 = full effect of the chosen projection.</summary>
	[Export] public float projectionStrength = 0f;

	/// <summary>Player only: mild framerate note for wider FOV / remap.</summary>
	[Export] public bool widerFovCost = false;

	/// <summary>ExpandedEdgePannini: fraction of screen height (0–0.5) that blends toward Pannini. Default 0.1 = top/bottom 10%.</summary>
	[Export] public float edgeBand = 0.1f;

	/// <summary>WarpEdgeBubble: half-size of the protected center oval (0–1, fraction of half-screen).</summary>
	[Export] public float bubbleInner = 0.45f;

	/// <summary>
	/// Maximum legal cone for the current mode / projection.
	/// Normal/rectilinear ~140, Expanded/Pannini ~270, Halo ~360.
	/// </summary>
	public float MaxConeForMode => mode switch
	{
		Mode.Normal => 140f,
		Mode.Expanded => projection == Projection.Pannini ? 270f : 220f,
		Mode.Halo360 => 360f,
		_ => 140f,
	};

	public float ClampedConeDeg => Mathf.Clamp(coneDeg, MinConeDeg, MaxConeForMode);
	public float ClampedPeripheralDeg => Mathf.Clamp(peripheralConeDeg, MinConeDeg, MaxConeForMode);

	public float EffectiveConeDeg => mode switch
	{
		Mode.Expanded => Mathf.Max(ClampedConeDeg, ClampedPeripheralDeg),
		Mode.Halo360 => Mathf.Min(360f, MaxConeForMode),
		_ => ClampedConeDeg,
	};

	public bool CanSeeRear => mode == Mode.Halo360;

	public bool IsExpandedProjection => projection is Projection.ExpandedEdgePannini or Projection.Pannini;
	public bool IsHaloProjection => projection is
		Projection.WarpEdgeBubble or
		Projection.FisheyeEquidistant or
		Projection.FisheyeStereographic or
		Projection.FisheyeEquisolid or
		Projection.Equirectangular or
		Projection.SphericalHalo;

	public float AccuracyAtAngle(float angleFromLookDeg)
	{
		var half = ClampedConeDeg * 0.5f;
		if (angleFromLookDeg <= half) return 1f;
		if (mode == Mode.Normal) return 0f;
		if (mode == Mode.Halo360) return peripheralAccuracy;
		var periHalf = ClampedPeripheralDeg * 0.5f;
		if (angleFromLookDeg <= periHalf) return peripheralAccuracy;
		return 0f;
	}

	/// <summary>Apply the simplified numbers onto an existing EntityStats (AI path). Uses clamped values.</summary>
	public void ApplyTo(EntityStats stats)
	{
		if (stats == null) return;
		stats.VisionConeDeg = EffectiveConeDeg;
		stats.Reaction *= reactionMul;
	}

	// --- factories ---

	public static VisionProfile Normal() => new()
	{
		mode = Mode.Normal,
		projection = Projection.Rectilinear,
		coneDeg = 110f,
		peripheralAccuracy = 0f,
		reactionMul = 1f,
		projectionStrength = 0f,
		widerFovCost = false,
	};

	/// <summary>Expanded supertype: edge-Pannini blend (rectilinear center, Pannini on vertical edge bands).</summary>
	public static VisionProfile ExpandedEdge() => new()
	{
		mode = Mode.Expanded,
		projection = Projection.ExpandedEdgePannini,
		coneDeg = 110f,
		peripheralConeDeg = 200f,
		peripheralAccuracy = 0.5f,
		reactionMul = 1f,
		projectionStrength = 0.45f,
		edgeBand = 0.1f,
		widerFovCost = true,
	};

	public static VisionProfile ExpandedPannini() => new()
	{
		mode = Mode.Expanded,
		projection = Projection.Pannini,
		coneDeg = 110f,
		peripheralConeDeg = 200f,
		peripheralAccuracy = 0.55f,
		reactionMul = 1f,
		projectionStrength = 0.4f,
		widerFovCost = true,
	};

	/// <summary>Halo supertype: center oval protected, edges hyper-compress into fisheye bubble.</summary>
	public static VisionProfile HaloBubble() => new()
	{
		mode = Mode.Halo360,
		projection = Projection.WarpEdgeBubble,
		coneDeg = 110f,
		peripheralConeDeg = 360f,
		peripheralAccuracy = 0.6f,
		reactionMul = 1.2f,
		projectionStrength = 0.65f,
		bubbleInner = 0.45f,
		widerFovCost = true,
	};

	/// <summary>Halo supertype: Devil Daggers / Hyper Demon spherical (periphery pulled in, center not crushed).</summary>
	public static VisionProfile HaloSpherical() => new()
	{
		mode = Mode.Halo360,
		projection = Projection.SphericalHalo,
		coneDeg = 110f,
		peripheralConeDeg = 360f,
		peripheralAccuracy = 0.7f,
		reactionMul = 1.25f,
		projectionStrength = 0.75f,
		widerFovCost = true,
	};

	public static VisionProfile HaloFisheye(Projection fisheye = Projection.FisheyeEquidistant) => new()
	{
		mode = Mode.Halo360,
		projection = fisheye,
		coneDeg = 110f,
		peripheralConeDeg = 360f,
		peripheralAccuracy = 0.55f,
		reactionMul = 1.15f,
		projectionStrength = 0.8f,
		widerFovCost = true,
	};

	public static VisionProfile HaloEquirect() => new()
	{
		mode = Mode.Halo360,
		projection = Projection.Equirectangular,
		coneDeg = 110f,
		peripheralConeDeg = 360f,
		peripheralAccuracy = 0.5f,
		reactionMul = 1.2f,
		projectionStrength = 1f,
		widerFovCost = true,
	};
}
