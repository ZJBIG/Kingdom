using System;
using UnityEngine;

// Observers run after state changes and must not interrupt the committed operation.
internal static class ObserverNotifications
{
    internal static void Publish(Action handlers)
    {
        if (handlers == null)
            return;
        Delegate[] listeners = handlers.GetInvocationList();
        for (int i = 0; i < listeners.Length; i++)
        {
            try
            {
                ((Action)listeners[i])();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    internal static void Publish<T>(Action<T> handlers, T state)
    {
        if (handlers == null)
            return;
        Delegate[] listeners = handlers.GetInvocationList();
        for (int i = 0; i < listeners.Length; i++)
        {
            try
            {
                ((Action<T>)listeners[i])(state);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
