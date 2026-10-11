using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace GTIP
{
    internal static class NativeAccess
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static readonly Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();
        static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        internal static object Call(object target, string name, params object[] args)
        {
            var type = target as Type ?? target.GetType();
            string key = type.FullName + "." + name;
            if (!Methods.TryGetValue(key, out var method))
                Methods[key] = method = type.GetMethod(name, Flags) ?? throw new MissingMethodException(key);
            try { return method.Invoke(target is Type ? null : target, args); }
            catch (TargetInvocationException error)
            { ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw(); throw; }
        }
        static FieldInfo Field(object target, string name)
        {
            var type = target as Type ?? target.GetType();
            string key = type.FullName + "." + name;
            if (!Fields.TryGetValue(key, out var field))
                Fields[key] = field = type.GetField(name, Flags) ?? throw new MissingFieldException(key);
            return field;
        }
        internal static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target is Type ? null : target);
        internal static void Set(object target, string name, object value) => Field(target, name).SetValue(target is Type ? null : target, value);
    }
}
