using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;

namespace OpenTabletDriver.Reflection
{
    public class PluginManager
    {
        public IReadOnlyCollection<TypeInfo> PluginTypes => pluginTypes;        
        protected readonly ConcurrentBag<PluginContext> plugins = new ConcurrentBag<PluginContext>();
        protected readonly ConcurrentBag<TypeInfo> pluginTypes = new ConcurrentBag<TypeInfo>();
        
        protected readonly static IEnumerable<Type> libTypes = 
            from type in Assembly.GetAssembly(typeof(IDriver)).GetExportedTypes()
                where type.IsAbstract || type.IsInterface
                select type;

        public virtual PluginReference GetPluginReference(string path) => new PluginReference(this, path);
        public virtual PluginReference GetPluginReference(Type type) => GetPluginReference(type.FullName);
        public virtual PluginReference GetPluginReference(object obj) => GetPluginReference(obj.GetType());

        public virtual T ConstructObject<T>(string name, object[] args = null) where T : class
        {
            args ??= new object[0];
            if (!string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    var type = PluginTypes.FirstOrDefault(t => t.FullName == name);
                    var matchingConstructors = from ctor in type?.GetConstructors()
                        let parameters = ctor.GetParameters()
                        where parameters.Length == args.Length
                        where IsValidParameterFor(args, parameters)
                        select ctor;

                    var constructor = matchingConstructors.FirstOrDefault();
                    return (T)constructor?.Invoke(args) ?? null;
                }
                catch
                {
                    Log.Write("Plugin", $"Unable to construct object '{name}'", LogLevel.Error);
                }
            }
            return null;
        }

        public virtual IReadOnlyCollection<TypeInfo> GetChildTypes<T>()
        {
            var children = from type in PluginTypes
                where typeof(T).IsAssignableFrom(type)
                select type;

            return children.ToArray();
        }

        protected virtual bool IsValidParameterFor(object[] args, ParameterInfo[] parameters)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var arg = args[i];
                if (!parameter.ParameterType.IsAssignableFrom(arg.GetType()))
                    return false;
            }
            return true;
        }

        protected virtual bool IsPluginType(Type type)
        {
            return !type.IsAbstract && !type.IsInterface &&
                libTypes.Any(t => t.IsAssignableFrom(type) ||
                    type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == t));
        }

        protected virtual bool IsPlatformSupported(Type type)
        {
            var attr = (SupportedPlatformAttribute)type.GetCustomAttribute(typeof(SupportedPlatformAttribute), false);
            return attr?.IsCurrentPlatform ?? true;
        }
    }
}
