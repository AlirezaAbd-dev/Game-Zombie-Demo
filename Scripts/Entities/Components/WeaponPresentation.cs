using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

/// <summary>Views inventory reload stages; owns hands, mechanical foley and weapon handling only.</summary>
public partial class WeaponPresentation : Node2D
{
    [Export] public InventoryComponent Inventory { get; set; }
    [Export] public Sprite2D Gun { get; set; }
    [Export] public Sprite2D SupportHand { get; set; }
    [Export] public Sprite2D GripHand { get; set; }
    [Export] public Sprite2D AmmunitionProp { get; set; }
    [Export] public AudioStreamPlayer2D ReloadAudio { get; set; }
    [Export] public Texture2D MagazineTexture { get; set; }
    [Export] public Texture2D ShellTexture { get; set; }
    [Export] public Texture2D UpperSleeveTexture { get; set; }
    [Export] public Texture2D ForearmTexture { get; set; }
    private Sprite2D _supportUpper, _supportForearm, _gripUpper, _gripForearm;

    public override void _Ready()
    {
        if (Inventory == null || Gun == null || SupportHand == null || GripHand == null || AmmunitionProp == null || ReloadAudio == null || UpperSleeveTexture == null || ForearmTexture == null)
        {
            GD.PushError("WeaponPresentation requires inventory, gun, hand, ammunition and audio exports.");
            SetProcess(false);
            return;
        }
        _supportUpper = MakeSleeve(UpperSleeveTexture);
        _supportForearm = MakeSleeve(ForearmTexture);
        _gripUpper = MakeSleeve(UpperSleeveTexture);
        _gripForearm = MakeSleeve(ForearmTexture);
        PlayerSettings.EnsureLoaded();
        ReloadAudio.Bus = PlayerSettings.EffectsBus;
        Inventory.ReloadPhaseChanged += OnPhaseChanged;
        Inventory.WeaponChanged += OnWeaponChanged;
        OnWeaponChanged(Inventory.CurrentWeapon);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Inventory))
        {
            Inventory.ReloadPhaseChanged -= OnPhaseChanged;
            Inventory.WeaponChanged -= OnWeaponChanged;
        }
        if (GodotObject.IsInstanceValid(ReloadAudio)) { ReloadAudio.Stop(); ReloadAudio.Stream = null; }
    }

    private void OnWeaponChanged(WeaponData weapon)
    {
        if (weapon == null) return;
        OnPhaseChanged(ReloadPhase.Idle);
        UpdatePose();
    }

    private void OnPhaseChanged(ReloadPhase phase)
    {
        ReloadAudio.Stop();
        var weapon = Inventory.CurrentWeapon;
        ReloadAudio.Stream = phase switch
        {
            ReloadPhase.Open => weapon?.ReloadOpenSound,
            ReloadPhase.Insert => weapon?.ReloadInsertSound,
            ReloadPhase.Close => weapon?.ReloadCloseSound,
            _ => null
        };
        if (ReloadAudio.Stream != null) ReloadAudio.Play();
        if (phase == ReloadPhase.Idle)
        {
            Gun.Rotation = 0;
            if (weapon != null) Gun.Position = weapon.WeaponOffset;
            AmmunitionProp.Visible = false;
        }
    }

    public override void _Process(double delta) => UpdatePose();

    private void UpdatePose()
    {
        var weapon = Inventory.CurrentWeapon;
        if (weapon == null) return;
        bool shells = weapon.ReloadStyle == ReloadStyle.IndividualShells;
        float progress = Inventory.ReloadPhaseProgress;
        Vector2 support = Gun.Position + weapon.SupportOffset.Rotated(Gun.Rotation);
        GripHand.Position = Gun.Position + weapon.GripOffset.Rotated(Gun.Rotation);
        SupportHand.Position = support;
        SupportHand.Visible = GripHand.Visible = Gun.Visible;
        AmmunitionProp.Visible = false;
        if (Inventory.IsReloading)
        {
            float tuck = Inventory.CurrentReloadPhase switch
            {
                ReloadPhase.Open => Mathf.SmoothStep(0, 1, progress),
                ReloadPhase.Close => 1 - Mathf.SmoothStep(0, 1, progress),
                _ => 1
            };
            Gun.Rotation = tuck * (shells ? 0.15f : 0.22f);
            Gun.Position = weapon.WeaponOffset + new Vector2(-1, 5) * tuck;
            GripHand.Position = Gun.Position + weapon.GripOffset.Rotated(Gun.Rotation);
            Vector2 pouch = new(-5, 13);
            Vector2 loadingPort = GripHand.Position + new Vector2(0, 2);
            SupportHand.Position = Inventory.CurrentReloadPhase switch
            {
                ReloadPhase.Open => support.Lerp(pouch, progress),
                ReloadPhase.Insert => pouch.Lerp(loadingPort, Mathf.SmoothStep(0, 0.8f, progress)),
                _ => loadingPort.Lerp(support, progress)
            };
            AmmunitionProp.Texture = shells ? ShellTexture : MagazineTexture;
            AmmunitionProp.Position = SupportHand.Position + new Vector2(2, -1);
            AmmunitionProp.Rotation = shells ? -0.2f : -0.6f;
            AmmunitionProp.Visible = Gun.Visible && Inventory.CurrentReloadPhase == ReloadPhase.Insert && progress < 0.8f;
        }
        PoseArm(new Vector2(0, -9), SupportHand.Position, 12, 15, -1, _supportUpper, _supportForearm);
        PoseArm(new Vector2(0, 9), GripHand.Position, 10, 12, 1, _gripUpper, _gripForearm);
        GripHand.Rotation = Gun.Rotation;
        SupportHand.Rotation = _supportForearm.Rotation * 0.35f;
    }

    private Sprite2D MakeSleeve(Texture2D texture)
    {
        var sleeve = new Sprite2D { Texture = texture, TextureFilter = TextureFilterEnum.Linear };
        AddChild(sleeve);
        return sleeve;
    }

    private void PoseArm(Vector2 shoulder, Vector2 wrist, float upperLength, float forearmLength,
        float bend, Sprite2D upper, Sprite2D forearm)
    {
        // Two-link arm: elbows follow the wrists while keeping the upper/forearm proportions.
        Vector2 offset = wrist - shoulder;
        float distance = Mathf.Clamp(offset.Length(), Mathf.Abs(upperLength - forearmLength) + 0.01f,
            upperLength + forearmLength - 0.01f);
        Vector2 direction = offset.Normalized();
        if (direction == Vector2.Zero) direction = Vector2.Right;
        float along = (upperLength * upperLength - forearmLength * forearmLength + distance * distance) / (2 * distance);
        float outward = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
        Vector2 elbow = shoulder + direction * along + new Vector2(-direction.Y, direction.X) * outward * bend;
        PoseSleeve(upper, shoulder, elbow, 0.19f);
        PoseSleeve(forearm, elbow, wrist, 0.17f);
    }

    private void PoseSleeve(Sprite2D sleeve, Vector2 start, Vector2 end, float thickness)
    {
        sleeve.Visible = Gun.Visible;
        sleeve.Position = (start + end) * 0.5f;
        sleeve.Rotation = (end - start).Angle();
        sleeve.Scale = new Vector2((start.DistanceTo(end) + 1.2f) / sleeve.Texture.GetWidth(), thickness);
    }
}
