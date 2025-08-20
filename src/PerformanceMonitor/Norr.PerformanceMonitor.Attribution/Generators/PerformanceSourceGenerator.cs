// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Norr.PerformanceMonitor.Attribution.Generators;

/// <summary>
/// Roslyn incremental source generator that discovers members annotated with
/// <c>[MeasurePerformance]</c> and emits wrapper/extension methods (and optional factory helpers)
/// that measure execution via <c>IMonitor</c>.
/// </summary>
/// <remarks>
/// <para>
/// This generator scans method and constructor declarations in the compilation and groups matches
/// by containing type. For each group it emits two generated, internal static types:
/// <list type="bullet">
/// <item><description><c>*_PerfExtensions</c>: extension methods for instance members.</description></item>
///
/// <item><description><c>*_PerfWrappers</c>: static helpers for static members and constructor factories.</description></item>
/// </list>
/// </para>
/// <para>
/// The output filenames end with <c>.g.cs</c> and are deterministic per containing type.
/// </para>
/// <para><b>Thread-safety:</b> Roslyn generators are created once per compilation and invoked by the compiler.
/// This implementation is stateless and thread-safe.</para>
/// </remarks>
/// <example>
/// Mark a method:
/// <code>
/// [MeasurePerformance]
/// public async Task DoWorkAsync() { /* ... */ }
/// </code>
/// The generator emits a <c>DoWorkAsyncWithPerf(..., IMonitor monitor)</c> wrapper
/// that opens a monitoring scope and calls the original method.
/// </example>
[Generator(LanguageNames.CSharp)]
public sealed class PerformanceSourceGenerator : IIncrementalGenerator
{
    private const string AttributeFqn = "Norr.PerformanceMonitor.Attribution.Attributes.MeasurePerformanceAttribute";

    /// <summary>
    /// Configures the incremental pipeline that discovers attributed methods/constructors
    /// and registers source outputs for the generated wrapper and extension types.
    /// </summary>
    /// <param name="context">The incremental generator initialization context provided by Roslyn.</param>
    /// <remarks>
    /// The pipeline:
    /// <list type="number">
    /// <item><description>Filters method/constructor syntax nodes that have attributes.</description></item>
    /// <item><description>Binds to semantic symbols and keeps only those with <c>[MeasurePerformance]</c>.</description></item>
    /// <item><description>Groups by containing type and generates two companion classes when applicable.</description></item>
    /// </list>
    /// </remarks>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            // Method or Ctor + has any attribute => candidate
            (node, _) =>
                (node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0) ||
                (node is ConstructorDeclarationSyntax c && c.AttributeLists.Count > 0),
            (ctx, _) =>
            {
                var model = ctx.SemanticModel;
                if (ctx.Node is MethodDeclarationSyntax mDecl)
                {
                    var sym = model.GetDeclaredSymbol(mDecl) as IMethodSymbol;
                    return (Node: (SyntaxNode)mDecl, Sym: sym);
                }
                else
                {
                    var cDecl = (ConstructorDeclarationSyntax)ctx.Node;
                    var sym = model.GetDeclaredSymbol(cDecl) as IMethodSymbol; // MethodKind.Constructor
                    return (Node: (SyntaxNode)cDecl, Sym: sym);
                }
            })
            .Where(p => p.Sym is not null && HasPerfAttribute(p.Sym!))
            .Where(p => p.Sym!.ContainingType is INamedTypeSymbol);

        context.RegisterSourceOutput(candidates.Collect(), (spc, items) =>
        {
            var grouped = items
                .Select(i => (Type: (INamedTypeSymbol)i.Sym!.ContainingType!, Method: i.Sym!, i.Node))
                .GroupBy(x => (ISymbol)x.Type, SymbolEqualityComparer.Default);

            foreach (var group in grouped)
            {
                var typeSymbol = (INamedTypeSymbol)group.Key;
                var methods = group.Select(x => x.Method).ToArray();

                string? ns = (typeSymbol.ContainingNamespace is { IsGlobalNamespace: false })
                    ? typeSymbol.ContainingNamespace.ToDisplayString()
                    : null;

                string baseTypeName = BuildContainingTypeName(typeSymbol);
                string extClassName = $"{baseTypeName}_PerfExtensions";
                string wrapClassName = $"{baseTypeName}_PerfWrappers";

                var sbExt = new StringBuilder();
                var sbWrap = new StringBuilder();
                bool extHas = false, wrapHas = false;

                AppendHeader(sbExt, ns, extClassName);
                AppendHeader(sbWrap, ns, wrapClassName);

                foreach (var m in methods)
                {
                    if (!IsSupported(m))
                        continue;

                    // Method or ctor?
                    if (m.MethodKind == MethodKind.Constructor)
                    {
                        var ctorCode = GenerateCtorFactory(typeSymbol, m);
                        if (ctorCode is null)
                            continue;
                        // Factories (ctors) always go to the wrapper class (not extensions)
                        sbWrap.AppendLine(ctorCode);
                        wrapHas = true;
                        continue;
                    }

                    string? code = GenerateMethodWrapper(m, typeSymbol);
                    if (code is null)
                        continue;

                    if (m.IsStatic)
                    {
                        sbWrap.AppendLine(code);
                        wrapHas = true;
                    }
                    else
                    {
                        sbExt.AppendLine(code);
                        extHas = true;
                    }
                }

                sbExt.AppendLine("}");
                sbWrap.AppendLine("}");

                if (extHas)
                    spc.AddSource($"{extClassName}.g.cs", SourceText.From(sbExt.ToString(), Encoding.UTF8));
                if (wrapHas)
                    spc.AddSource($"{wrapClassName}.g.cs", SourceText.From(sbWrap.ToString(), Encoding.UTF8));
            }
        });
    }

    private static void AppendHeader(StringBuilder sb, string? ns, string className)
    {
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Norr.PerformanceMonitor.Abstractions;");
        if (!string.IsNullOrEmpty(ns))
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }
        sb.Append("internal static class ").Append(className).AppendLine();
        sb.AppendLine("{");
    }

    private static bool HasPerfAttribute(IMethodSymbol method)
        => method.GetAttributes().Any(a =>
               a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Equals("global::" + AttributeFqn, StringComparison.Ordinal) == true
            || a.AttributeClass?.ToDisplayString()
                    .Equals(AttributeFqn, StringComparison.Ordinal) == true);

    private static bool IsSupported(IMethodSymbol method)
    {
        if (method.MethodKind is MethodKind.Constructor)
            return true;

        if (method.ReturnsVoid && method.IsAsync)
            return false; // no async void

        return method.MethodKind == MethodKind.Ordinary;
    }

    // -------------------- Method wrapper (Task/ValueTask aware) ----------------------------

    private static string? GenerateMethodWrapper(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        bool isStatic = method.IsStatic;

        // Type parameters (both containing type and method)
        string typeParamsDeclForContaining = BuildTypeParamsDecl(containingType.TypeParameters);
        string typeParamsDeclForMethod = BuildTypeParamsDecl(method.TypeParameters);
        string combinedTypeParamsDecl = CombineTypeParams(typeParamsDeclForContaining, typeParamsDeclForMethod);

        string constraintsType = BuildTypeConstraints(containingType.TypeParameters);
        string constraintsMethod = BuildConstraintsForMethod(method);
        string combinedConstraints = CombineConstraints(constraintsType, constraintsMethod);

        string typeDisplay = containingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        string methodName = method.Name;

        var (paramList, argList) = BuildParameterAndArgumentLists(
            method,
            includeThis: !isStatic,
            extensionThisType: typeDisplay);

        var returnTypeSymbol = method.ReturnType;
        string returnType = returnTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var (isTaskLike, isTaskGeneric, isValueTask, isValueTaskGeneric) = InspectTaskLike(returnTypeSymbol);

        string callTarget = isStatic ? typeDisplay : "@this";
        string genericCallPart = BuildGenericArgsForMethod(method);

        string callExpr = $"{callTarget}.{methodName}{genericCallPart}({argList})";
        string wrapperName = methodName + "WithPerf";

        var sb = new StringBuilder();

        if (isTaskLike)
            sb.Append("    public static async ").Append(returnType).Append(' ').Append(wrapperName);
        else
            sb.Append("    public static ").Append(returnType).Append(' ').Append(wrapperName);

        if (combinedTypeParamsDecl.Length > 0)
            sb.Append(combinedTypeParamsDecl);

        sb.Append('(').Append(paramList).Append(", IMonitor monitor)");
        if (!string.IsNullOrEmpty(combinedConstraints))
            sb.Append(' ').Append(combinedConstraints);
        sb.AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine($"        using var __scope = monitor.Begin(\"{containingType.Name}.{methodName}\");");

        if (isTaskLike)
        {
            if (isValueTask && !isValueTaskGeneric)
            {
                sb.AppendLine($"        await {callExpr}.ConfigureAwait(false);");
                sb.AppendLine("        return;");
            }
            else if (isValueTaskGeneric)
            {
                sb.AppendLine($"        return await {callExpr}.ConfigureAwait(false);");
            }
            else if (!isTaskGeneric)
            {
                sb.AppendLine($"        await {callExpr}.ConfigureAwait(false);");
                sb.AppendLine("        return;");
            }
            else
            {
                sb.AppendLine($"        return await {callExpr}.ConfigureAwait(false);");
            }
        }
        else
        {
            if (method.ReturnsVoid)
                sb.AppendLine($"        {callExpr};");
            else
                sb.AppendLine($"        return {callExpr};");
        }

        sb.AppendLine("    }");
        return sb.ToString();
    }

    private static (string paramList, string argList) BuildParameterAndArgumentLists(
        IMethodSymbol method, bool includeThis, string extensionThisType)
    {
        var paramSb = new StringBuilder();
        var argSb = new StringBuilder();
        bool first = true;

        if (includeThis)
        {
            paramSb.Append("this ").Append(extensionThisType).Append(" @this");
            first = false;
        }

        foreach (var p in method.Parameters)
        {
            if (!first)
            {
                paramSb.Append(", ");
                argSb.Append(", ");
            }
            first = false;

            string modifier = p.RefKind switch
            {
                RefKind.None => p.IsParams ? "params " : string.Empty,
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => string.Empty
            };

            string typeName = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            paramSb.Append(modifier).Append(typeName).Append(' ').Append(p.Name);

            string argMod = p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => string.Empty
            };
            argSb.Append(argMod).Append(p.Name);
        }

        return (paramSb.ToString(), argSb.ToString());
    }

    private static string BuildConstraintsForMethod(IMethodSymbol m)
    {
        if (m.TypeParameters.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var tp in m.TypeParameters)
        {
            var cons = new List<string>();
            foreach (var c in tp.ConstraintTypes)
                cons.Add(c.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            if (tp.HasConstructorConstraint)
                cons.Add("new()");
            if (tp.HasReferenceTypeConstraint)
                cons.Add(tp.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
            if (tp.HasUnmanagedTypeConstraint)
                cons.Add("unmanaged");
            if (tp.HasValueTypeConstraint)
                cons.Add("struct");
            if (tp.HasNotNullConstraint)
                cons.Add("notnull");

            if (cons.Count > 0)
                sb.Append(" where ").Append(tp.Name).Append(" : ").Append(string.Join(", ", cons));
        }
        return sb.ToString();
    }

    private static string BuildTypeConstraints(ImmutableArray<ITypeParameterSymbol> tps)
    {
        if (tps.Length == 0)
            return string.Empty;
        var sb = new StringBuilder();
        foreach (var tp in tps)
        {
            var cons = new List<string>();
            foreach (var c in tp.ConstraintTypes)
                cons.Add(c.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            if (tp.HasConstructorConstraint)
                cons.Add("new()");
            if (tp.HasReferenceTypeConstraint)
                cons.Add(tp.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
            if (tp.HasUnmanagedTypeConstraint)
                cons.Add("unmanaged");
            if (tp.HasValueTypeConstraint)
                cons.Add("struct");
            if (tp.HasNotNullConstraint)
                cons.Add("notnull");

            if (cons.Count > 0)
                sb.Append(" where ").Append(tp.Name).Append(" : ").Append(string.Join(", ", cons));
        }
        return sb.ToString();
    }

    private static string BuildTypeParamsDecl(ImmutableArray<ITypeParameterSymbol> tps)
        => tps.Length > 0 ? "<" + string.Join(", ", tps.Select(tp => tp.Name)) + ">" : string.Empty;

    private static string CombineTypeParams(string a, string b)
    {
        if (string.IsNullOrEmpty(a))
            return b;
        if (string.IsNullOrEmpty(b))
            return a;
        // "<A>" + "<B>" -> "<A,B>"
        return "<" + a.Trim('<', '>') + "," + b.Trim('<', '>') + ">";
    }

    private static string CombineConstraints(string a, string b)
        => string.IsNullOrEmpty(a) ? b : (string.IsNullOrEmpty(b) ? a : a + b);

    private static (bool isTaskLike, bool isTaskGeneric, bool isValueTask, bool isValueTaskGeneric) InspectTaskLike(ITypeSymbol type)
    {
        var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (name == "global::System.Threading.Tasks.Task")
            return (true, false, false, false);
        if (name.StartsWith("global::System.Threading.Tasks.Task<", StringComparison.Ordinal))
            return (true, true, false, false);
        if (name == "global::System.Threading.Tasks.ValueTask")
            return (true, false, true, false);
        if (name.StartsWith("global::System.Threading.Tasks.ValueTask<", StringComparison.Ordinal))
            return (true, true, true, true);

        return (false, false, false, false);
    }

    private static string BuildGenericArgsForMethod(IMethodSymbol method)
        => method.TypeParameters.Length > 0
            ? "<" + string.Join(", ", method.TypeParameters.Select(tp => tp.Name)) + ">"
            : string.Empty;

    private static string BuildContainingTypeName(INamedTypeSymbol type)
    {
        var stack = new Stack<string>();
        INamedTypeSymbol? t = type;
        while (t is not null)
        {
            var part = t.Name;
            if (t.TypeParameters.Length > 0)
                part += "_" + string.Join("_", t.TypeParameters.Select(_ => "T"));
            stack.Push(part);
            t = t.ContainingType;
        }
        return string.Join("_", stack);
    }

    // -------------------- Constructor factory wrapper -----------------------------------------

    private static string? GenerateCtorFactory(INamedTypeSymbol containingType, IMethodSymbol ctor)
    {
        // Ctor return type: containing type
        string typeDisplay = containingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        string typeParamsDeclForContaining = BuildTypeParamsDecl(containingType.TypeParameters);
        string constraintsType = BuildTypeConstraints(containingType.TypeParameters);

        // Parameter list
        var (paramList, argList) = BuildCtorParamAndArgLists(ctor);

        string wrapperName = "NewWithPerf";

        var sb = new StringBuilder();

        sb.Append("    public static ").Append(typeDisplay).Append(' ').Append(wrapperName);
        if (!string.IsNullOrEmpty(typeParamsDeclForContaining))
            sb.Append(typeParamsDeclForContaining);

        sb.Append('(').Append(paramList).Append(", IMonitor monitor)");
        if (!string.IsNullOrEmpty(constraintsType))
            sb.Append(' ').Append(constraintsType);

        sb.AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine($"        using var __scope = monitor.Begin(\"{containingType.Name}.ctor\");");
        sb.AppendLine($"        return new {typeDisplay}({argList});");
        sb.AppendLine("    }");

        return sb.ToString();
    }

    private static (string paramList, string argList) BuildCtorParamAndArgLists(IMethodSymbol ctor)
    {
        var paramSb = new StringBuilder();
        var argSb = new StringBuilder();
        bool first = true;

        foreach (var p in ctor.Parameters)
        {
            if (!first)
            {
                paramSb.Append(", ");
                argSb.Append(", ");
            }
            first = false;

            string modifier = p.RefKind switch
            {
                RefKind.None => p.IsParams ? "params " : string.Empty,
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => string.Empty
            };

            string typeName = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            paramSb.Append(modifier).Append(typeName).Append(' ').Append(p.Name);

            string argMod = p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => string.Empty
            };
            argSb.Append(argMod).Append(p.Name);
        }

        return (paramSb.ToString(), argSb.ToString());
    }
}
