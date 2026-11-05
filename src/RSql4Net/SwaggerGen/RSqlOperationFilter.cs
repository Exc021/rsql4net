using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
#if NET10_0_OR_GREATER
// Swashbuckle 10 uses Microsoft.OpenApi 2.x: models moved to the root namespace,
// schema types are an enum and default values are JsonNodes.
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
#endif
using RSql4Net.Configurations;
using RSql4Net.Models.Paging;
using RSql4Net.Models.Queries;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RSql4Net.SwaggerGen
{
    /// <summary>
    ///
    /// </summary>
    public class RSqlOperationFilter : IOperationFilter
    {
        private readonly Settings _settings;
        private readonly IOptions<JsonOptions> _options;

        /// <summary>
        /// is query type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool IsQuery(Type type) => (type.IsGenericType &&
                                                   (type.GetGenericTypeDefinition() == typeof(IRSqlQuery<>) ||
                                                    type.GetGenericTypeDefinition() == typeof(RSqlQuery<>)));
        /// <summary>
        /// is pageable type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool IsPageable(Type type) => (type.IsGenericType &&
                                                      (type.GetGenericTypeDefinition() == typeof(IRSqlPageable<>) ||
                                                       type.GetGenericTypeDefinition() == typeof(RSqlPageable<>)));

        /// <summary>
        /// create instance of
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="options"></param>
        public RSqlOperationFilter(Settings settings, IOptions<JsonOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            foreach (var parameterInfo in context.MethodInfo.GetParameters())
            {
                if (IsQuery(parameterInfo.ParameterType))
                {
                    BuildQueryParameters(parameterInfo, operation);
                }
                else if (IsPageable(parameterInfo.ParameterType))
                {
                    BuildPageableParameters(parameterInfo, operation);
                }
            }
        }

        private string ConvertName(string name)
        {
            var policy = _options.Value.JsonSerializerOptions.PropertyNamingPolicy;
            return policy == null ? name : policy.ConvertName(name);
        }

        /// <summary>
        /// Removes the parameter(s) Swashbuckle generated for the method parameter.
        /// </summary>
        private static void RemoveGeneratedParameters(ParameterInfo parameterInfo, OpenApiOperation operation)
        {
#if NET10_0_OR_GREATER
            operation.Parameters ??= new List<IOpenApiParameter>();
#endif
            foreach (var parameter in operation.Parameters.Where(p => p.Name == parameterInfo.Name).ToList())
            {
                operation.Parameters.Remove(parameter);
            }
        }

        private void BuildQueryParameters(ParameterInfo parameterInfo, OpenApiOperation operation)
        {
            RemoveGeneratedParameters(parameterInfo, operation);
            operation.Parameters.Add(new OpenApiParameter
            {
                In = ParameterLocation.Query,
                Name = ConvertName(_settings.QueryField),
                Schema = StringSchema()
            });
        }

        private void BuildPageableParameters(ParameterInfo parameterInfo, OpenApiOperation operation)
        {
            RemoveGeneratedParameters(parameterInfo, operation);
            operation.Parameters.Add(new OpenApiParameter
            {
                In = ParameterLocation.Query,
                Name = ConvertName(_settings.PageNumberField),
                Schema = NumberSchema(0, "Page number")
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                In = ParameterLocation.Query,
                Name = ConvertName(_settings.PageSizeField),
                Schema = NumberSchema(_settings.PageSize, "Page size")
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                In = ParameterLocation.Query,
                Name = ConvertName(_settings.SortField),
                Schema = SortSchema()
            });
        }

#if NET10_0_OR_GREATER
        private static OpenApiSchema StringSchema() =>
            new OpenApiSchema { Type = JsonSchemaType.String };

        private static OpenApiSchema NumberSchema(int defaultValue, string description) =>
            new OpenApiSchema
            {
                Type = JsonSchemaType.Number, Default = JsonValue.Create(defaultValue), Description = description
            };

        private static OpenApiSchema SortSchema() =>
            new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Sort" }
            };
#else
        private static OpenApiSchema StringSchema() =>
            new OpenApiSchema { Type = "string" };

        private static OpenApiSchema NumberSchema(int defaultValue, string description) =>
            new OpenApiSchema
            {
                Type = "number", Default = new OpenApiInteger(defaultValue), Description = description
            };

        private static OpenApiSchema SortSchema() =>
            new OpenApiSchema
            {
                Type = "array", Items = new OpenApiSchema { Type = "string", Description = "Sort" }
            };
#endif
    }
}
