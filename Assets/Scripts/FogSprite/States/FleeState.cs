using UnityEngine;

public class FleeState : FogSpriteState
{
    public FleeState(FogSpriteController c) : base(c) { }

    public override void Enter()
    {
        // Freeze công trình vừa trộm
        if (ctrl.TargetStorage != null)
        {
            var freezable = FreezeManager.Instance?.FindNearestFreezable(
                ctrl.TargetStorage.GetPosition(), 2f);

            if (freezable != null)
                freezable.Freeze();
        }

        ctrl.MoveTo(ctrl.fogSpawnPoint.position);
    }

    public override void Tick()
    {
        if (ctrl.HasReached())
        {
            ctrl.StateMachine.ChangeState(new DissolveState(ctrl));
        }
    }
}