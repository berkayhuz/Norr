// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Exporters;

using Monitor = Norr.PerformanceMonitor.Core.Monitor;

namespace Norr.PerformanceMonitor.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering the Norr Performance Monitor into an
    /// <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// This class provides a single entry point to register the default Norr monitoring
    /// components, including:
    /// <list type="bullet">
    ///   <item><see cref="IMonitor"/> implementation (<see cref="Monitor"/>)</item>
    ///   <item><see cref="IMetricExporter"/> implementations:
    ///       <list type="bullet">
    ///           <item><see cref="AggregationExporter"/> for in-process aggregation</item>
    ///           <item><see cref="ConsoleExporter"/> for human-readable console output</item>
    ///       </list>
    ///   </item>
    /// </list>
    /// </remarks>
    public static class AddPerformanceMonitorExtensions
    {
        /// <summary>
        /// Registers the Norr Performance Monitor core services along with the default
        /// in-process aggregation and console exporters.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to register into.</param>
        /// <returns>
        /// The same <see cref="IServiceCollection"/> so that multiple calls can be chained.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method uses <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService, TImplementation}(IServiceCollection)"/>
        /// to avoid overwriting existing <see cref="IMonitor"/> registrations.
        /// </para>
        /// <para>
        /// It also uses <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>
        /// to allow multiple <see cref="IMetricExporter"/> implementations to be registered
        /// side-by-side without conflict.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var builder = WebApplication.CreateBuilder(args);
        /// builder.Services.AddPerformanceMonitor();
        /// </code>
        /// </example>
        public static IServiceCollection AddPerformanceMonitor(this IServiceCollection services)
        {
            services.TryAddSingleton<IMonitor, Monitor>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMetricExporter, AggregationExporter>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMetricExporter, ConsoleExporter>());
            return services;
        }
    }
}
