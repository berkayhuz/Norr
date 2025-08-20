// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Defines OpenTelemetry <em>resource attributes</em> that describe the service
/// emitting metrics and traces.
/// </summary>
/// <remarks>
/// <para>
/// OpenTelemetry <b>Resource</b> attributes provide identifying information about
/// the service instance producing telemetry. This information is attached to all
/// metrics, traces, and logs and is used by backends to group and filter data.
/// </para>
/// <para>
/// These values typically map to semantic conventions:
/// <list type="bullet">
///   <item>
///     <term><c>service.name</c></term>
///     <description>Logical name of the service (required in many backends).</description>
///   </item>
///   <item>
///     <term><c>service.version</c></term>
///     <description>Version of the deployed service.</description>
///   </item>
///   <item>
///     <term><c>deployment.environment</c></term>
///     <description>Deployment environment name (for example, <c>production</c>, <c>staging</c>).</description>
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class ResourceOptions
{
    /// <summary>
    /// Gets the logical name of the service emitting telemetry.
    /// </summary>
    /// <value>
    /// A short, descriptive name for the service. This value populates the
    /// <c>service.name</c> resource attribute in OpenTelemetry.
    /// </value>
    /// <remarks>
    /// Required by most OpenTelemetry backends. Choose a stable name that remains
    /// consistent across deployments.
    /// </remarks>
    public string? ServiceName
    {
        get; init;
    }

    /// <summary>
    /// Gets the version of the service emitting telemetry.
    /// </summary>
    /// <value>
    /// A string representing the service version. This value populates the
    /// <c>service.version</c> resource attribute in OpenTelemetry.
    /// </value>
    /// <remarks>
    /// Often set from the application’s assembly version or build metadata.
    /// </remarks>
    public string? ServiceVersion
    {
        get; init;
    }

    /// <summary>
    /// Gets the name of the deployment environment for the service.
    /// </summary>
    /// <value>
    /// A string representing the deployment environment. This value populates the
    /// <c>deployment.environment</c> resource attribute in OpenTelemetry.
    /// </value>
    /// <remarks>
    /// Typical values include <c>production</c>, <c>staging</c>, <c>dev</c>, or
    /// <c>test</c>. Useful for filtering and grouping metrics in telemetry backends.
    /// </remarks>
    public string? DeploymentEnvironment
    {
        get; init;
    }
}
