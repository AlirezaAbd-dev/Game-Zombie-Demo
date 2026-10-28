using System;
using System.Collections.Generic;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;
using TopDownDemo.UI;

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
    [Signal] public delegate void WeaponSelectionDeniedEventHandler(string weaponName);

    [Export] public Godot.Collections.Array<WeaponData> Weapons { get; set; } = new();

    public const int MaxSlots = 8;
    public List<InventorySlot> Slots { get; private set; } = new();

    public int CurrentWeaponIndex { get; private set; } = 0;
    public WeaponData CurrentWeapon => Weapons != null && Weapons.Count > CurrentWeaponIndex ? Weapons[CurrentWeaponIndex] : null;

    public bool IsReloading { get; private set; } = false;
    private float _reloadTimer;
    private float _phaseDuration;
    public ReloadPhase CurrentReloadPhase { get; private set; }
    public float ReloadPhaseProgress => IsReloading ? Mathf.Clamp(1f - _reloadTimer / _phaseDuration, 0, 1) : 0;
    public event Action<ReloadPhase> ReloadPhaseChanged;

    public void CancelReload()
    {
        if (!IsReloading) return;
        IsReloading = false;
        CurrentReloadPhase = ReloadPhase.Idle;
        _reloadTimer = 0;
        ReloadPhaseChanged?.Invoke(ReloadPhase.Idle);
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, false);
    }

    private void BeginReloadPhase(ReloadPhase phase, float duration)
    {
        CurrentReloadPhase = phase;
        _phaseDuration = Mathf.Max(0.01f, duration);
        _reloadTimer = _phaseDuration;
        ReloadPhaseChanged?.Invoke(phase);
    }

    private readonly Dictionary<string, int> _magAmmo = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _reserveAmmo = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InventorySlot> _missionItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unlockedWeaponIds = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> UnlockedWeaponIds => _unlockedWeaponIds;
    public IReadOnlyDictionary<string, InventorySlot> MissionItems => _missionItems;

    public int CurrentAmmo => CurrentWeapon != null ? _magAmmo.GetValueOrDefault(CurrentWeapon.WeaponId, 0) : 0;
    public int CurrentReserve => CurrentWeapon != null ? _reserveAmmo.GetValueOrDefault(CurrentWeapon.AmmoId, 0) : 0;

    public override void _Ready()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            Slots.Add(new InventorySlot());
        }

        InitializeDefaultWeapons();
        var session = SaveSystem.Instance;
        SetUnlockedWeapons(session?.HasActiveSession == true ? session.ActiveSaveData.UnlockedWeaponIds : null);
        if (session?.HasActiveSession == true)
            SetAmmoState(session.ActiveSaveData.MagAmmo,session.ActiveSaveData.ReserveAmmo,session.ActiveSaveData.EquippedWeaponIndex);

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
        if (!IsWeaponUnlocked(Weapons[index].WeaponId))
        {
            EmitSignal(SignalName.WeaponSelectionDenied,Weapons[index].WeaponName);
            return;
        }

        CancelReload();
        CurrentWeaponIndex = index;

        EmitSignal(SignalName.WeaponChanged, CurrentWeapon);
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, false);
    }

    public void NextWeapon()
    {
        if (Weapons == null || Weapons.Count <= 1) return;
        CycleWeapon(1);
    }

    public void PreviousWeapon()
    {
        if (Weapons == null || Weapons.Count <= 1) return;
        CycleWeapon(-1);
    }

    private void CycleWeapon(int direction)
    {
        for (int step = 1; step < Weapons.Count; step++)
        {
            int index = (CurrentWeaponIndex + direction * step + Weapons.Count) % Weapons.Count;
            if (IsWeaponUnlocked(Weapons[index].WeaponId)) { SelectWeapon(index); return; }
        }
    }

    public bool IsWeaponUnlocked(string id) => !string.IsNullOrWhiteSpace(id) && _unlockedWeaponIds.Contains(id);

    public void SetUnlockedWeapons(IEnumerable<string> ids)
    {
        CancelReload();
        var requested = ids == null ? null : new HashSet<string>(ids,StringComparer.OrdinalIgnoreCase);
        _unlockedWeaponIds.Clear();
        foreach (var weapon in Weapons)
        {
            if (weapon == null) continue;
            if (requested == null || requested.Contains(weapon.WeaponId)) _unlockedWeaponIds.Add(weapon.WeaponId);
        }
        if (_unlockedWeaponIds.Count == 0 && Weapons.Count > 0) _unlockedWeaponIds.Add(Weapons[0].WeaponId);
        if (CurrentWeapon == null || !IsWeaponUnlocked(CurrentWeapon.WeaponId))
            for (int i = 0; i < Weapons.Count; i++)
                if (IsWeaponUnlocked(Weapons[i].WeaponId)) { CurrentWeaponIndex = i; break; }
        if (CurrentWeapon != null)
        {
            EmitSignal(SignalName.WeaponChanged,CurrentWeapon);
            EmitSignal(SignalName.AmmoChanged,CurrentAmmo,CurrentReserve,IsReloading);
        }
        EmitSignal(SignalName.InventoryUpdated);
    }

    public bool AcquireWeapon(string id)
    {
        foreach (var weapon in Weapons)
        {
            if (weapon == null || !string.Equals(weapon.WeaponId,id,StringComparison.OrdinalIgnoreCase)) continue;
            if (!_unlockedWeaponIds.Add(weapon.WeaponId)) return false;
            _magAmmo[weapon.WeaponId] = Mathf.Max(_magAmmo.GetValueOrDefault(weapon.WeaponId,0),weapon.MagCapacity);
            EmitSignal(SignalName.InventoryUpdated);
            return true;
        }
        return false;
    }

    public void UpdateReload(float delta)
    {
        if (!IsReloading || CurrentWeapon == null || delta <= 0) return;
        // Carry elapsed time across stages so low frame rates cannot skip or duplicate ammunition.
        while (IsReloading && delta >= _reloadTimer)
        {
            delta -= _reloadTimer;
            var weapon = CurrentWeapon;
            bool shells = weapon.ReloadStyle == ReloadStyle.IndividualShells;
            switch (CurrentReloadPhase)
            {
                case ReloadPhase.Open:
                    BeginReloadPhase(ReloadPhase.Insert, shells ? weapon.ShellInsertDuration : weapon.ReloadDuration * 0.5f);
                    break;
                case ReloadPhase.Insert:
                    if (shells)
                    {
                        TransferReloadAmmo(1);
                        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, true);
                    }
                    if (shells && CurrentAmmo < weapon.MagCapacity && CurrentReserve > 0)
                        BeginReloadPhase(ReloadPhase.Insert, weapon.ShellInsertDuration);
                    else
                        BeginReloadPhase(ReloadPhase.Close, weapon.ReloadDuration * (shells ? 0.55f : 0.25f));
                    break;
                case ReloadPhase.Close:
                    if (!shells) TransferReloadAmmo(weapon.MagCapacity);
                    CancelReload();
                    break;
                default:
                    CancelReload();
                    break;
            }
        }
        if (IsReloading) _reloadTimer -= delta;
    }

    private void TransferReloadAmmo(int limit)
    {
        int added = Mathf.Min(limit, Mathf.Min(CurrentWeapon.MagCapacity - CurrentAmmo, CurrentReserve));
        _magAmmo[CurrentWeapon.WeaponId] = CurrentAmmo + added;
        _reserveAmmo[CurrentWeapon.AmmoId] = CurrentReserve - added;
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
        BeginReloadPhase(ReloadPhase.Open, CurrentWeapon.ReloadDuration * (CurrentWeapon.ReloadStyle == ReloadStyle.IndividualShells ? 0.45f : 0.25f));
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, true);
        return true;
    }

    public int AddReserveAmmo(string ammoId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(ammoId)) return 0;
        int max = GetMaxReserve(ammoId);
        int current = _reserveAmmo.GetValueOrDefault(ammoId, 0);
        int accepted = (int)Math.Min(amount,Math.Max(0,(long)max - current));
        if (accepted == 0) return 0;
        _reserveAmmo[ammoId] = current + accepted;

        if (CurrentWeapon != null && string.Equals(CurrentWeapon.AmmoId,ammoId,StringComparison.OrdinalIgnoreCase))
        {
            EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, IsReloading);
        }
        return accepted;
    }

    private int GetMaxReserve(string ammoId)
    {
        foreach (var weapon in Weapons)
            if (weapon != null && string.Equals(weapon.AmmoId,ammoId,StringComparison.OrdinalIgnoreCase)) return weapon.MaxReserve;
        return 0;
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0 || string.IsNullOrWhiteSpace(item.Id)) return false;
        if (item.Type == ItemType.Weapon)
            return amount == 1 && item.GrantedWeapon != null && (AcquireWeapon(item.GrantedWeapon.WeaponId) || IsWeaponUnlocked(item.GrantedWeapon.WeaponId));

        // Mission items cannot be blocked by the consumable inventory capacity.
        if (item.Type == ItemType.Key || item.Type == ItemType.Document)
        {
            _items[item.Id] = _items.GetValueOrDefault(item.Id, 0) + amount;
            _missionItems[item.Id] = new InventorySlot { Item = item, Count = _items[item.Id] };
            EmitSignal(SignalName.ItemAdded, item.Id, _items[item.Id]);
            EmitSignal(SignalName.InventoryUpdated);
            return true;
        }

        if (item.Type == ItemType.Ammo)
        {
            if (!float.IsFinite(item.Value) || item.Value < 1 || item.Value >= int.MaxValue) return false;
            long rounds = (long)(int)item.Value * amount;
            long ammoCapacity = (long)GetMaxReserve(item.Id) - _reserveAmmo.GetValueOrDefault(item.Id,0);
            if (rounds > ammoCapacity) return false;
            AddReserveAmmo(item.Id,(int)rounds);
            return true;
        }

        if (item.MaxStack <= 0) return false;
        long capacity = 0;
        foreach (var slot in Slots)
            if (slot.Item == null) capacity += item.MaxStack;
            else if (string.Equals(slot.Item.Id,item.Id,StringComparison.OrdinalIgnoreCase))
                capacity += Mathf.Max(0,item.MaxStack - slot.Count);
        // Reject the whole operation before changing any stack.
        if (capacity < amount) return false;
        int added = amount;
        foreach (var slot in Slots)
        {
            if (slot.Item != null && string.Equals(slot.Item.Id,item.Id,StringComparison.OrdinalIgnoreCase))
            {
                int count = Mathf.Min(amount,Mathf.Max(0,item.MaxStack - slot.Count));
                slot.Count += count; amount -= count;
            }
            if (amount == 0) break;
        }
        foreach (var slot in Slots)
        {
            if (amount == 0) break;
            if (slot.Item != null) continue;
            int count = Mathf.Min(amount,item.MaxStack);
            slot.Item = item; slot.Count = count; amount -= count;
        }
        _items[item.Id] = _items.GetValueOrDefault(item.Id,0) + added;
        EmitSignal(SignalName.ItemAdded,item.Id,_items[item.Id]);
        EmitSignal(SignalName.InventoryUpdated);
        return true;
    }

    public bool HasItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        // Check slots directly first
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].Item != null && string.Equals(Slots[i].Item.Id, itemId, StringComparison.OrdinalIgnoreCase) && Slots[i].Count > 0)
            {
                return true;
            }
        }

        // Fallback check against dictionary
        foreach (var kvp in _items)
        {
            if (string.Equals(kvp.Key, itemId, StringComparison.OrdinalIgnoreCase) && kvp.Value > 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool ConsumeItem(string itemId, int amount = 1)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId) || !_items.TryGetValue(itemId,out int count) || count < amount) return false;

        _items[itemId] -= amount;
        if (_items[itemId] <= 0)
        {
            _items.Remove(itemId);
        }
        if (_missionItems.TryGetValue(itemId, out var missionItem))
        {
            missionItem.Count -= amount;
            if (missionItem.Count <= 0) _missionItems.Remove(itemId);
        }

        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].Item != null && string.Equals(Slots[i].Item.Id,itemId,StringComparison.OrdinalIgnoreCase))
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
        if (slotIndex < 0 || slotIndex >= Slots.Count || Slots[slotIndex].Item == null || player == null || player.IsDead)
            return false;

        var item = Slots[slotIndex].Item;
        bool used = false;

        if (item.Type == ItemType.Health || item.Type == ItemType.Herb)
        {
            used = TryApplyRestorative(item,player);
        }
        else if (item.Type == ItemType.Booster)
        {
            player.ApplyAdrenalineBoost(15.0f);
            player.Heal(item.Value);
            used = true;
            HUDUI.Instance?.ShowHealNotification(item.ItemName, item.Value, "ADRENALINE BOOST (15s)");
            AudioManager.Instance?.PlaySound(SoundType.MedicalHeal, player.GlobalPosition);
        }
        else if (item.Type == ItemType.Key)
        {
            // Check if player is near any blast door requiring this key
            var doors = player.GetTree()?.GetNodesInGroup("doors");
            if (doors != null)
            {
                foreach (var node in doors)
                {
                    if (node is BlastDoor door && !door.IsOpen)
                    {
                        if (string.Equals(door.RequiredItemId, item.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            float dist = player.GlobalPosition.DistanceTo(door.GlobalPosition);
                            if (dist <= 180.0f)
                            {
                                door.OpenDoor();
                                used = true;
                                break;
                            }
                        }
                    }
                }
            }

            // Also check interactables
            if (!used)
            {
                var interactables = player.GetTree()?.GetNodesInGroup("interactables");
                if (interactables != null)
                {
                    foreach (var node in interactables)
                    {
                        if (node is InteractableComponent interactable && interactable.CanInteract(player))
                        {
                            if (string.Equals(interactable.RequiredItemId, item.Id, StringComparison.OrdinalIgnoreCase))
                            {
                                float dist = player.GlobalPosition.DistanceTo(interactable.GlobalPosition);
                                if (dist <= 180.0f)
                                {
                                    interactable.TryInteract(player);
                                    used = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (used)
            {
                AudioManager.Instance?.PlaySound(SoundType.ItemUnlock, player.GlobalPosition);
                return true;
            }
        }

        if (used)
        {
            ConsumeItem(item.Id, 1);
            AudioManager.Instance?.PlaySound(SoundType.ItemUnlock);
            return true;
        }

        return false;
    }

    public static bool TryApplyRestorative(ItemData item, Player player)
    {
        if (item == null || player == null || player.IsDead
            || (item.Type != ItemType.Health && item.Type != ItemType.Herb)) return false;
        float hp = player.HealthComponent == null ? 0
            : Mathf.Min(Mathf.Max(0,item.Value),player.HealthComponent.MaxHealth - player.HealthComponent.CurrentHealth);
        float stamina = player.StaminaComponent?.Restore(item.StaminaRestore) ?? 0;
        if (hp <= 0 && stamina <= 0) return false;
        if (hp > 0) player.Heal(hp);
        else AudioManager.Instance?.PlaySound(SoundType.MedicalHeal,player.GlobalPosition);
        HUDUI.Instance?.ShowHealNotification(item.ItemName,hp,stamina > 0 ? $"+{stamina:0} STAMINA" : "");
        return true;
    }

    public bool QuickHeal(Player player)
    {
        if (player == null || player.IsDead || player.HealthComponent == null)
            return false;

        float currentHp = player.HealthComponent.CurrentHealth;
        float maxHp = player.HealthComponent.MaxHealth;
        float missingHp = maxHp - currentHp;

        if (missingHp <= 0.0f)
            return false;

        int bestSlotIndex = -1;
        float bestFitDifference = float.MaxValue;

        // Find the healing item whose heal amount best matches missing HP without excess waste
        for (int i = 0; i < Slots.Count; i++)
        {
            var item = Slots[i].Item;
            if (item == null) continue;

            if ((item.Type == ItemType.Health || item.Type == ItemType.Herb) && item.Value > 0)
            {
                float diff = Mathf.Abs(missingHp - item.Value);
                if (diff < bestFitDifference)
                {
                    bestFitDifference = diff;
                    bestSlotIndex = i;
                }
            }
        }

        if (bestSlotIndex >= 0)
        {
            return UseSlot(bestSlotIndex, player);
        }

        return false;
    }

    public bool CombineSlots(int slotA, int slotB)
    {
        if (slotA < 0 || slotA >= Slots.Count || slotB < 0 || slotB >= Slots.Count
            || (slotA == slotB && Slots[slotA].Count < 2))
            return false;

        var itemA = Slots[slotA].Item;
        var itemB = Slots[slotB].Item;

        if (itemA == null || itemB == null)
            return false;

        string idA = itemA.Id.ToLower();
        string idB = itemB.Id.ToLower();
        string resultPath = null;

        // Recipe 1: Green Herb + Green Herb -> Double Green Herb (+70 HP)
        if (idA == "herb_green" && idB == "herb_green")
        {
            resultPath = "res://Resources/Items/Herb_Green_Double.tres";
        }
        // Recipe 2: Green Herb + Blue Herb -> Mixed Herb G+B (+60 HP & 100 Stamina)
        else if ((idA == "herb_green" && idB == "herb_blue") || (idA == "herb_blue" && idB == "herb_green"))
        {
            resultPath = "res://Resources/Items/Herb_Mixed.tres";
        }
        // Recipe 3: Double Green Herb + Green Herb -> First Aid Spray (G+G+G = Full 100 HP Heal!)
        else if ((idA == "herb_green_double" && idB == "herb_green") || (idA == "herb_green" && idB == "herb_green_double"))
        {
            resultPath = "res://Resources/Items/TraumaKit.tres";
        }
        // General fallback to ItemData Combine properties
        else if (itemA.CombineWithId == itemB.Id && !string.IsNullOrEmpty(itemA.CombineResultId))
        {
            resultPath = $"res://Resources/Items/{itemA.CombineResultId}.tres";
        }

        if (!string.IsNullOrEmpty(resultPath) && ResourceLoader.Exists(resultPath))
        {
            var resultItem = GD.Load<ItemData>(resultPath);
            if (resultItem != null)
            {
                int capacity = 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    int remaining = Slots[i].Count - (i == slotA ? 1 : 0) - (i == slotB ? 1 : 0);
                    if (Slots[i].Item == null || remaining <= 0) capacity += resultItem.MaxStack;
                    else if (Slots[i].Item.Id == resultItem.Id) capacity += Mathf.Max(0,resultItem.MaxStack - remaining);
                }
                if (capacity < 1 || resultItem.MaxStack <= 0) return false;
                RemoveCraftIngredient(slotA);
                RemoveCraftIngredient(slotB);
                AddItem(resultItem,1); // Capacity has been checked after consuming the selected units.
                EmitSignal(SignalName.ItemRemoved,itemA.Id,_items.GetValueOrDefault(itemA.Id,0));
                EmitSignal(SignalName.ItemRemoved,itemB.Id,_items.GetValueOrDefault(itemB.Id,0));
                AudioManager.Instance?.PlaySound(SoundType.PickupCollect);
                return true;
            }
        }

        return false;
    }

    private void RemoveCraftIngredient(int index)
    {
        var slot = Slots[index];
        string id = slot.Item.Id;
        slot.Count--;
        if (slot.Count == 0) slot.Item = null;
        _items[id]--;
        if (_items[id] == 0) _items.Remove(id);
    }

    public IReadOnlyList<InventorySlot> GetSlots() => Slots;
    public IReadOnlyDictionary<string, int> GetMagAmmo() => _magAmmo;
    public IReadOnlyDictionary<string, int> GetReserveAmmo() => _reserveAmmo;

    public void SetAmmoState(Dictionary<string, int> magAmmo, Dictionary<string, int> reserveAmmo, int weaponIndex = 0)
    {
        CancelReload();
        _magAmmo.Clear();
        _reserveAmmo.Clear();
        if (magAmmo != null)
        {
            foreach (var kvp in magAmmo)
            {
                _magAmmo[kvp.Key] = kvp.Value;
            }
        }
        if (reserveAmmo != null)
        {
            foreach (var kvp in reserveAmmo)
            {
                _reserveAmmo[kvp.Key] = kvp.Value;
            }
        }
        foreach (var weapon in Weapons)
        {
            if (weapon == null) continue;
            int savedMagazine = Mathf.Max(0,_magAmmo.GetValueOrDefault(weapon.WeaponId,0));
            long reserve = (long)Mathf.Max(0,_reserveAmmo.GetValueOrDefault(weapon.AmmoId,0))
                + Mathf.Max(0,savedMagazine - weapon.MagCapacity);
            _magAmmo[weapon.WeaponId] = Mathf.Min(savedMagazine,weapon.MagCapacity);
            _reserveAmmo[weapon.AmmoId] = (int)Math.Min(reserve,weapon.MaxReserve);
        }
        if (weaponIndex >= 0 && weaponIndex < Weapons.Count && IsWeaponUnlocked(Weapons[weaponIndex].WeaponId))
        {
            CurrentWeaponIndex = weaponIndex;
        }

        if (CurrentWeapon != null)
        {
            EmitSignal(SignalName.WeaponChanged, CurrentWeapon);
            EmitSignal(SignalName.AmmoChanged, CurrentAmmo, CurrentReserve, IsReloading);
        }
    }

    public void ClearInventory()
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            Slots[i].Item = null;
            Slots[i].Count = 0;
        }
        _items.Clear();
        _missionItems.Clear();
        EmitSignal(SignalName.InventoryUpdated);
    }
}
