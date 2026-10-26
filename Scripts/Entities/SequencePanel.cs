using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class SequencePanel : Node2D
{
    [Export] public SequencePanelData Data { get; set; }
    [Export] public InteractableComponent Interactable { get; set; }
    public int AcceptedSteps { get; private set; }
    private SequencePanelUI _view;
    private StoryDirector _story;

    public override void _Ready()
    {
        if (Data == null || Interactable == null || Data.Steps.Length == 0 || Data.CorrectOrder.Length == 0
            || string.IsNullOrWhiteSpace(Data.CompletionFlag))
        { GD.PushError($"Sequence panel requires steps, a completion flag, and controls: {GetPath()}"); return; }
        foreach (int index in Data.CorrectOrder)
            if (index < 0 || index >= Data.Steps.Length)
            { GD.PushError($"Sequence panel has an invalid step index: {GetPath()}"); return; }
        _story = StoryDirector.Instance;
        if (_story == null) { GD.PushError("Sequence panel requires story service."); return; }
        _view = new SequencePanelUI { Layer = 110 };
        AddChild(_view);
        _view.StepRequested += Submit;
        Interactable.Interacted += Open;
        _story.FlagsChanged += Refresh;
        Refresh();
    }

    private void Open(Player player)
    {
        if (_story.HasFlag(Data.CompletionFlag) || GetTree().Paused) return;
        AcceptedSteps = 0;
        _view.Open(Data, player);
    }

    private void Submit(int step)
    {
        if (!_view.IsOpen || _story.HasFlag(Data.CompletionFlag)) return;
        if (step != Data.CorrectOrder[AcceptedSteps])
        {
            AcceptedSteps = 0;
            _view.ShowFeedback(Data.FailureMessage);
            return;
        }
        AcceptedSteps++;
        if (AcceptedSteps < Data.CorrectOrder.Length)
        { _view.ShowFeedback($"ACCEPTED {AcceptedSteps}/{Data.CorrectOrder.Length}: {Data.Steps[step]}"); return; }
        _view.Close();
        _story.SetFlag(Data.CompletionFlag);
        _story.TriggerRadioComms("LIFT CONTROL", Data.SuccessMessage, 8);
        AudioManager.Instance?.PlaySound(SoundType.GeneratorStart, GlobalPosition);
    }

    private void Refresh() => Interactable.IsActive = !_story.HasFlag(Data.CompletionFlag);

    public override void _ExitTree()
    {
        if (IsInstanceValid(_story)) _story.FlagsChanged -= Refresh;
    }
}
