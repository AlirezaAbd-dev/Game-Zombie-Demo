using System;
using System.Collections.Generic;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

public class InventorySlot
{
    public ItemData Item { get; set; }
    public int Count { get; set; }
}

[GlobalClass]
public partial class InventoryComponent : Node
{
    [Signal]
    public delegate void ItemAddedEventHandler(string itemId, int count);

    [Signal]
    public delegate void ItemRemovedEventHandler(string itemId, int count);

    [Signal]
    public delegate void WeaponChangedEventHandler(WeaponData newWeapon);

    [Signal]
    public delegate void AmmoChangedEventHandler(int currentInMag, int reserve, bool isReloading);

    [Signal]
    public delegate void InventoryUpdatedEventHandler();

    [Export] public Godot.Collections.Array<WeaponData> Weapons { get; set; } = new();

    public const int MaxSlots = 8;
    public List<InventorySlot> Slots { get; private set; } = new();

    public int CurrentWeaponIndex { get; private set; } = 0;
    public WeaponData CurrentWeapon => Weapons != null && Weapons.Count > CurrentWeaponIndex ? Weapons[CurrentWeaponIndex] : null;

    public bool IsReloading { get; private set; } = false;
    private float _reloadTimer = 0.0f;

    private readonly Dictionary<string, int> _magAmmo = new();
    private readonly Dictionary<string, int> _reserveAmmo = new();
    private readonly Dictionary<string, int> _items = new();

    public int CurrentAmmo => CurrentWeapon != null ? _magAmmo.GetValueOrDefault(CurrentWeapon.WeaponId, 0) : 0;
    public int CurrentReserve => CurrentWeapon != null ? _reserveAmmo.GetValueOrDefault(CurrentWeapon.AmmoId, 0) : 0;

    public override void _Ready()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            Slots.Add(new InventorySlot());
        }

        InitializeDefaultWeapons();

        if (CurrentWeapon != null)
        {
            EmitSignal(SignalName.WeaponChanged, CurrentWeapon);
            EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, IsReloading);
        }
    }

    private void InitializeDefaultWeapons()
    {
        if (Weapons.Count == 0)
        {
            var pistol = GD.Load<WeaponData>("res://Resources/Weapons/Pistol.tres");
            var shotgun = GD.Load<WeaponData>("res://Resources/Weapons/Shotgun.tres");
            var rifle = GD.Load<WeaponData>("res://Resources/Weapons/Rifle.tres");

            if (pistol != null) Weapons.Add(pistol);
            if (shotgun != null) Weapons.Add(shotgun);
            if (rifle != null) Weapons.Add(rifle);
        }

        foreach (var weapon in Weapons)
        {
            if (weapon == null) continue;

            if (!_magAmmo.ContainsKey(weapon.WeaponId))
            {
                _magAmmo[weapon.WeaponId] = weapon.MagCapacity;
            }

            if (!_reserveAmmo.ContainsKey(weapon.AmmoId))
            {
                _reserveAmmo[weapon.AmmoId] = weapon.WeaponId switch
                {
                    "handgun" => 45,
                    "shotgun" => 12,
                    "rifle" => 10,
                    _ => weapon.MaxReserve / 2
                };
            }
        }
    }

    public void SelectWeapon(int index)
    {
        if (Weapons == null || index < 0 || index >= Weapons.Count || index == CurrentWeaponIndex)
            return;

        IsReloading = false;
        CurrentWeaponIndex = index;

        EmitSignal(SignalName.WeaponChanged, CurrentWeapon);
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, false);
    }

    public void NextWeapon()
    {
        if (Weapons == null || Weapons.Count <= 1) return;
        int next = (CurrentWeaponIndex + 1) % Weapons.Count;
        SelectWeapon(next);
    }

    public void PreviousWeapon()
    {
        if (Weapons == null || Weapons.Count <= 1) return;
        int prev = (CurrentWeaponIndex - 1 + Weapons.Count) % Weapons.Count;
        SelectWeapon(prev);
    }

    public void UpdateReload(float delta)
    {
        if (!IsReloading || CurrentWeapon == null) return;

        _reloadTimer -= delta;
        if (_reloadTimer <= 0.0f)
        {
            int current = _magAmmo.GetValueOrDefault(CurrentWeapon.WeaponId, 0);
            int needed = CurrentWeapon.MagCapacity - current;
            int available = _reserveAmmo.GetValueOrDefault(CurrentWeapon.AmmoId, 0);
            int added = Mathf.Min(needed, available);

            _magAmmo[CurrentWeapon.WeaponId] = current + added;
            _reserveAmmo[CurrentWeapon.AmmoId] = available - added;
            IsReloading = false;

            EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, false);
        }
    }

    public bool TryConsumeAmmo()
    {
        if (IsReloading || CurrentWeapon == null) return false;

        int current = _magAmmo.GetValueOrDefault(CurrentWeapon.WeaponId, 0);
        if (current <= 0) return false;

        _magAmmo[CurrentWeapon.WeaponId] = current - 1;
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, false);
        return true;
    }

    public bool StartReload()
    {
        if (IsReloading || CurrentWeapon == null) return false;

        int current = _magAmmo.GetValueOrDefault(CurrentWeapon.WeaponId, 0);
        int reserve = _reserveAmmo.GetValueOrDefault(CurrentWeapon.AmmoId, 0);

        if (current >= CurrentWeapon.MagCapacity || reserve <= 0)
            return false;

        IsReloading = true;
        _reloadTimer = CurrentWeapon.ReloadDuration;
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, true);
        return true;
    }

    public void AddReserveAmmo(string ammoId, int amount)
    {
        int max = CurrentWeapon != null && CurrentWeapon.AmmoId == ammoId ? CurrentWeapon.MaxReserve : 99;
        int current = _reserveAmmo.GetValueOrDefault(ammoId, 0);
        _reserveAmmo[ammoId] = Mathf.Min(max, current + amount);

        if (CurrentWeapon != null && CurrentWeapon.AmmoId == ammoId)
        {
            EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, IsReloading);
        }
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null) return false;

        if (item.Type == ItemType.Ammo)
        {
            AddReserveAmmo(item.Id, (int)item.Value * amount);
            return true;
        }

        // Stacking into existing slot
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].Item != null && Slots[i].Item.Id == item.Id && Slots[i].Count < item.MaxStack)
            {
                int canAdd = Mathf.Min(amount, item.MaxStack - Slots[i].Count);
                Slots[i].Count += canAdd;
                amount -= canAdd;

                _items[item.Id] = _items.GetValueOrDefault(item.Id, 0) + canAdd;
                EmitSignal(SignalName.ItemAdded, item.Id, _items[item.Id]);
                EmitSignal(SignalName.InventoryUpdated);

                if (amount <= 0) return true;
            }
        }

        // Place into first empty slot
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].Item == null)
            {
                Slots[i].Item = item;
                Slots[i].Count = amount;

                _items[item.Id] = _items.GetValueOrDefault(item.Id, 0) + amount;
                EmitSignal(SignalName.ItemAdded, item.Id, _items[item.Id]);
                EmitSignal(SignalName.InventoryUpdated);
                return true;
            }
        }

        return false; // Inventory full
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

        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].Item != null && Slots[i].Item.Id == itemId)
            {
                int take = Mathf.Min(amount, Slots[i].Count);
                Slots[i].Count -= take;
                amount -= take;

                if (Slots[i].Count <= 0)
                {
                    Slots[i].Item = null;
                    Slots[i].Count = 0;
                }

                if (amount <= 0) break;
            }
        }

        EmitSignal(SignalName.ItemRemoved, itemId, _items.GetValueOrDefault(itemId, 0));
        EmitSignal(SignalName.InventoryUpdated);
        return true;
    }

    public bool UseSlot(int slotIndex, Player player)
    {
        if (slotIndex < 0 || slotIndex >= Slots.Count || Slots[slotIndex].Item == null || player == null)
            return false;

        var item = Slots[slotIndex].Item;
        bool used = false;

        if (item.Type == ItemType.Health || item.Type == ItemType.Herb)
        {
            if (player.HealthComponent != null && player.HealthComponent.CurrentHealth < player.HealthComponent.MaxHealth)
            {
                player.Heal(item.Value);
                used = true;
            }
            else if (item.Id == "herb_blue" || item.Id == "herb_mixed")
            {
                player.StaminaComponent?.Regenerate(item.Value);
                used = true;
            }
        }
        else if (item.Type == ItemType.Booster)
        {
            player.ApplyAdrenalineBoost(15.0f);
            used = true;
        }

        if (used)
        {
            ConsumeItem(item.Id, 1);
            AudioManager.Instance?.PlaySound(SoundType.ItemUnlock);
            return true;
        }

        return false;
    }

    public bool CombineSlots(int slotA, int slotB)
    {
        if (slotA < 0 || slotA >= Slots.Count || slotB < 0 || slotB >= Slots.Count || slotA == slotB)
            return false;

        var itemA = Slots[slotA].Item;
        var itemB = Slots[slotB].Item;

        if (itemA == null || itemB == null)
            return false;

        if (itemA.CombineWithId == itemB.Id && !string.IsNullOrEmpty(itemA.CombineResultId))
        {
            string resultPath = $"res://Resources/Items/{itemA.CombineResultId}.tres";
            if (!ResourceLoader.Exists(resultPath))
            {
                resultPath = itemA.CombineResultId switch
                {
                    "herb_mixed" => "res://Resources/Items/Herb_Mixed.tres",
                    _ => resultPath
                };
            }

            var resultItem = GD.Load<ItemData>(resultPath);
            if (resultItem != null)
            {
                ConsumeItem(itemA.Id, 1);
                ConsumeItem(itemB.Id, 1);
                AddItem(resultItem, 1);
                AudioManager.Instance?.PlaySound(SoundType.PickupCollect);
                return true;
            }
        }

        return false;
    }
}
