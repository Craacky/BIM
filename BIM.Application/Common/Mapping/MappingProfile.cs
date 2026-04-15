using AutoMapper;
using System.Reflection;

namespace BIM.Application.Common.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            ApplyMappingsFromAssembly(Assembly.GetExecutingAssembly());
        }

        private void ApplyMappingsFromAssembly(Assembly assembly)
        {
            var mapFromType = typeof(IMapFrom<>);
            var mapMethodName = nameof(IMapFrom<object>.Mapping);
            bool hasInterface(Type t)
                => t.IsGenericType && t.GetGenericTypeDefinition() == mapFromType;
            var types = assembly.GetExportedTypes().Where(t => t.GetInterfaces().Any(hasInterface)).ToList();
            var argumentTypes = new Type[] { typeof(Profile) };
            foreach (var type in types)
            {
                var instance = Activator.CreateInstance(type);
                var methodInfo = type.GetMethod(mapMethodName);
                if (methodInfo != null)
                    methodInfo.Invoke(instance, new object[] { this });
                else
                {
                    var interfaces = type.GetInterfaces().Where(hasInterface).ToList();
                    if (interfaces.Count > 0)
                    {
                        foreach (var interf in interfaces)
                        {
                            var interMethodInfo = interf.GetMethod(mapMethodName, argumentTypes);
                            interMethodInfo?.Invoke(instance, new object[] { this });
                        }
                    }
                }
            }
        }
    }
}
