using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class InventoryUI : Control
{
    [ExportGroup("UI Nodes")]
    [Export] public GridContainer SlotGrid { get; set; }
    [Export] public Label ItemNameLabel { get; set; }
    [Export] public Label ItemDescLabel { get; set; }
    [Export] public Label EcgStatusLabel { get; set; }
    [Export] public ColorRect EcgPulseLine { get; set; }
    [Export] public Button UseButton { get; set; }
    [Export] public Button CombineButton { get; set; }
    [Export] public Button CloseButton { get; set; }
    [Export] public Label HintLabel { get; set; }
    [Export] public ItemList MissionItemList { get; set; }
    private readonly System.Collections.Generic.List<ItemData> _missionItems = new();

    private Player _player;
    private int _selectedSlotIndex = -1;
    private int _combiningSourceIndex = -1;
    private float _ecgPhase = 0.0f;

    private readonly Button[] _slotButtons = new Button[InventoryComponent.MaxSlots];
    private readonly TextureRect[] _slotIcons = new TextureRect[InventoryComponent.MaxSlots];
    private readonly Label[] _slotCountLabels = new Label[InventoryComponent.MaxSlots];

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        SlotGrid ??= GetNodeOrNull<GridContainer>("%SlotGrid");
        ItemNameLabel ??= GetNodeOrNull<Label>("%ItemNameLabel");
        ItemDescLabel ??= GetNodeOrNull<Label>("%ItemDescLabel");
        EcgStatusLabel ??= GetNodeOrNull<Label>("%EcgStatusLabel");
        EcgPulseLine ??= GetNodeOrNull<ColorRect>("%EcgPulseLine");
        UseButton ??= GetNodeOrNull<Button>("%UseButton");
        CombineButton ??= GetNodeOrNull<Button>("%CombineButton");
        CloseButton ??= GetNodeOrNull<Button>("%CloseButton");
        HintLabel ??= GetNodeOrNull<Label>("%HintLabel");
        MissionItemList ??= GetNodeOrNull<ItemList>("%MissionItemList");
        if (MissionItemList != null) MissionItemList.ItemSelected += SelectMissionItem;
        if (ItemDescLabel != null) ItemDescLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        if (UseButton != null) UseButton.Pressed += OnUsePressed;
        if (CombineButton != null) CombineButton.Pressed += OnCombinePressed;
        if (CloseButton != null) CloseButton.Pressed += ToggleInventory;

        CreateSlotUI();
        CallDeferred(nameof(InitPlayer));
    }

    private void InitPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player?.InventoryComponent != null)
        {
            _player.InventoryComponent.InventoryUpdated += RefreshSlots;
        }
        RefreshSlots();
    }

    private void CreateSlotUI()
    {
        if (SlotGrid == null) return;

        // Clear children
        foreach (Node child in SlotGrid.GetChildren())
        {
            child.QueueFree();
        }

        for (int i = 0; i < InventoryComponent.MaxSlots; i++)
        {
            int index = i;
            var btn = new Button
            {
                CustomMinimumSize = new Vector2(80, 80),
                ToggleMode = false,
                Text = ""
            };

            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                MouseFilter = MouseFilterEnum.Ignore
            };

            var countLbl = new Label
            {
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                OffsetRight = -4,
                OffsetBottom = -4,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Text = "",
                MouseFilter = MouseFilterEnum.Ignore
            };
            countLbl.AddThemeColorOverride("font_color", Colors.Yellow);

            btn.AddChild(icon);
            btn.AddChild(countLbl);
            btn.Pressed += () => OnSlotClicked(index);

            SlotGrid.AddChild(btn);
            _slotButtons[i] = btn;
            _slotIcons[i] = icon;
            _slotCountLabels[i] = countLbl;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_inventory") && !@event.IsEcho())
        {
            ToggleInventory();
            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleInventory()
    {
        if (!Visible && GetTree().Paused) return;
        Visible = !Visible;
        if (!Visible) ControlBindings.ReleaseGameplayActions();
        GetTree().Paused = Visible;
        Input.MouseMode = Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;

        if (Visible)
        {
            if (CloseButton != null) CloseButton.Text = $"CLOSE ({ControlBindings.Hint("toggle_inventory")})";
            _combiningSourceIndex = -1;
            RefreshSlots();
            SelectSlot(0);
            AudioManager.Instance?.PlaySound(SoundType.ItemUnlock);
        }
    }

    private void RefreshSlots()
    {
        if (_player?.InventoryComponent == null) return;
        _missionItems.Clear();
        MissionItemList?.Clear();
        foreach (var entry in _player.InventoryComponent.MissionItems.Values)
        {
            _missionItems.Add(entry.Item);
            MissionItemList?.AddItem(entry.Item.ItemName);
        }
        var slots = _player.InventoryComponent.Slots;

        for (int i = 0; i < InventoryComponent.MaxSlots && i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.Item != null && _slotIcons[i] != null)
            {
                _slotIcons[i].Texture = slot.Item.Icon;
                _slotIcons[i].Visible = true;
                _slotCountLabels[i].Text = slot.Count > 1 ? slot.Count.ToString() : "";
            }
            else
            {
                if (_slotIcons[i] != null) _slotIcons[i].Visible = false;
                if (_slotCountLabels[i] != null) _slotCountLabels[i].Text = "";
            }
        }

        UpdateEcgMonitor();
    }

    private void SelectMissionItem(long index)
    {
        if (index < 0 || index >= _missionItems.Count) return;
        _selectedSlotIndex = -1;
        _combiningSourceIndex = -1;
        var item = _missionItems[(int)index];
        if (ItemNameLabel != null) ItemNameLabel.Text = item.ItemName.ToUpper();
        if (ItemDescLabel != null) ItemDescLabel.Text = item.Description;
        if (UseButton != null) UseButton.Visible = false;
        if (CombineButton != null) CombineButton.Visible = false;
    }

    private void SelectSlot(int index)
    {
        _selectedSlotIndex = index;
        if (_player?.InventoryComponent == null) return;

        var slots = _player.InventoryComponent.Slots;
        if (index >= 0 && index < slots.Count && slots[index].Item != null)
        {
            var item = slots[index].Item;
            if (ItemNameLabel != null) ItemNameLabel.Text = item.ItemName.ToUpper();
            if (ItemDescLabel != null) ItemDescLabel.Text = item.Description;

            if (UseButton != null)
            {
                UseButton.Visible = item.Type == ItemType.Health || item.Type == ItemType.Herb || item.Type == ItemType.Booster;
            }

            if (CombineButton != null)
            {
                CombineButton.Visible = !string.IsNullOrEmpty(item.CombineWithId);
            }
        }
        else
        {
            if (ItemNameLabel != null) ItemNameLabel.Text = "EMPTY SLOT";
            if (ItemDescLabel != null) ItemDescLabel.Text = "No item selected.";
            if (UseButton != null) UseButton.Visible = false;
            if (CombineButton != null) CombineButton.Visible = false;
        }

        // Highlight selected button
        for (int i = 0; i < _slotButtons.Length; i++)
        {
            if (_slotButtons[i] != null)
            {
                _slotButtons[i].Modulate = (i == _selectedSlotIndex) ? new Color(0.2f, 1.0f, 0.5f, 1.0f) : Colors.White;
            }
        }
    }

    private void OnSlotClicked(int index)
    {
        if (_combiningSourceIndex >= 0)
        {
            // Complete combination
            bool success = _player?.InventoryComponent?.CombineSlots(_combiningSourceIndex, index) ?? false;
            _combiningSourceIndex = -1;
            if (HintLabel != null) HintLabel.Text = success ? "ITEMS COMBINED SUCCESSFULLY!" : "CHECK INGREDIENTS AND MAKE ROOM FOR THE RESULT.";
            RefreshSlots();
            SelectSlot(index);
            return;
        }

        SelectSlot(index);
    }

    private void OnUsePressed()
    {
        if (_selectedSlotIndex < 0 || _player == null) return;

        var slot = _player.InventoryComponent?.Slots != null && _selectedSlotIndex < _player.InventoryComponent.Slots.Count
            ? _player.InventoryComponent.Slots[_selectedSlotIndex]
            : null;
        var item = slot?.Item;

        bool used = _player.InventoryComponent?.UseSlot(_selectedSlotIndex, _player) ?? false;
        if (used)
        {
            if (item != null && item.Type == ItemType.Key)
            {
                if (HintLabel != null) HintLabel.Text = "KEYCARD USED! BLAST DOOR UNLOCKED.";
                var timer = GetTree().CreateTimer(0.7f);
                timer.Timeout += ToggleInventory;
            }
            else
            {
                if (HintLabel != null) HintLabel.Text = "ITEM USED.";
            }

            RefreshSlots();
            SelectSlot(_selectedSlotIndex);
        }
        else
        {
            if (item != null && item.Type == ItemType.Key)
            {
                if (HintLabel != null) HintLabel.Text = "CANNOT USE THIS HERE. STAND IN FRONT OF THE BLAST DOOR.";
            }
            else
            {
                if (HintLabel != null) HintLabel.Text = "CANNOT USE ITEM RIGHT NOW.";
            }
        }
    }

    private void OnCombinePressed()
    {
        if (_selectedSlotIndex < 0) return;
        _combiningSourceIndex = _selectedSlotIndex;
        if (HintLabel != null) HintLabel.Text = "SELECT A TARGET ITEM, OR THIS STACK AGAIN TO USE TWO.";
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        // ECG monitor pulse
        _ecgPhase += (float)delta * 5.0f;
        if (EcgPulseLine != null)
        {
            float pulse = Mathf.Sin(_ecgPhase) * 12.0f;
            EcgPulseLine.Position = new Vector2(EcgPulseLine.Position.X, 40 + pulse);
        }
    }

    private void UpdateEcgMonitor()
    {
        if (_player?.HealthComponent == null) return;

        float hpPercent = _player.HealthComponent.CurrentHealth / _player.HealthComponent.MaxHealth;
        Color ecgColor;
        string statusText;

        if (hpPercent > 0.65f)
        {
            statusText = "CONDITION: FINE";
            ecgColor = new Color(0.2f, 1.0f, 0.35f, 1.0f);
        }
        else if (hpPercent > 0.30f)
        {
            statusText = "CONDITION: CAUTION";
            ecgColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);
        }
        else
        {
            statusText = "CONDITION: DANGER";
            ecgColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);
        }

        if (EcgStatusLabel != null)
        {
            EcgStatusLabel.Text = statusText;
            EcgStatusLabel.Modulate = ecgColor;
        }

        if (EcgPulseLine != null)
        {
            EcgPulseLine.Color = ecgColor;
        }
    }
}
