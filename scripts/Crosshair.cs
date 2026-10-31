using Godot;

public partial class Crosshair : Sprite2D
{
    private float _targetScale = 1.0f;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;
    }

    public void SetDynamicSpread(float spreadRad)
    {
        _targetScale = 1.0f + Mathf.Clamp(spreadRad * 3.5f, 0.0f, 1.5f);
    }

    public override void _Process(double delta)
    {
        GlobalPosition = GetViewport().GetMousePosition();
        Scale = Scale.Lerp(Vector2.One * _targetScale, (float)delta * 18.0f);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
