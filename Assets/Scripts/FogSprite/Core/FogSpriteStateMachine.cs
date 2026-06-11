public class FogSpriteStateMachine
{
    private FogSpriteState _current;

    public void ChangeState(FogSpriteState newState)
    {
        _current?.Exit();
        _current = newState;
        _current.Enter();
    }

    public void Tick() => _current?.Tick();
}