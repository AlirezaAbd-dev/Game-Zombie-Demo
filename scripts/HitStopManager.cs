using Godot;

public partial class HitStopManager : Node
{
    public static HitStopManager? Instance { get; private set; }

    private ulong _restoreTimestampUsec = 0;
    private bool _isActive = false;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
            Engine.TimeScale = 1.0;
        }
    }

    public void TriggerHitStop(float timeScale, float durationRealtimeSec)
    {
        Engine.TimeScale = Mathf.Clamp(timeScale, 0.01f, 1.0f);
        _restoreTimestampUsec = Time.GetTicksUsec() + (ulong)(durationRealtimeSec * 1_000_000f);
        _isActive = true;
    }

    public override void _Process(double delta)
    {
        if (_isActive && Time.GetTicksUsec() >= _restoreTimestampUsec)
        {
            if (Engine.TimeScale < 1.0f && !GetTree().Paused)
            {
                Engine.TimeScale = 1.0f;
            }
            _isActive = false;
        }
    }
}
