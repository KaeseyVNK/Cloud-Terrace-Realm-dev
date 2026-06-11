public class StalkState : FogSpriteState
{
    private const float STEAL_RANGE = 0.8f;

    public StalkState(FogSpriteController c) : base(c) { }

    public override void Enter()
    {
        ctrl.MoveTo(ctrl.TargetStorage.GetPosition());
    }

    public override void Tick()
    {
        if (ctrl.IsInLight())
        {
            ctrl.StateMachine.ChangeState(new FleeState(ctrl));
            return;
        }

        if (ctrl.HasReached(STEAL_RANGE))
        {
            ctrl.StateMachine.ChangeState(new StealState(ctrl));
        }
    }
}