using System;
using System.Collections.Generic;

namespace Ursa.UI.Core
{
    /// <summary>
    /// Minimal service locator for Ursa UI runtime.
    /// </summary>
    public static class UrsaCore
    {
        private static readonly Dictionary<Type, object> Services = new();

        public static void Register<T>(T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Services[typeof(T)] = instance;
        }

        public static bool TryResolve<T>(out T service)
        {
            if (Services.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                service = typed;
                return true;
            }

            service = default;
            return false;
        }

        public static T Resolve<T>()
        {
            if (TryResolve<T>(out var service))
                return service;

            throw new InvalidOperationException($"Service not found: {typeof(T)}");
        }

        public static void Clear() => Services.Clear();
    }
}
