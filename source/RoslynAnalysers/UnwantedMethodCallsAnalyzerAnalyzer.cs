using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalysers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnwantedMethodCallAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UnwantedMethodCallAnalyzer";

        public const string Title = "Unwanted Method Call found";

        /* Example output:
         * Unwanted method 'System.Diagnostics.Process.Start' called
         * <UnwantedReason>
         */
        public const string MessageFormat = "Unwanted method '{0}' called{1}";
        public const string Category = "UnwantedMethodCall";
        public const string ConfigurationFileName = "unwanted_method_calls.json";
        public const string Description = "If this type should be allowed to call this method, please update the '" + ConfigurationFileName + "' ExcludeCheckingTypes array.";

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            true,
            Description);

        static UnwantedMethod[] unwantedMethodsCache = new UnwantedMethod[0];

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(CacheUnwantedMethodsFromConfig);
        }

        void CacheUnwantedMethodsFromConfig(CompilationStartAnalysisContext context)
        {
            // https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Using%20Additional%20Files.md
            var configurationFile = context.Options.AdditionalFiles.FirstOrDefault(x => x.Path.Contains(ConfigurationFileName));
            var sourceText = configurationFile?.GetText()?.ToString();
            if (sourceText != null)
            {
                var root = SimpleJson.DeserializeObject<UnwantedMethodCalls>(sourceText);
                unwantedMethodsCache = root?.UnwantedMethods ?? new UnwantedMethod[0];
            }

            if (unwantedMethodsCache.Any())
                context.RegisterSyntaxNodeAction(CheckUnwantedMethodCalls, SyntaxKind.InvocationExpression);
        }

        void CheckUnwantedMethodCalls(SyntaxNodeAnalysisContext context)
        {
            var expressionSyntax = (InvocationExpressionSyntax)context.Node;
            var memberAccessExpression = expressionSyntax.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpression == null) return;

            var memberSymbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, memberAccessExpression).Symbol as IMethodSymbol;
            if (memberSymbol == null) return;

            var currentType = context.ContainingSymbol?.ContainingType.ToString();
            var memberContainingType = memberSymbol.ContainingType.ToString();
            foreach (var unwantedMethod in unwantedMethodsCache)
            {
                if (unwantedMethod.ExcludeCheckingTypes.Contains(currentType)) continue;

                if (memberContainingType == unwantedMethod.TypeNamespace && memberSymbol.Name == unwantedMethod.MethodName)
                {
                    var fullyQualifiedOffender = $"{memberContainingType}.{memberSymbol.Name}";
                    var unwantedReason = string.IsNullOrWhiteSpace(unwantedMethod.UnwantedReason)
                        ? null
                        : $"\nUnwanted Reason: {unwantedMethod.UnwantedReason}";
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        memberAccessExpression.GetLocation(),
                        fullyQualifiedOffender,
                        unwantedReason,
                        currentType);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        class UnwantedMethodCalls
        {
            public UnwantedMethod[] UnwantedMethods { get; set; } = { };
        }

        class UnwantedMethod
        {
            public string TypeNamespace { get; set; } = "";

            public string MethodName { get; set; } = "";

            public string UnwantedReason { get; set; } = "";

            public string[] ExcludeCheckingTypes { get; set; } = { };
        }
    }
}