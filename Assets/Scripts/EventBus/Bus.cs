using UnityEngine;

public static class Bus<T> where T : IEvent 
{
    public delegate void Event(T args);


}
