using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;

namespace TopDownDemo.Systems.Tests;

public partial class WeaponSmokeTest : Node2D
{
    private int _checks;
    private SceneTree Tree => GetTree();
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private async void Run()
    {
        try
        {
            RenderingServer.SetDefaultClearColor(new Color("111b22"));
            var prefab = GD.Load<PackedScene>("res://Prefabs/Player.tscn");
            var actors = new Player[3];
            Title("BLACKSITE ZERO  /  FIELD EQUIPMENT", new Vector2(65, 45), 30);
            Title("OPERATIVE REDESIGN     •     WEAPON HANDLING", new Vector2(65, 94), 16);
            for (int i = 0; i < 3; i++)
            {
                var actor = prefab.Instantiate<Player>();
                AddChild(actor);
                actor.SetProcess(false); actor.SetPhysicsProcess(false); actor.Camera.Enabled = false;
                actor.Position = new Vector2(230 + i * 400, 310);
                actor.Scale = Vector2.One * 2;
                foreach (var light in actor.GetChildren().OfType<PointLight2D>()) light.Enabled = false;
                actors[i] = actor;
                var inventory = actor.InventoryComponent;
                inventory.SetUnlockedWeapons(null);
                inventory.SelectWeapon(i);
                var weapon = inventory.CurrentWeapon;
                var view = actor.GetNode<WeaponPresentation>("%WeaponPresentation");
                Check(actor.BodySprite.Texture.ResourcePath.EndsWith("operative.svg") && actor.GunSprite.Texture == weapon.WeaponSprite, "authored character and weapon art binds: " + weapon.WeaponId);
                Check(weapon.ReloadOpenSound != null && weapon.ReloadInsertSound != null && weapon.ReloadCloseSound != null,
                    "all three reload sounds import: " + weapon.WeaponId);
                Title(weapon.WeaponName.ToUpperInvariant(), new Vector2(85 + i * 400, 470), 20);
                Title(i == 0 ? "2.2 s / MAGAZINE + SLIDE" : i == 1 ? "0.65 s / SHELL + PUMP" : "3.1 s / MAGAZINE + BOLT", new Vector2(85 + i * 400, 510), 16);
                Reset(inventory, i, 0, weapon.MagCapacity);
                Check(inventory.StartReload(), "empty reload starts: " + weapon.WeaponId);
                Check(view.ReloadAudio.Playing && view.ReloadAudio.Stream == weapon.ReloadOpenSound, "opening stage plays dedicated foley");
                Check(!inventory.TryConsumeAmmo(), "firing blocked while manipulating weapon");
                if (i != 1)
                {
                    inventory.UpdateReload(weapon.ReloadDuration - 0.01f);
                    Check(inventory.IsReloading && inventory.CurrentAmmo == 0, "magazine cannot fire before the action is ready");
                    inventory.UpdateReload(0.02f);
                    Check(!inventory.IsReloading && inventory.CurrentAmmo == weapon.MagCapacity && inventory.CurrentReserve == 0, "magazine reload duration and ammunition conservation");
                }
                else
                {
                    inventory.UpdateReload(0.45f + 0.65f + 0.001f);
                    Check(inventory.IsReloading && inventory.CurrentAmmo == 1 && inventory.CurrentReserve == 5, "shotgun inserts one shell at a time");
                    inventory.CancelReload();
                    Check(inventory.CurrentAmmo == 1 && inventory.CurrentReserve == 5 && inventory.TryConsumeAmmo(), "interrupted shotgun preserves and can fire an inserted shell");
                    inventory.StartReload(); inventory.UpdateReload(20);
                    Check(inventory.CurrentAmmo == 5 && inventory.CurrentReserve == 0 && !inventory.IsReloading, "large time step loads only available shells without duplication");
                }
                Reset(inventory, i, 0, 2);
                inventory.StartReload();
                inventory.UpdateReload(weapon.ReloadDuration * (i == 1 ? 0.45f : 0.25f) + 0.001f);
                Check(view.ReloadAudio.Playing && view.ReloadAudio.Stream == weapon.ReloadInsertSound, "insertion stage selects matching weapon foley");
                float phaseBeforePause = inventory.ReloadPhaseProgress;
                double audioBeforePause = view.ReloadAudio.GetPlaybackPosition();
                actor.SetProcess(true);
                Tree.Paused = true;
                await Wait(0.08);
                Check(inventory.ReloadPhaseProgress == phaseBeforePause && Math.Abs(view.ReloadAudio.GetPlaybackPosition() - audioBeforePause) < 0.04, $"pause freezes reload handling and its audio together (phase {phaseBeforePause}/{inventory.ReloadPhaseProgress}, audio {audioBeforePause}/{view.ReloadAudio.GetPlaybackPosition()})");
                Tree.Paused = false;
                actor.SetProcess(false);
                inventory.CancelReload();
                Reset(inventory, i, 0, 2);
                inventory.StartReload(); inventory.UpdateReload(0.1f);
                inventory.SelectWeapon((i + 1) % 3);
                Check(!inventory.IsReloading && !view.ReloadAudio.Playing && actor.GunSprite.Rotation == 0, "switch interrupts audio and clears weapon pose");
                inventory.SelectWeapon(i);
                Check(inventory.CurrentAmmo == 0 && inventory.CurrentReserve == 2, "early interruption conserves ammunition");
                Reset(inventory, i, weapon.MagCapacity, 0);
                Check(!inventory.StartReload() && !view.ReloadAudio.Playing, "full weapon does not play reload sounds");
            }
            await Wait(0.1);
            await Capture("weapons-ready");
            foreach (var actor in actors)
            {
                var inventory = actor.InventoryComponent;
                Reset(inventory, inventory.CurrentWeaponIndex, 0, 6);
                inventory.StartReload();
                inventory.UpdateReload(inventory.CurrentWeapon.ReloadDuration * (inventory.CurrentWeaponIndex == 1 ? 0.45f : 0.25f) + 0.2f);
            }
            await Wait(0.06);
            await Capture("weapons-reloading");
            foreach (var actor in actors)
            {
                var inventory = actor.InventoryComponent;
                var view = actor.GetNode<WeaponPresentation>("%WeaponPresentation");
                Check(Mathf.Abs(actor.GunSprite.Rotation) > 0.1f && view.AmmunitionProp.Visible, "reload visibly lowers weapon and moves ammunition with the support hand");
                actor.DashState.Enter();
                Check(!inventory.IsReloading && !view.ReloadAudio.Playing, "dodge cancels reload and foley");
                actor.DashState.Exit();
                inventory.StartReload();
                actor.HealthComponent.TakeDamage(1000, Vector2.Zero);
                Check(!inventory.IsReloading && !view.ReloadAudio.Playing, "death cancels reload and foley");
            }
            GD.Print($"WEAPON SMOKE PASS: {_checks} checks");
            foreach (var actor in actors) actor.QueueFree();
            await Wait(0.3); Tree.Quit();
        }
        catch (Exception error) { GD.PushError("WEAPON SMOKE FAIL: " + error); Tree.Quit(1); }
    }

    private static void Reset(InventoryComponent inventory, int index, int magazine, int reserve)
    {
        var weapon = inventory.Weapons[index];
        inventory.SetAmmoState(new Dictionary<string, int> { [weapon.WeaponId] = magazine },
            new Dictionary<string, int> { [weapon.AmmoId] = reserve }, index);
    }
    private void Title(string text, Vector2 position, int size)
    {
        var label = new Label { Text = text, Position = position, Modulate = new Color("bccac7") };
        label.AddThemeFontSizeOverride("font_size", size); AddChild(label);
    }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-playtest")) return;
        await ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(false);
        using var screenshot = GetViewport().GetTexture().GetImage();
        string destination = OS.HasFeature("qa") ? $"user://{name}.png" : $"res://builds/{name}.png";
        Check(screenshot.SavePng(destination) == Error.Ok, "capture " + name);
    }
    private async Task Wait(double seconds) => await ToSignal(Tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
