using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace UnwantedMethodCallsAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnwantedMethodCallAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UnwantedMethodCallAnalyzer";
        public const string Title = "Unwanted Method Call found";
        public const string MessageFormat = "Unwanted method '{0}' called";
        public const string Category = "UnwantedMethodCall";
        public const string ConfigurationFileName = "unwanted_method_calls.json";
        
        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
        
        private static UnwantedMethod[] _unwantedMethodsCache;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(CacheUnwantedMethodsFromConfig);
        }

        private void CacheUnwantedMethodsFromConfig(CompilationStartAnalysisContext context)
        {
            // https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Using%20Additional%20Files.md
            var configurationFile = context.Options.AdditionalFiles.FirstOrDefault(x => x.Path.Contains(ConfigurationFileName));
            var sourceText = configurationFile?.GetText()?.ToString();
            if (sourceText != null)
            {
                var root = SimpleJson.SimpleJson.DeserializeObject<UnwantedMethodCalls>(sourceText);
                _unwantedMethodsCache = root?.UnwantedMethods;
            }

            if (_unwantedMethodsCache != null && _unwantedMethodsCache.Any())
                context.RegisterSyntaxNodeAction(CheckUnwantedMethodCalls, SyntaxKind.InvocationExpression);
        }

        private void CheckUnwantedMethodCalls(SyntaxNodeAnalysisContext context)
        {
            var expressionSyntax = (InvocationExpressionSyntax)context.Node;
            var memberAccessExpression = expressionSyntax.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpression == null) return;

            var memberSymbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, memberAccessExpression).Symbol as IMethodSymbol;
            if (memberSymbol == null) return;

            var currentType = context.ContainingSymbol?.ContainingType.ToString();
            var memberContainingType = memberSymbol.ContainingType.ToString();
            foreach (var unwantedMethod in _unwantedMethodsCache)
            {
                if (unwantedMethod.ExcludeCheckingTypes.Contains(currentType)) continue;
                
                if (memberContainingType == unwantedMethod.TypeNamespace && memberSymbol.Name == unwantedMethod.MethodName)
                {
                    var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), $"{memberContainingType}.{memberSymbol.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private class UnwantedMethodCalls
        {
            public UnwantedMethod[] UnwantedMethods { get; set; }
        }

        private class UnwantedMethod
        {
            public string TypeNamespace { get; set; }

            public string MethodName { get; set; }

            public string[] ExcludeCheckingTypes { get; set; } = { };
        }
    }
}
