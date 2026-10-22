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
    public delegate void WeaponChangedEventHandler(WeaponData newWeapon);

    [Signal]
    public delegate void AmmoChangedEventHandler(int currentInMag, int reserve, bool isReloading);

    [Export] public Godot.Collections.Array<WeaponData> Weapons { get; set; } = new();

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
                // Initial starting ammo
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

    public void AddItem(ItemData item, int amount = 1)
    {
        if (item == null) return;

        if (item.Type == ItemType.Ammo)
        {
            AddReserveAmmo(item.Id, (int)item.Value * amount);
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
