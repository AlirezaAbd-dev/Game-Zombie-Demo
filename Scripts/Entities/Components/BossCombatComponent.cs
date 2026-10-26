using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

public enum BossAttackPhase { Idle, MeleeWindup, AcidWindup, Recovery }

public partial class BossCombatComponent : Node
{
    [Export] public BossCombatData Data { get; set; }
    public BossAttackPhase Phase { get; private set; }
    public Vector2 LockedDirection { get; private set; }
    public float Progress { get; private set; }
    private float _elapsed;
    private float _meleeCooldown;
    private float _acidCooldown = 4;
    private Boss_Unit9 _boss;

    public override void _Ready()
    {
        _boss = GetParent() as Boss_Unit9;
        Data ??= new BossCombatData();
        if (_boss == null || Data.MeleeWindup <= 0 || Data.AcidWindup <= 0 || Data.RecoverySeconds <= 0)
            GD.PushError("Boss combat requires a boss parent and positive attack timings.");
    }

    // Called exclusively by the boss's physics state, including pause and stagger handling.
    public bool Step(float delta, Player target)
    {
        _meleeCooldown -= delta;
        _acidCooldown -= delta;
        if (Phase != BossAttackPhase.Idle)
        {
            _elapsed += delta;
            float duration = Phase == BossAttackPhase.MeleeWindup ? Data.MeleeWindup
                : Phase == BossAttackPhase.AcidWindup ? Data.AcidWindup : Data.RecoverySeconds;
            Progress = Mathf.Min(1, _elapsed / duration);
            if (_elapsed >= duration)
            {
                if (Phase == BossAttackPhase.MeleeWindup) ExecuteMelee(target);
                else if (Phase == BossAttackPhase.AcidWindup) ExecuteAcid();
                SetPhase(Phase == BossAttackPhase.Recovery ? BossAttackPhase.Idle : BossAttackPhase.Recovery);
            }
            return true;
        }
        Vector2 offset = target.GlobalPosition - _boss.GlobalPosition;
        if (offset.Length() <= _boss.AttackRange && _meleeCooldown <= 0 && HasSight(target))
        {
            LockedDirection = offset.Normalized();
            _meleeCooldown = Data.MeleeCooldown;
            SetPhase(BossAttackPhase.MeleeWindup);
            AudioManager.Instance?.PlaySound(SoundType.ZombieGroan, _boss.GlobalPosition,1.2f);
            return true;
        }
        if (_boss.CurrentPhase >= 2 && offset.Length() > 140 && _acidCooldown <= 0 && HasSight(target))
        {
            LockedDirection = offset.Normalized();
            _acidCooldown = _boss.CurrentPhase == 3 ? Data.AcidCooldown * 0.65f : Data.AcidCooldown;
            SetPhase(BossAttackPhase.AcidWindup);
            AudioManager.Instance?.PlaySound(SoundType.VentBreak, _boss.GlobalPosition);
            return true;
        }
        return false;
    }

    public bool HasSight(Player player)
    {
        using var query = PhysicsRayQueryParameters2D.Create(_boss.GlobalPosition, player.GlobalPosition, 1,
            new Godot.Collections.Array<Rid> { _boss.GetRid(), player.GetRid() });
        return _boss.GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    private void ExecuteMelee(Player player)
    {
        Vector2 offset = player.GlobalPosition - _boss.GlobalPosition;
        if (offset.Length() <= _boss.AttackRange + 25 && HasSight(player)
            && Mathf.Abs(LockedDirection.AngleTo(offset)) <= Data.SweepHalfAngle)
            player.TakeDamage(_boss.AttackDamage, offset.Normalized());
        AudioManager.Instance?.PlaySound(SoundType.FinisherSlash, _boss.GlobalPosition);
    }

    private void ExecuteAcid()
    {
        AudioManager.Instance?.PlaySound(SoundType.VentBreak, _boss.GlobalPosition);
        for (int i = -1; i <= 1; i++)
            ProjectilePool.Instance?.Spawn(_boss.GlobalPosition + LockedDirection * 55,
                LockedDirection.Angle() + i * 0.25f, _boss.GetParent(), 20, 320, true);
    }

    private void SetPhase(BossAttackPhase phase) { Phase = phase; _elapsed = 0; Progress = 0; }
    public void Cancel() { SetPhase(BossAttackPhase.Idle); _meleeCooldown = 0.5f; }
}
