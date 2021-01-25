using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using NUnit.Framework;
using Octopus.UnwantedMethodCallsAnalyzer;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.UnwantedMethodCallsAnalyzer.UnwantedMethodCallAnalyzer>;
using static Microsoft.CodeAnalysis.Testing.DiagnosticResult;

namespace Tests
{
    public class UnwantedMethodCallAnalyzerTest
    {
        const string AdditionalFileText = @"
{
  ""UnwantedMethods"": [
    {
      ""TypeNamespace"": ""System.Diagnostics.Process"",
      ""MethodName"": ""Start"",
      ""UnwantedReason"": ""This would be bad to call""
      ""ExcludeCheckingTypes"": [
        ""ConsoleApplication1.ShouldBeIgnored""
      ]
    }
  ]
}
";

        static readonly (string AdditionalFileName, string AdditionalFileText)[] AdditionalFiles =
        {
            (UnwantedMethodCallAnalyzer.ConfigurationFileName, AdditionalFileText)
        };

        [Test]
        public async Task EmptySourceSucceeds()
        {
            var test = @"";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SourceWithBadCallsFails()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class TypeName
    {
        public TypeName()
        {
            {|#0:Process.Start|}(new ProcessStartInfo(""constructor"")
            {
                Arguments = ""commit -m message && exec maliciousProcess""
            });
        }
        
        void BadMethod()
        {
            {|#1:Process.Start|}(new ProcessStartInfo(""testMethod"")
            {
                Arguments = ""commit -m message && exec maliciousProcess""
            });
        }

        void GoodMethod()
        {
        }
    }

    class ShouldBeIgnored
    {        
        void BadMethod()
        {
            Process.Start(new ProcessStartInfo(""testMethod"")
            {
                Arguments = ""commit -m message && exec maliciousProcess""
            });
        }
    }
}";

            var expectedMessage = UnwantedMethodCallAnalyzer.MessageFormat
                .Replace("{0}", "System.Diagnostics.Process.Start")
                .Replace("{1}", "\nUnwanted Reason: This would be bad to call");
            var expectedRule = new DiagnosticDescriptor(UnwantedMethodCallAnalyzer.DiagnosticId,
                UnwantedMethodCallAnalyzer.Title,
                expectedMessage,
                UnwantedMethodCallAnalyzer.Category,
                DiagnosticSeverity.Error,
                true);
            var result1 = new DiagnosticResult(expectedRule).WithLocation(0);
            var result2 = new DiagnosticResult(expectedRule).WithLocation(1);

            await VerifyWithAdditionalFiles(
                test,
                AdditionalFiles,
                result1,
                result2
            );
        }

        [Test]
        public async Task SourceWithNoUnwantedCallsSucceeds()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class TypeName
    {
        public TypeName()
        {
        }

        void GoodMethod()
        {
        }
    }
}";

            await VerifyWithAdditionalFiles(test, AdditionalFiles);
        }

        [Test]
        public async Task EmptyJsonAdditionalFileTextSucceeds()
        {
            var emptyJson = "{}";
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class TypeName
    {            
        void BadMethod()
        {
            Process.Start(new ProcessStartInfo(""testMethod"")
            {
                Arguments = ""commit -m message && exec maliciousProcess""
            });
        }
    }
}";

            var additionalFiles = new[] { (UnwantedMethodCallAnalyzer.ConfigurationFileName, additionalFileText: emptyJson) };
            await VerifyWithAdditionalFiles(test, additionalFiles);
        }

        async Task VerifyWithAdditionalFiles(string source,
            (string ConfigurationFileName, string additionalFileText)[] additionalFiles,
            params DiagnosticResult[] expectedDiagnostics)
        {
            var test = new CSharpAnalyzerTest<UnwantedMethodCallAnalyzer, NUnitVerifier>();
            test.TestState.Sources.Add(source);

            foreach (var diag in expectedDiagnostics)
                test.TestState.ExpectedDiagnostics.Add(diag);

            foreach (var file in additionalFiles)
                test.TestState.AdditionalFiles.Add(file);

            await test.RunAsync();
        }
    }
}