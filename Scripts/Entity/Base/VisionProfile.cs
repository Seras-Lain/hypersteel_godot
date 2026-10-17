using Godot;

namespace Hypersteel.Entity;

/// <summary>
/// Drop-in vision sheet. Player reads it for camera projection; AI reads the simplified cone / accuracy / reaction numbers.
/// Stamp on ActorEntity or operator the same way as EntityStats.
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

	[Export] public Mode mode = Mode.Normal;

	/// <summary>Base horizontal cone (degrees). Normal default ~110.</summary>
	[Export] public float coneDeg = 110f;

	/// <summary>Expanded peripheral cone. Used by Expanded and as the soft outer for Halo.</summary>
	[Export] public float peripheralConeDeg = 200f;

	/// <summary>Accuracy multiplier when target is outside the normal cone but inside peripheral. 1 = full. Lower = worse.</summary>
	[Export] public float peripheralAccuracy = 0.55f;

	/// <summary>Reaction time multiplier. Halo > 1 slows reaction.</summary>
	[Export] public float reactionMul = 1f;

	/// <summary>Player only: projection strength / FOV bonus. 0 = pure rectilinear.</summary>
	[Export] public float projectionStrength = 0f;

	/// <summary>Player only: mild framerate note. Not a hard cap; just a flag for the renderer later.</summary>
	[Export] public bool widerFovCost = false;

	public float EffectiveConeDeg => mode switch
	{
		Mode.Expanded => Mathf.Max(coneDeg, peripheralConeDeg),
		Mode.Halo360 => 360f,
		_ => coneDeg,
	};

	public bool CanSeeRear => mode == Mode.Halo360;

	public float AccuracyAtAngle(float angleFromLookDeg)
	{
		var half = coneDeg * 0.5f;
		if (angleFromLookDeg <= half) return 1f;
		if (mode == Mode.Normal) return 0f;
		if (mode == Mode.Halo360) return peripheralAccuracy;
		// Expanded: full inside normal, reduced in the outer band, zero past peripheral
		var periHalf = peripheralConeDeg * 0.5f;
		if (angleFromLookDeg <= periHalf) return peripheralAccuracy;
		return 0f;
	}

	/// <summary>Apply the simplified numbers onto an existing EntityStats (AI path).</summary>
	public void ApplyTo(EntityStats stats)
	{
		if (stats == null) return;
		stats.VisionConeDeg = EffectiveConeDeg;
		stats.Reaction *= reactionMul;
	}

	public static VisionProfile Normal() => new()
	{
		mode = Mode.Normal,
		coneDeg = 110f,
		peripheralAccuracy = 0f,
		reactionMul = 1f,
		projectionStrength = 0f,
		widerFovCost = false,
	};

	public static VisionProfile Expanded() => new()
	{
		mode = Mode.Expanded,
		coneDeg = 110f,
		peripheralConeDeg = 200f,
		peripheralAccuracy = 0.5f,
		reactionMul = 1f,
		projectionStrength = 0.35f,
		widerFovCost = true,
	};

	public static VisionProfile Halo() => new()
	{
		mode = Mode.Halo360,
		coneDeg = 110f,
		peripheralConeDeg = 360f,
		peripheralAccuracy = 0.65f,
		reactionMul = 1.25f,
		projectionStrength = 0.7f,
		widerFovCost = true,
	};
}
