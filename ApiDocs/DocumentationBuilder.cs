using System;
using System.Collections.Generic;
using System.Reflection;

namespace AirGestureAI.ApiDocs
{
    /// <summary>
    /// Reflects over an assembly to build an <see cref="ApiTypeDoc"/> tree
    /// representing all public types and their public members.
    /// </summary>
    public sealed class DocumentationBuilder
    {
        /// <summary>
        /// Builds documentation entries for every public non-compiler-generated type.
        /// </summary>
        /// <param name="assembly">Assembly to inspect.</param>
        public IReadOnlyList<ApiTypeDoc> Build(Assembly assembly)
        {
            var result = new List<ApiTypeDoc>();

            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsSpecialName) continue;

                var members = new List<ApiMemberDoc>();

                // Properties
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    members.Add(new ApiMemberDoc
                    {
                        Name        = prop.Name,
                        MemberType  = "Property",
                        ReturnType  = prop.PropertyType.Name,
                        IsDeprecated = prop.GetCustomAttribute<ObsoleteAttribute>() is not null,
                    });

                // Methods (excluding property accessors and compiler-generated methods)
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (method.IsSpecialName) continue;
                    members.Add(new ApiMemberDoc
                    {
                        Name        = method.Name,
                        MemberType  = "Method",
                        ReturnType  = method.ReturnType.Name,
                        IsDeprecated = method.GetCustomAttribute<ObsoleteAttribute>() is not null,
                    });
                }

                // Events
                foreach (var ev in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    members.Add(new ApiMemberDoc
                    {
                        Name       = ev.Name,
                        MemberType = "Event",
                        ReturnType = ev.EventHandlerType?.Name ?? string.Empty,
                    });

                result.Add(new ApiTypeDoc
                {
                    FullName  = type.FullName ?? type.Name,
                    Name      = type.Name,
                    Namespace = type.Namespace ?? string.Empty,
                    Members   = members,
                });
            }

            return result;
        }
    }
}
