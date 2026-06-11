using UnityEngine;

public class DormantState : FogSpriteState
{
    public DormantState(FogSpriteController c) : base(c) { }

    public override void Enter()
    {
        ctrl.gameObject.SetActive(false);
    }

    public override void Tick() { }

    public void WakeUp()
    {
        ctrl.gameObject.SetActive(true);
        ctrl.StateMachine.ChangeState(new SpawnState(ctrl));
    }
}