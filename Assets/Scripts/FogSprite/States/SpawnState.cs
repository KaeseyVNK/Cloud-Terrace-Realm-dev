using UnityEngine;

public class SpawnState : FogSpriteState
{
    private float _timer;
    private const float SPAWN_DURATION = 1.2f;

    public SpawnState(FogSpriteController c) : base(c) { }

    public override void Enter()
    {
        _timer = 0f;
        ctrl.SR.color = new Color(1, 1, 1, 0f);
    }

    public override void Tick()
    {
        _timer += Time.deltaTime;
        float alpha = Mathf.Clamp01(_timer / SPAWN_DURATION);
        ctrl.SR.color = new Color(1, 1, 1, alpha * 0.4f);

        if (_timer >= SPAWN_DURATION)
        {
            ctrl.TargetStorage = FindNearestStorage();

            if (ctrl.TargetStorage != null)
                ctrl.StateMachine.ChangeState(new StalkState(ctrl));
            else
                ctrl.StateMachine.ChangeState(new DormantState(ctrl));
        }
    }

    private IResourceStorage FindNearestStorage()
    {
        IResourceStorage nearest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < ResourceStorageRegistry.Instances.Count; i++)
        {
            var s = ResourceStorageRegistry.Instances[i];
            if (s == null || !s.HasResources()) continue;

            float d = Vector3.Distance(ctrl.transform.position, s.GetPosition());
            if (d < minDist) { minDist = d; nearest = s; }
        }
        return nearest;
    }
}