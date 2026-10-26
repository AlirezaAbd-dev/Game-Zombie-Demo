using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class StartingLoadoutData : Resource
{
    [Export] public WeaponData StarterWeapon { get; set; }
    [Export] public ItemData Dressing { get; set; }
    [Export] public Godot.Collections.Array<int> StartingRounds { get; set; } = new();
    [Export] public Godot.Collections.Array<int> StartingDressings { get; set; } = new();

    public int Rounds(DifficultyLevel difficulty) => At(StartingRounds,difficulty);
    public int Dressings(DifficultyLevel difficulty) => At(StartingDressings,difficulty);
    private static int At(Godot.Collections.Array<int> values,DifficultyLevel difficulty)
        => values.Count == 0 ? 0 : Mathf.Max(0,values[Mathf.Clamp((int)difficulty,0,values.Count - 1)]);
}
