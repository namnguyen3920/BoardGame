using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Subject : Singleton_Mono_Method<Subject>, ISubject
{
    private List<IObserver> _obs = new List<IObserver>();
    public void AddMethod(IObserver observer)
    {
        this._obs.Add(observer);
    }
    public void RemoveMethod(IObserver observer)
    {
        this._obs.Remove(observer);
    }
    public void Notify()
    {
        foreach(var observer in this._obs)
        {
            observer?.Invoke();
        }
    }
}
