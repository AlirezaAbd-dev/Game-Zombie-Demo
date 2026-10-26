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
        MouseFilter = MouseFilterEnum.Ignore;
        if (RadarPanel == null)
        {
            RadarPanel = new PanelContainer { Name = "RadarPanel" };
            RadarPanel.SetAnchorsPreset(LayoutPreset.TopRight);
            RadarPanel.OffsetLeft = -210; RadarPanel.OffsetRight = -20;
            RadarPanel.OffsetTop = 110; RadarPanel.OffsetBottom = 305;
            AddChild(RadarPanel);
            var column = new VBoxContainer(); RadarPanel.AddChild(column);
            MapTitleLabel = new Label { Text = $"FACILITY MAP [{ControlBindings.Hint("toggle_map")}]", HorizontalAlignment = HorizontalAlignment.Center };
            MapTitleLabel.AddThemeFontSizeOverride("font_size", 12); column.AddChild(MapTitleLabel);
            MapCanvas = new Control { CustomMinimumSize = new Vector2(180, 160),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore };
            column.AddChild(MapCanvas);
        }
        if (RadarPanel != null) RadarPanel.ClipContents = true;
        RadarPanel?.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.015f, 0.03f, 0.045f, 0.97f),
            BorderColor = new Color(0.14f, 0.42f, 0.5f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8
        });

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
        if (@event.IsActionPressed("toggle_map") && !@event.IsEcho())
        {
            ToggleFullscreenMap();
            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleFullscreenMap()
    {
        if (!IsFullscreen && GetTree().Paused) return;
        IsFullscreen = !IsFullscreen;
        if (!IsFullscreen) ControlBindings.ReleaseGameplayActions();
        if (MapTitleLabel != null) MapTitleLabel.Text = $"FACILITY MAP [{ControlBindings.Hint("toggle_map")}]";
        GetTree().Paused = IsFullscreen;
        Input.MouseMode = IsFullscreen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;

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
                if (MapTitleLabel != null) MapTitleLabel.Text = $"TACTICAL SECTOR BLUEPRINT [{ControlBindings.Hint("toggle_map")} TO CLOSE]";
            }
            else
            {
                RadarPanel.SetAnchorsPreset(LayoutPreset.TopRight);
                RadarPanel.OffsetLeft = -200;
                RadarPanel.OffsetRight = -20;
                RadarPanel.OffsetTop = 130;
                RadarPanel.OffsetBottom = 310;
                _mapScale = 0.075f;
                if (MapTitleLabel != null) MapTitleLabel.Text = $"TACTICAL RADAR ({ControlBindings.Hint("toggle_map")})";
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

        if (CampaignActDirector.Instance != null)
        {
            foreach (var room in CampaignActDirector.Instance.Rooms)
                DrawSectorRect(canvas, canvasCenter, room.Bounds, new Color(0.1f, 0.23f, 0.28f, 0.5f), room.Title);
        }
        else
        {
        // Sector room blueprint rectangles matching WorldManager boundaries
        DrawSectorRect(canvas, canvasCenter, WorldManager.Sector1Bounds, new Color(0.12f, 0.25f, 0.4f, 0.6f), "SEC-1");
        DrawSectorRect(canvas, canvasCenter, WorldManager.Sector2Bounds, new Color(0.35f, 0.25f, 0.1f, 0.6f), "SUB-2");
        DrawSectorRect(canvas, canvasCenter, WorldManager.Sector3Bounds, new Color(0.1f, 0.35f, 0.3f, 0.6f), "LAB-3");
        DrawSectorRect(canvas, canvasCenter, WorldManager.Sector4Bounds, new Color(0.4f, 0.35f, 0.1f, 0.6f), "HELI-4");
        }

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
        if (CampaignActDirector.Instance != null)
        {
            if (CampaignActDirector.Instance.ActiveObjective == null) return;
            targetWorld = CampaignActDirector.Instance.ActiveObjective.Waypoint;
        }
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
        if (IsFullscreen && CampaignActDirector.Instance != null)
            canvas.DrawString(ThemeDB.FallbackFont, topL + new Vector2(4, 14), label, width: Mathf.Max(0, mapRect.Size.X - 8), fontSize: 10,
                modulate: new Color(0.7f, 0.9f, 0.95f));
    }

    private Vector2 WorldToMapPos(Vector2 canvasCenter, Vector2 worldPos)
    {
        if (CampaignActDirector.Instance != null)
        {
            Vector2 center = _player.GlobalPosition;
            float scale = 0.07f;
            var rooms = CampaignActDirector.Instance.Rooms;
            if (IsFullscreen && rooms.Count > 0)
            {
                Rect2 bounds = rooms[0].Bounds;
                foreach (var room in rooms) bounds = bounds.Merge(room.Bounds);
                center = bounds.GetCenter();
                scale = Mathf.Min((MapCanvas.Size.X - 40) / bounds.Size.X, (MapCanvas.Size.Y - 40) / bounds.Size.Y);
            }
            return canvasCenter + (worldPos - center) * scale;
        }
        Vector2 offsetFromCenter = worldPos - _worldCenter;
        return canvasCenter + offsetFromCenter * _mapScale;
    }
}
