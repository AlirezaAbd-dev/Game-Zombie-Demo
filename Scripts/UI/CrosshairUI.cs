using Godot;

namespace TopDownDemo.UI;

public partial class CrosshairUI : Sprite2D
{
    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;
    }

    public override void _Process(double delta)
    {
        GlobalPosition = GetViewport().GetMousePosition();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
