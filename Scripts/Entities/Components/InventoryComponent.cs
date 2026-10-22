using System;
using System.Collections.Generic;
using Godot;
using TopDownDemo.Core;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class InventoryComponent : Node
{
    [Signal]
    public delegate void ItemAddedEventHandler(string itemId, int count);

    [Signal]
    public delegate void ItemRemovedEventHandler(string itemId, int count);

    [Signal]
    public delegate void AmmoChangedEventHandler(int currentInMag, int reserve, bool isReloading);

    [Export] public int MagCapacity { get; set; } = 15;
    [Export] public int MaxReserveAmmo { get; set; } = 90;
    [Export] public float ReloadDuration { get; set; } = 1.2f;

    public int CurrentAmmo { get; private set; } = 15;
    public int ReserveAmmo { get; private set; } = 45;
    public bool IsReloading { get; private set; } = false;

    private readonly Dictionary<string, int> _items = new();
    private float _reloadTimer = 0.0f;

    public override void _Ready()
    {
        CurrentAmmo = MagCapacity;
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, IsReloading);
    }

    public void UpdateReload(float delta)
    {
        if (!IsReloading) return;

        _reloadTimer -= delta;
        if (_reloadTimer <= 0.0f)
        {
            int needed = MagCapacity - CurrentAmmo;
            int added = Mathf.Min(needed, ReserveAmmo);
            CurrentAmmo += added;
            ReserveAmmo -= added;
            IsReloading = false;

            EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, false);
        }
    }

    public bool TryConsumeAmmo()
    {
        if (IsReloading || CurrentAmmo <= 0) return false;

        CurrentAmmo--;
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, false);
        return true;
    }

    public bool StartReload()
    {
        if (IsReloading || CurrentAmmo >= MagCapacity || ReserveAmmo <= 0)
            return false;

        IsReloading = true;
        _reloadTimer = ReloadDuration;
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, true);
        return true;
    }

    public void AddReserveAmmo(int amount)
    {
        ReserveAmmo = Mathf.Min(MaxReserveAmmo, ReserveAmmo + amount);
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, IsReloading);
    }

    public void AddItem(ItemData item, int amount = 1)
    {
        if (item == null) return;

        if (item.Type == ItemType.Ammo)
        {
            AddReserveAmmo((int)item.Value * amount);
            return;
        }

        if (_items.ContainsKey(item.Id))
        {
            _items[item.Id] += amount;
        }
        else
        {
            _items[item.Id] = amount;
        }

        EmitSignal(SignalName.ItemAdded, item.Id, _items[item.Id]);
    }

    public bool HasItem(string itemId) => _items.ContainsKey(itemId) && _items[itemId] > 0;

    public bool ConsumeItem(string itemId, int amount = 1)
    {
        if (!HasItem(itemId) || _items[itemId] < amount) return false;

        _items[itemId] -= amount;
        if (_items[itemId] <= 0)
        {
            _items.Remove(itemId);
        }

        EmitSignal(SignalName.ItemRemoved, itemId, _items.GetValueOrDefault(itemId, 0));
        return true;
    }
}
