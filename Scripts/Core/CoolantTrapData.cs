using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class CoolantTrapData : Resource
{
    [Export] public float BossDamage { get; set; } = 200;
    [Export] public float InfectedDamage { get; set; } = 100;
    [Export] public float RechargeSeconds { get; set; } = 24;
    [Export] public float StaggerSeconds { get; set; } = 1.8f;
    [Export] public string PowerFlag { get; set; } = "silo_backup_online";
    [Export] public string TimerId { get; set; } = "silo_coolant_ready_at";
}
