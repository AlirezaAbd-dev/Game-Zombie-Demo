using Godot;
using TopDownDemo.Core;

namespace TopDownDemo.Systems;

/// <summary>Keeps the refuge state coherent, including saves from the original lab prototype.</summary>
public partial class LabCampaignRules : Node
{
    private StoryDirector _story;
    private bool _reconciling;

    public override void _Ready() => CallDeferred(nameof(Bind));

    private void Bind()
    {
        _story = StoryDirector.Instance;
        if (_story == null)
        {
            GD.PushError("Laboratory rules require a StoryDirector.");
            return;
        }
        _story.FlagsChanged += Reconcile;
        Reconcile();
    }

    private void Reconcile()
    {
        if (_reconciling || !_story.HasFlag(StoryFlags.LEILA_RESCUED)) return;
        _reconciling = true;
        _story.RemoveFlag(StoryFlags.LEILA_ABANDONED);
        _story.SetFlag(StoryFlags.LEILA_RESCUE_ACCEPTED);
        _story.SetFlag("lab_route_decided");
        _story.SetFlag("lab_filtration_online");
        _story.SetFlag("lab_route_complete");
        _reconciling = false;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_story)) _story.FlagsChanged -= Reconcile;
    }
}
