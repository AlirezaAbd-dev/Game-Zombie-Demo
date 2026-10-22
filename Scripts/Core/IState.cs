namespace TopDownDemo.Core;

public interface IState
{
    void Enter();
    void Exit();
    void Update(float delta);
    void PhysicsUpdate(float delta);
}
