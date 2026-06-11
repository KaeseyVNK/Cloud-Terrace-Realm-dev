using UnityEngine;

public class DissolveState : FogSpriteState
{
    private float _timer;
    private const float DISSOLVE_DURATION = 1f;

    public DissolveState(FogSpriteController c) : base(c) { }

    public override void Enter()
    {
        _timer = 0f;
        ctrl.StopMoving();
    }

    public override void Tick()
    {
        _timer += Time.deltaTime;
        float alpha = Mathf.Lerp(0.4f, 0f, _timer / DISSOLVE_DURATION);
        ctrl.SR.color = new Color(1, 1, 1, alpha);

        if (_timer >= DISSOLVE_DURATION)
        {
            ctrl.StateMachine.ChangeState(new DormantState(ctrl));
        }
    }
}