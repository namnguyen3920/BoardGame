
public interface ISubject
{
    void AddMethod(IObserver observer);
    void RemoveMethod(IObserver observer);
    void Notify();
}
