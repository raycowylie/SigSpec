using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;

namespace SigSpec.Core
{
    public class SigSpecGenerator
    {
        private readonly SigSpecGeneratorSettings _settings;

        public SigSpecGenerator(SigSpecGeneratorSettings settings)
        {
            _settings = settings;
        }

        public SigSpecDocument GenerateForHubsAsync(IReadOnlyDictionary<string, Type> hubs)
        {
            SigSpecDocument document = new SigSpecDocument();
            return GenerateForHubs(hubs, document);
        }

        public SigSpecDocument GenerateForHubs(IReadOnlyDictionary<string, Type> hubs, SigSpecDocument template)
        {
            SigSpecDocument document = template;
            SigSpecSchemaResolver resolver = new SigSpecSchemaResolver(document, _settings);
            JsonSchemaGenerator generator = new JsonSchemaGenerator(_settings);

            foreach (KeyValuePair<string, Type> h in hubs)
            {
                Type type = h.Value;

                SigSpecHub hub = new SigSpecHub
                {
                    Name = type.Name.EndsWith("Hub") ? type.Name.Substring(0, type.Name.Length - 3) : type.Name,
                    Description = type.GetXmlDocsSummary()
                };

                foreach (MethodInfo method in GetOperationMethods(type))
                {
                    SigSpecOperation operation = GenerateOperation(type, method, generator, resolver, SigSpecOperationType.Sync);
                    hub.Operations[method.Name] = operation;
                }

                foreach (MethodInfo method in GetChannelMethods(type))
                {
                    hub.Operations[method.Name] = GenerateOperation(type, method, generator, resolver, SigSpecOperationType.Observable);
                }

                Type[] baseTypeGenericArguments = type.BaseType.GetGenericArguments();
                if (baseTypeGenericArguments.Length == 1)
                {
                    Type callbackType = baseTypeGenericArguments[0];
                    foreach (MethodInfo callbackMethod in GetOperationMethods(callbackType))
                    {
                        SigSpecOperation callback = GenerateOperation(type, callbackMethod, generator, resolver, SigSpecOperationType.Sync);
                        hub.Callbacks[callbackMethod.Name] = callback;
                    }
                }

                document.Hubs[h.Key] = hub;
            }

            return document;
        }

        private static IEnumerable<string> _forbiddenOperations { get; } = typeof(Hub).GetRuntimeMethods().Concat(typeof(Hub<>).GetRuntimeMethods()).Select(x => x.Name).Distinct();
        private IEnumerable<MethodInfo> GetOperationMethods(Type type)
        {
            return type.GetTypeInfo().GetRuntimeMethods().Where(m =>
            {
                bool returnsChannelReader =
                    m.ReturnType.IsGenericType &&
                    (m.ReturnType.GetGenericTypeDefinition() == typeof(ChannelReader<>) ||
                     m.ReturnType.GetGenericTypeDefinition().IsAssignableToTypeName("IAsyncEnumerable`1", TypeNameStyle.Name));

                return
                    m.IsPublic &&
                    m.IsSpecialName == false &&
                    m.DeclaringType != typeof(Hub) &&
                    m.DeclaringType != typeof(Hub<>) &&
                    m.DeclaringType != typeof(object) &&
                    !_forbiddenOperations.Contains(m.Name) &&
                    returnsChannelReader == false;
            });
        }

        private IEnumerable<MethodInfo> GetChannelMethods(Type type)
        {
            return type.GetTypeInfo().GetRuntimeMethods().Where(m =>
            {
                bool returnsChannelReader =
                    m.ReturnType.IsGenericType &&
                    (m.ReturnType.GetGenericTypeDefinition() == typeof(ChannelReader<>) ||
                     m.ReturnType.GetGenericTypeDefinition().IsAssignableToTypeName("IAsyncEnumerable`1", TypeNameStyle.Name));

                return
                    m.IsPublic &&
                    m.IsSpecialName == false &&
                    m.DeclaringType != typeof(Hub) &&
                    m.DeclaringType != typeof(Hub<>) &&
                    m.DeclaringType != typeof(object) &&
                    !_forbiddenOperations.Contains(m.Name) &&
                    returnsChannelReader == true;
            });
        }

        private SigSpecOperation GenerateOperation(Type type, MethodInfo method, JsonSchemaGenerator generator, SigSpecSchemaResolver resolver, SigSpecOperationType operationType)
        {
            SigSpecOperation operation = new SigSpecOperation
            {
                Description = method.GetXmlDocsSummary(),
                Type = operationType
            };

            foreach (ParameterInfo arg in method.GetParameters().Where(param => param.ParameterType != typeof(CancellationToken)))
            {
                SigSpecParameter parameter = generator.GenerateWithReferenceAndNullability<SigSpecParameter>(
                    arg.ParameterType.ToContextualType(), arg.ParameterType.ToContextualType().IsNullableType, resolver, (p, s) =>
                    {
                        p.Description = arg.GetXmlDocs();
                    });

                operation.Parameters[arg.Name] = parameter;
            }

            Type returnType =
                operationType == SigSpecOperationType.Observable
                    ? method.ReturnType.GetGenericArguments().First()
                : method.ReturnType == typeof(Task)
                    ? null
                : method.ReturnType.IsGenericType && method.ReturnType.BaseType == typeof(Task)
                    ? method.ReturnType.GetGenericArguments().First()
                    : method.ReturnType;

            operation.ReturnType = returnType == null ? null : generator.GenerateWithReferenceAndNullability<JsonSchema>(
                returnType.ToContextualType(), returnType.ToContextualType().IsNullableType, resolver, (p, s) =>
                {
                    string returnDescr = method.GetXmlDocsTag("returns");
                    p.Description = method.ReturnType.GetXmlDocsSummary();
                    if (returnDescr != null && (p.Description == null || p.Description == ""))
                    {
                        p.Description = returnDescr;
                    }

                });

            return operation;
        }
    }
}
