using Godot;

namespace TopDownDemo.Entities.Components;

public partial class ObstacleSteeringComponent : Node
{
    [Export] public CharacterBody2D Body { get; set; }
    [Export] public float Radius { get; set; } = 44;
    private readonly PhysicsShapeQueryParameters2D _query = new() { CollisionMask = 1 };
    private readonly CircleShape2D _shape = new();
    private readonly Vector2[] _directions = new Vector2[16];
    private Vector2 _previous;
    private Rid _targetRid;
    private readonly Godot.Collections.Array<Rid> _exclude = new();

    public override void _Ready()
    {
        Body ??= GetParent() as CharacterBody2D;
        _shape.Radius = Radius;
        _query.Shape = _shape;
        for (int i = 0; i < _directions.Length; i++) _directions[i] = Vector2.FromAngle(i * Mathf.Tau / _directions.Length);
        if (Body == null) GD.PushError("Obstacle steering requires a character body.");
    }

    public Vector2 GetDirection(CharacterBody2D target)
    {
        Vector2 desired = (target.GlobalPosition - Body.GlobalPosition).Normalized();
        if (_targetRid != target.GetRid())
        {
            _targetRid = target.GetRid();
            _exclude.Clear(); _exclude.Add(Body.GetRid()); _exclude.Add(_targetRid);
            _query.Exclude = _exclude;
        }
        Vector2 best = Vector2.Zero;
        float bestScore = float.NegativeInfinity;
        foreach (Vector2 direction in _directions)
        {
            _query.Transform = new Transform2D(0, Body.GlobalPosition + direction * 75);
            if (Body.GetWorld2D().DirectSpaceState.IntersectShape(_query, 1).Count != 0) continue;
            float score = direction.Dot(desired) + direction.Dot(_previous) * 0.3f;
            if (score > bestScore) { best = direction; bestScore = score; }
        }
        _previous = best;
        return best;
    }
}
