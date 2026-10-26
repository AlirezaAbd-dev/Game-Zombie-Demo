using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class CampaignInteraction : Node2D
{
    [Export] public CampaignInteractionData Data { get; set; }
    [Export] public InteractableComponent Interactable { get; set; }
    private bool _busy;
    private DialogueManager _dialogue;

    public override void _Ready()
    {
        if (Data == null || Interactable == null)
        {
            GD.PushError($"Campaign interaction is missing data or interaction area: {GetPath()}");
            return;
        }
        Interactable.PromptText = Data.Prompt;
        Interactable.RequiredItemId = Data.RequiredItemId;
        Interactable.RequiredStoryFlag = Data.RequiredFlag;
        Interactable.LockedPromptText = Data.LockedMessage;
        Interactable.Interacted += OnInteracted;
        Refresh();
        if (StoryDirector.Instance != null) StoryDirector.Instance.FlagsChanged += Refresh;
    }

    public override void _ExitTree()
    {
        DisconnectDialogue();
        if (StoryDirector.Instance != null) StoryDirector.Instance.FlagsChanged -= Refresh;
    }

    private void Refresh()
    {
        if (Data == null || Interactable == null) return;
        Interactable.IsActive = !_busy && (Data.Repeatable || string.IsNullOrEmpty(Data.CompletionFlag)
            || StoryDirector.Instance?.HasFlag(Data.CompletionFlag) != true);
    }

    private void OnInteracted(Player player)
    {
        if (_busy) return;
        if (!string.IsNullOrEmpty(Data.DialogueId))
        {
            var dialogue = DialogueManager.Instance;
            var sequence = DialogueDatabase.Get(Data.DialogueId);
            if (dialogue == null || dialogue.IsDialogueActive || sequence == null) return;
            _busy = true;
            _dialogue = dialogue;
            Refresh();
            dialogue.DialogueEnded += OnDialogueEnded;
            dialogue.DialogueCancelled += OnDialogueCancelled;
            dialogue.StartDialogue(sequence, _ => CompleteInteraction(player));
            return;
        }
        CompleteInteraction(player);
    }

    private void OnDialogueEnded(int choice)
    {
        DisconnectDialogue();
        _busy = false;
        Refresh();
    }

    private void OnDialogueCancelled() => OnDialogueEnded(-1);

    private void DisconnectDialogue()
    {
        if (IsInstanceValid(_dialogue))
        {
            _dialogue.DialogueEnded -= OnDialogueEnded;
            _dialogue.DialogueCancelled -= OnDialogueCancelled;
        }
        _dialogue = null;
    }

    private void CompleteInteraction(Player player)
    {
        if (!IsInstanceValid(this) || !IsInsideTree() || !IsInstanceValid(player)) return;
        if (Data.GrantedItem != null && player.InventoryComponent?.AddItem(Data.GrantedItem) != true)
        {
            HUDUI.Instance?.ShowFloatingNotification("MAKE SPACE FOR THE ITEM AND TRY AGAIN", Colors.Orange);
            return;
        }
        if (!string.IsNullOrEmpty(Data.CompletionFlag)) StoryDirector.Instance?.SetFlag(Data.CompletionFlag);
        foreach (string flag in Data.AdditionalFlags)
            if (!string.IsNullOrWhiteSpace(flag)) StoryDirector.Instance?.SetFlag(flag);
        if (Data.RestoresPower) StoryDirector.Instance?.OnGeneratorActivated();
        if (!string.IsNullOrEmpty(Data.Message))
            StoryDirector.Instance?.TriggerRadioComms(Data.Speaker, Data.Message, 9.0f);
        if (Data.IsCheckpoint)
        {
            bool saved = SaveSystem.Instance?.Save(SaveSystem.Instance.CurrentSlot, player,
                GetTree().CurrentScene.SceneFilePath, StoryDirector.Instance?.CurrentAct ?? StoryAct.None) == true;
            HUDUI.Instance?.ShowFloatingNotification(saved ? "CHECKPOINT SAVED" : "SAVE FAILED — TRY AGAIN",
                saved ? Colors.Aquamarine : Colors.OrangeRed);
        }
        Refresh();
    }
}
