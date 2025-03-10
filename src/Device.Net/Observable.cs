﻿using System;
using System.Collections.Generic;

namespace Device.Net
{
    /// <summary>
    /// Basic observable which does thib of the Rx extensions so that we don't have to add that dependency
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Observable<T> : IObservable<T>
    {
        #region Fields
        private readonly List<IObserver<T>> _observers = new();
        #endregion

        public void Next(T item) => Locked(() => _observers.ForEach(o => o.OnNext(item)));

        #region Implementation
        public IDisposable Subscribe(IObserver<T> observer)
        {
            Locked(() => _observers.Add(observer));
            return new UnsubscribeDisposable(() => Unsubscribe(observer));
        }
        #endregion

        #region Private or internal methods
        private void Locked(Action action)
        {
            lock (_observers) action();
        }

        internal void Unsubscribe(IObserver<T> observer) => Locked(() => _observers.Remove(observer));
        #endregion
    }
}
