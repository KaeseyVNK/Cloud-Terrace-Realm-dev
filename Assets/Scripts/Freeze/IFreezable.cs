using UnityEngine;

public interface IFreezable
{
    void Freeze();
    void Unfreeze();
    bool IsFrozen { get; }
    Vector3 GetPosition();
}