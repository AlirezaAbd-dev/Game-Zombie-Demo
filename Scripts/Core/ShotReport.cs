namespace TopDownDemo.Core;

/// <summary>All pellets from one trigger pull share one accuracy result.</summary>
public sealed class ShotReport
{
    private bool _hitRecorded;
    public bool TryRecordHit()
    {
        if (_hitRecorded) return false;
        _hitRecorded = true;
        return true;
    }
}
