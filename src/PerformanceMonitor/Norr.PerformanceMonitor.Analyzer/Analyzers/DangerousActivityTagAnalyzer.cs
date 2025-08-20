// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;


namespace Norr.PerformanceMonitor.Analyzers;

/// <summary>
/// Roslyn analyzer that detects usage of potentially dangerous or high-cardinality
/// tag keys when calling <c>Activity.SetTag</c>.
/// </summary>
/// <remarks>
/// <para>
/// Certain tag keys (e.g., <c>http.url</c>, <c>http.path</c>) can drastically increase
/// metric cardinality or leak personally identifiable information (PII) when used
/// directly in telemetry. This analyzer inspects calls to
/// 
/// if any banned key is used as the first argument.
/// </para>
/// <para>
/// The banned keys list includes:
/// <list type="bullet">
///   <item><description><c>http.path</c></description></item>
///   <item><description><c>http.target</c></description></item>
///   <item><description><c>http.url</c></description></item>
///   <item><description><c>request.path</c></description></item>
///   <item><description><c>url</c></description></item>
///   <item><description><c>norr.http.request_path</c></description></item>
/// </list>
/// </para>
/// <para>
/// <b>Why it matters:</b> Tagging with raw URLs or paths without scrubbing can create
/// unbounded metric dimensions (cardinality explosion) and expose sensitive values
/// in logs, traces, or exported metrics. Use <c>http.route</c> or scrubbed values instead.
/// </para>
/// <para>
/// <b>Thread safety:</b> Roslyn analyzers are instantiated once per compilation and must be thread-safe.
/// This analyzer contains only immutable state.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DangerousActivityTagAnalyzer : DiagnosticAnalyzer
{
    // Keys that should not be used in Activity.SetTag due to cardinality/PII risk.
    private static readonly string[] _bannedKeys =
    [
        "http.path", "http.target", "http.url", "request.path", "url", "norr.http.request_path"
    ];

    private static readonly DiagnosticDescriptor _rule = new(
        id: DiagnosticIds.DangerousActivityTag,
        title: "High-cardinality or sensitive tag key used",
        messageFormat: "Tag key '{0}' is potentially high-cardinality or sensitive; avoid using it as a tag. Prefer 'http.route' or scrubbed values.",
        category: "Norr.Observability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Avoid using raw path/url/user-identifying tags which explode cardinality or leak PII."
    );

    /// <summary>
    /// Gets the diagnostics that this analyzer is capable of producing.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    /// <summary>
    /// Initializes the analyzer by registering syntax node actions.
    /// </summary>
    /// <param name="context">The analysis context provided by Roslyn.</param>
    /// <remarks>
    /// This method is called once per compilation. Here, we:
    /// <list type="bullet">
    ///   <item><description>Disable analysis for generated code.</description></item>
    ///   <item><description>Enable concurrent execution.</description></item>
    ///   <item><description>Register an action for <see cref="SyntaxKind.InvocationExpression"/> nodes to detect <c>Activity.SetTag</c> usage.</description></item>
    /// </list>
    /// </remarks>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>
    /// Analyzes <see cref="InvocationExpressionSyntax"/> nodes to detect calls to
    /// <c>Activity.SetTag</c> with banned tag keys.
    /// </summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <remarks>
    /// <para>
    /// The analyzer checks:
    /// <list type="number">
    ///   <item><description>The member name is exactly <c>SetTag</c>.</description></item>
    ///   <item><description>The first argument is a string literal.</description></item>
    ///   <item><description>The string literal value is in the banned keys list.</description></item>
    /// </list>
    /// If all conditions match, a warning diagnostic is reported.
    /// </para>
    /// </remarks>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Must be Activity.SetTag(...)
        if (invocation.Expression is not MemberAccessExpressionSyntax maes ||
            maes.Name.Identifier.Text is not "SetTag")
        {
            return;
        }

        // First argument must be a string literal key
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0)
            return;

        if (args[0].Expression is not LiteralExpressionSyntax firstArg ||
            !firstArg.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        var key = firstArg.Token.ValueText;
        if (_bannedKeys.Contains(key))
        {
            context.ReportDiagnostic(Diagnostic.Create(_rule, firstArg.GetLocation(), key));
        }
    }
}
