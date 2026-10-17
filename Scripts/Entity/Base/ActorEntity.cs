using Godot;
using Hypersteel.Damage;
using Hypersteel.Health;
using Hypersteel.Abilities.Kits;

namespace Hypersteel.Entity;

public partial class ActorEntity : CharacterBody3D, IDamageable
{
	[Export] public KitSheet kit;
	[Export] public HealthComponent health;
	[Export] public StatusComponent status;
	[Export] public FeelingsComponent feelings;
	[Export] public JumpKit jumpKit;
	[Export] public EntityStats stats;
	[Export] public VisionProfile vision;
	[Export] public StringName entityId = "actor";
	[Export] public float Mass = 80f;
	[Export] public bool collisionDamage = true;

	[Export] bool wantCoverLog;
	public bool WantCoverLog
	{
		get => wantCoverLog;
		set => wantCoverLog = value;
	}

	public HealthBand Band { get; private set; } = HealthBand.High;
	public System.Collections.Generic.List<CoverReduction> LastCoverLog { get; private set; }

	public EntityStats Stats => stats ??= new EntityStats { Mass = Mass };

	public override void _Ready()
	{
		stats ??= new EntityStats { Mass = Mass };
		Mass = stats.Mass;
		if (vision != null)
			vision.ApplyTo(stats);
		KitComposer.Compose(this, kit);
		health ??= GetNodeOrNull<HealthComponent>("Health");
		health ??= FindChild("Health", true, false) as HealthComponent;
		status ??= GetNodeOrNull<StatusComponent>("Status");
		feelings ??= GetNodeOrNull<FeelingsComponent>("Feelings");
		jumpKit ??= GetNodeOrNull<JumpKit>("JumpKit");
		if (jumpKit != null && jumpKit.Motor == null)
			jumpKit.BindBody(this);
		if (collisionDamage && GetNodeOrNull<CollisionDamage>("CollisionDamage") == null)
		{
			var cd = new CollisionDamage { Name = "CollisionDamage", Mass = Mass };
			AddChild(cd);
		}
		if (health != null)
			health.Damaged += OnHealthDamaged;
	}

	public void Hurt(Hit hit) => health?.Hurt(hit);

	public virtual void Hurt(DamagePacket packet)
	{
		if (wantCoverLog)
			LastCoverLog = packet.CoverLog;
		else
			LastCoverLog = null;
		health?.Hurt(packet);
	}

	public bool IsDead => health != null && health.State != null && health.State.Dead;

	protected virtual void OnHealthDamaged(float taken, int segment)
	{
		HealthBand next = BandFromHealth();
		if (next == Band) return;
		HealthBand prev = Band;
		Band = next;
		OnHealthBand(prev, next);
	}

	protected virtual void OnHealthBand(HealthBand from, HealthBand to) { }

	HealthBand BandFromHealth()
	{
		if (health?.State == null) return HealthBand.High;
		float torso = health.State.HpOf(BodySegment.Torso);
		float max = health.rules != null ? Math.Max(1f, health.rules.torsoHp) : 100f;
		float p = torso / max;
		if (p <= 0f) return HealthBand.Dead;
		if (p < 0.25f) return HealthBand.Low;
		if (p < 0.6f) return HealthBand.Medium;
		return HealthBand.High;
	}
}

public enum HealthBand
{
	High = 0,
	Medium,
	Low,
	Dead,
}
