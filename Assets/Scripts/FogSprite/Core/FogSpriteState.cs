public abstract class FogSpriteState
{
    protected FogSpriteController ctrl;

    public FogSpriteState(FogSpriteController controller)
    {
        ctrl = controller;
    }

    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}