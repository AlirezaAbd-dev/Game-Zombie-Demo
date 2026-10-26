using Godot;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

/// <summary>Authored enemies remain dormant until a persistent story event occurs.</summary>
public partial class EnemyActivationComponent : Node
{
    [Export] public string ActivationFlag { get; set; } = string.Empty;
    private Zombie _enemy;
    private StoryDirector _story;
    private uint _layer;
    private uint _mask;
    private ProcessModeEnum _processMode;

    public override void _Ready()
    {
        _enemy = GetParent() as Zombie;
        _story = StoryDirector.Instance;
        if (_enemy == null || _story == null || string.IsNullOrWhiteSpace(ActivationFlag))
        {
            GD.PushError($"Enemy activation requires an enemy, story director, and flag: {GetPath()}");
            return;
        }
        _enemy.Ready += Bind;
    }

    private void Bind()
    {
        _layer = _enemy.CollisionLayer;
        _mask = _enemy.CollisionMask;
        _processMode = _enemy.ProcessMode;
        _story.FlagsChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (_enemy.IsQueuedForDeletion() || _enemy.IsDead) return;
        bool active = _story.HasFlag(ActivationFlag);
        _enemy.Visible = active;
        _enemy.CollisionLayer = active ? _layer : 0;
        _enemy.CollisionMask = active ? _mask : 0;
        _enemy.ProcessMode = active ? _processMode : ProcessModeEnum.Disabled;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_story)) _story.FlagsChanged -= Refresh;
    }
}
