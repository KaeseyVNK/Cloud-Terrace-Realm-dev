using UnityEngine;

public class StealState : FogSpriteState
{
    private float _timer;
    private const float STEAL_DURATION = 1.5f;

    public StealState(FogSpriteController c) : base(c) { }

    public override void Enter()
    {
        _timer = 0f;
        ctrl.StopMoving();
    }

    public override void Tick()
    {
        if (ctrl.IsInLight())
        {
            ctrl.StateMachine.ChangeState(new FleeState(ctrl));
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= STEAL_DURATION)
        {
            int stolen = ctrl.TargetStorage.TakeResources(ctrl.stealAmount);
            GameLog.Log($"[FogSprite] Stole {stolen} resources!");
            ctrl.StateMachine.ChangeState(new FleeState(ctrl));
        }
    }
}