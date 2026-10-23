using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class MapCanvasControl : Control
{
    [Export] public MiniMapUI MiniMap { get; set; }

    public override void _Draw()
    {
        MiniMap?.DrawMapElements(this);
    }
}

public partial class MiniMapUI : Control
{
    [Export] public bool IsFullscreen { get; set; } = false;

    [ExportGroup("UI Nodes")]
    [Export] public PanelContainer RadarPanel { get; set; }
    [Export] public Control MapCanvas { get; set; }
    [Export] public TextureRect PlayerMarker { get; set; }
    [Export] public Label MapTitleLabel { get; set; }

    private Player _player;
    private readonly Vector2 _worldCenter = new(1000, -500);
    private float _mapScale = 0.075f;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        RadarPanel ??= GetNodeOrNull<PanelContainer>("%RadarPanel");
        MapCanvas ??= GetNodeOrNull<Control>("%MapCanvas");
        PlayerMarker ??= GetNodeOrNull<TextureRect>("%PlayerMarker");
        MapTitleLabel ??= GetNodeOrNull<Label>("%MapTitleLabel");

        if (MapCanvas != null)
        {
            MapCanvas.Draw += () => DrawMapElements(MapCanvas);
        }

        CallDeferred(nameof(InitPlayer));
    }

    private void InitPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.M)
        {
            ToggleFullscreenMap();
            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleFullscreenMap()
    {
        IsFullscreen = !IsFullscreen;
        GetTree().Paused = IsFullscreen;

        if (RadarPanel != null)
        {
            if (IsFullscreen)
            {
                RadarPanel.SetAnchorsPreset(LayoutPreset.Center);
                RadarPanel.OffsetLeft = -320;
                RadarPanel.OffsetRight = 320;
                RadarPanel.OffsetTop = -240;
                RadarPanel.OffsetBottom = 240;
                _mapScale = 0.17f;
                if (MapTitleLabel != null) MapTitleLabel.Text = "TACTICAL SECTOR BLUEPRINT [M TO CLOSE]";
            }
            else
            {
                RadarPanel.SetAnchorsPreset(LayoutPreset.TopRight);
                RadarPanel.OffsetLeft = -200;
                RadarPanel.OffsetRight = -20;
                RadarPanel.OffsetTop = 130;
                RadarPanel.OffsetBottom = 310;
                _mapScale = 0.075f;
                if (MapTitleLabel != null) MapTitleLabel.Text = "TACTICAL RADAR (M)";
            }
        }
        MapCanvas?.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_player) || MapCanvas == null) return;

        MapCanvas.QueueRedraw();
    }

    public void DrawMapElements(CanvasItem canvas)
    {
        if (!IsInstanceValid(_player)) return;

        Vector2 canvasCenter = MapCanvas.Size * 0.5f;

        // Sector room blueprint rectangles matching WorldManager boundaries
        DrawSectorRect(canvas, canvasCenter, WorldManager.Sector1Bounds, new Color(0.12f, 0.25f, 0.4f, 0.6f), "SEC-1");
        DrawSectorRect(canvas, canvasCenter, WorldManager.Sector2Bounds, new Color(0.35f, 0.25f, 0.1f, 0.6f), "SUB-2");
        DrawSectorRect(canvas, canvasCenter, WorldManager.Sector3Bounds, new Color(0.1f, 0.35f, 0.3f, 0.6f), "LAB-3");
        DrawSectorRect(canvas, canvasCenter, WorldManager.Sector4Bounds, new Color(0.4f, 0.35f, 0.1f, 0.6f), "HELI-4");

        // Draw Player position and arrow
        Vector2 playerMapPos = WorldToMapPos(canvasCenter, _player.GlobalPosition);
        canvas.DrawCircle(playerMapPos, 5.0f, new Color(0.2f, 1.0f, 0.4f, 1.0f));

        Vector2 facing = new Vector2(Mathf.Cos(_player.GlobalRotation), Mathf.Sin(_player.GlobalRotation)) * 12.0f;
        canvas.DrawLine(playerMapPos, playerMapPos + facing, new Color(0.2f, 1.0f, 0.4f, 1.0f), 2.0f);

        // Draw Objectives
        var phase = StoryDirector.Instance?.CurrentPhase ?? StoryPhase.Act1_Blackout;
        Vector2 targetWorld = phase switch
        {
            StoryPhase.Act1_Blackout => new Vector2(1820, 350),          // Generator
            StoryPhase.Act2_ContainmentBreach => new Vector2(600, -250), // Terminal
            _ => new Vector2(600, -1350)                                 // Helipad
        };
        Vector2 objMapPos = WorldToMapPos(canvasCenter, targetWorld);
        canvas.DrawCircle(objMapPos, 6.0f, new Color(1.0f, 0.85f, 0.2f, 0.9f));
        canvas.DrawArc(objMapPos, 10.0f, 0, Mathf.Tau, 12, new Color(1.0f, 0.85f, 0.2f, 0.7f), 1.5f);
    }

    private void DrawSectorRect(CanvasItem canvas, Vector2 canvasCenter, Rect2 worldRect, Color fillColor, string label)
    {
        Vector2 topL = WorldToMapPos(canvasCenter, worldRect.Position);
        Vector2 botR = WorldToMapPos(canvasCenter, worldRect.End);
        Rect2 mapRect = new Rect2(topL, botR - topL);

        canvas.DrawRect(mapRect, fillColor, true);
        canvas.DrawRect(mapRect, new Color(0.2f, 0.7f, 1.0f, 0.8f), false, 1.5f);
    }

    private Vector2 WorldToMapPos(Vector2 canvasCenter, Vector2 worldPos)
    {
        Vector2 offsetFromCenter = worldPos - _worldCenter;
        return canvasCenter + offsetFromCenter * _mapScale;
    }
}
