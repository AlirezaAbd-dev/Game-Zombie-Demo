using System;
using Godot;

namespace TopDownDemo.Core;

public partial class StateMachine : Node
{
    public IState CurrentState { get; private set; }
    public string CurrentStateName => CurrentState?.GetType().Name ?? "None";

    public event Action<IState> StateChanged;

    public void ChangeState(IState newState)
    {
        if (newState == null || CurrentState == newState)
            return;

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
        StateChanged?.Invoke(CurrentState);
    }

    public void Update(float delta)
    {
        CurrentState?.Update(delta);
    }

    public void PhysicsUpdate(float delta)
    {
        CurrentState?.PhysicsUpdate(delta);
    }
}
