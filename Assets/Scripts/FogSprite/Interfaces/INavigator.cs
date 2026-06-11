using UnityEngine;

public interface INavigator
{
    void MoveTo(Vector3 destination);
    bool HasReached(float threshold = 0.3f);
    void Stop();
}