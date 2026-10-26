using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class PowerRoutingPanel : Node2D
{
    [Export] public PowerRoutingData Data { get; set; }
    [Export] public InteractableComponent Interactable { get; set; }
    private PowerRoutingUI _view;

    public override void _Ready()
    {
        if (Data == null || Interactable == null || string.IsNullOrWhiteSpace(Data.CompletionFlag)
            || Data.Circuits.Length == 0 || Data.Circuits.Length > 16
            || Data.RequiredMask < 0 || Data.RequiredMask >= (1 << Data.Circuits.Length))
        {
            GD.PushError($"Power panel requires puzzle data and interaction area: {GetPath()}");
            return;
        }
        _view = new PowerRoutingUI { Layer = 110 };
        AddChild(_view);
        _view.ApplyRequested += Apply;
        Interactable.Interacted += Open;
        StoryDirector.Instance.FlagsChanged += Refresh;
        Refresh();
    }

    private void Open(Player player)
    {
        if (StoryDirector.Instance?.HasFlag(Data.CompletionFlag) != true) _view.Open(Data, player);
    }

    private void Apply(int mask)
    {
        if (mask != Data.RequiredMask)
        {
            _view.ShowFeedback(Data.FailureMessage);
            return;
        }
        _view.Close();
        StoryDirector.Instance.SetFlag(Data.CompletionFlag);
        if (Data.RestoresFacilityPower) StoryDirector.Instance.OnGeneratorActivated();
        StoryDirector.Instance.TriggerRadioComms(Data.Speaker, Data.SuccessMessage, 9);
        AudioManager.Instance?.PlaySound(SoundType.GeneratorStart, GlobalPosition);
    }

    private void Refresh() => Interactable.IsActive = !StoryDirector.Instance.HasFlag(Data.CompletionFlag);

    public override void _ExitTree()
    {
        if (StoryDirector.Instance != null) StoryDirector.Instance.FlagsChanged -= Refresh;
    }
}
