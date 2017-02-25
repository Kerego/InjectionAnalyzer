using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using InjectionAnalyzer;

namespace InjectionAnalyzer.Test
{
	[TestClass]
	public class UnitTest : CodeFixVerifier
	{

		//No diagnostics expected to show up
		[TestMethod]
		public void TestMethod1()
		{
			var test = @"";

			VerifyCSharpDiagnostic(test);
		}

		//Diagnostic and CodeFix both triggered and checked for
		[TestMethod]
		public void TestMethod2()
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
		private readonly string _navigationService;
	}
}";
			var expected = new DiagnosticResult
			{
				Id = "InjectionAnalyzer",
				Message = String.Format("Readonly Field '{0}' is injected in none of the constructors.", "_navigationService"),
				Severity = DiagnosticSeverity.Info,
				Locations =
					new[] {
							new DiagnosticResultLocation("Test0.cs", 13, 29)
						}
			};

			VerifyCSharpDiagnostic(test, expected);

			var fixtest = @"
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
		private readonly string _navigationService;

		public TypeName(string navigationService)
		{
			_navigationService = navigationService;
		}
	}
}";
			VerifyCSharpFix(test, fixtest);
		}

		protected override CodeFixProvider GetCSharpCodeFixProvider()
		{
			return new InjectionAnalyzerCodeFixProvider();
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new InjectionAnalyzer();
		}
	}
}