using Godot;
using TopDownDemo.Entities;

namespace TopDownDemo.UI;

public partial class CrosshairUI : Sprite2D
{
    private Player _player;
    private readonly Vector2 _baseScale = Vector2.One;

    private Control _pauseMenu;
    private Control _inventoryMenu;
    private MiniMapUI _mapMenu;
    private Control _gameOverPanel;
    private Control _victoryPanel;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        _player = GetTree().GetFirstNodeInGroup("player") as Player;

        _pauseMenu = GetParent()?.GetNodeOrNull<Control>("%PauseMenuUI");
        _inventoryMenu = GetParent()?.GetNodeOrNull<Control>("%InventoryUI");
        _mapMenu = GetParent()?.GetNodeOrNull<MiniMapUI>("%MiniMapUI");
        _gameOverPanel = GetParent()?.GetNodeOrNull<Control>("%GameOverPanel");
        _victoryPanel = GetParent()?.GetNodeOrNull<Control>("%VictoryPanel");
    }

    public override void _Process(double delta)
    {
        bool isAnyMenuOpen = GetTree().Paused
            || (_pauseMenu != null && _pauseMenu.Visible)
            || (_inventoryMenu != null && _inventoryMenu.Visible)
            || (_mapMenu != null && _mapMenu.IsFullscreen)
            || (_gameOverPanel != null && _gameOverPanel.Visible)
            || (_victoryPanel != null && _victoryPanel.Visible)
            || (PadlockPuzzleUI.Instance != null && PadlockPuzzleUI.Instance.Visible);

        if (isAnyMenuOpen)
        {
            if (Input.MouseMode != Input.MouseModeEnum.Visible)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            Visible = false;
            return;
        }

        if (Input.MouseMode != Input.MouseModeEnum.Hidden)
        {
            Input.MouseMode = Input.MouseModeEnum.Hidden;
        }
        Visible = true;

        GlobalPosition = GetViewport().GetMousePosition();

        if (!IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
        }

        if (IsInstanceValid(_player))
        {
            float dt = (float)delta;
            if (_player.IsAiming)
            {
                Scale = Scale.Lerp(_baseScale * 0.8f, dt * 12.0f);
                Modulate = Modulate.Lerp(new Color(2.5f, 0.4f, 0.4f, 1.0f), dt * 12.0f);
            }
            else if (_player.Velocity.Length() > 20.0f)
            {
                Scale = Scale.Lerp(_baseScale * 1.25f, dt * 10.0f);
                Modulate = Modulate.Lerp(new Color(1.0f, 1.0f, 1.0f, 0.8f), dt * 10.0f);
            }
            else
            {
                Scale = Scale.Lerp(_baseScale, dt * 8.0f);
                Modulate = Modulate.Lerp(Colors.White, dt * 8.0f);
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
