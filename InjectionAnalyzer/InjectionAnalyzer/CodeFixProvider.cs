using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Editing;

namespace InjectionAnalyzer
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectionAnalyzerCodeFixProvider)), Shared]
	public class InjectionAnalyzerCodeFixProvider : CodeFixProvider
	{
		private const string title = "Inject dependency";

		public sealed override ImmutableArray<string> FixableDiagnosticIds
		{
			get { return ImmutableArray.Create(InjectionAnalyzer.DiagnosticId); }
		}

		public sealed override FixAllProvider GetFixAllProvider()
		{
			// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
			return WellKnownFixAllProviders.BatchFixer;
		}

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			// TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// Find the type declaration identified by the diagnostic.
			var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().First();

			// Register a code action that will invoke the fix.
			context.RegisterCodeFix(
				CodeAction.Create(
					title: title,
					createChangedDocument: c => InjectDependencyAsync(context.Document, declaration, c),
					equivalenceKey: title),
				diagnostic);
		}

		private async Task<Document> InjectDependencyAsync(Document document, FieldDeclarationSyntax fieldDeclaration, CancellationToken cancellationToken)
		{
			var editor = await DocumentEditor.CreateAsync(document, cancellationToken);

			var fieldIdentifier = fieldDeclaration.Declaration.Variables.First().Identifier;

			var parameter = SyntaxFactory
				.Parameter(SyntaxFactory.Identifier(fieldIdentifier.Text.Substring(1, fieldIdentifier.Text.Length - 1)))
				.WithType(fieldDeclaration.Declaration.Type);

			var left = SyntaxFactory.IdentifierName(fieldIdentifier);
			var right = SyntaxFactory.IdentifierName(parameter.Identifier);

			var assignment = 
				SyntaxFactory.ExpressionStatement(
					SyntaxFactory.AssignmentExpression(
						SyntaxKind.SimpleAssignmentExpression,
						left, 
						right));

			var classDeclaration = fieldDeclaration.Ancestors().First(x => x is ClassDeclarationSyntax) as ClassDeclarationSyntax;

			var constructor = classDeclaration.DescendantNodes().FirstOrDefault(x => x is ConstructorDeclarationSyntax) as ConstructorDeclarationSyntax;
			if(constructor is null)
			{
				constructor = SyntaxFactory
					.ConstructorDeclaration(classDeclaration.Identifier.Text)
					.WithoutTrivia()
					.AddBodyStatements(assignment)
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.AddParameterListParameters(parameter);

				var newClass = classDeclaration.AddMembers(constructor);
				editor.ReplaceNode(classDeclaration, newClass);
			}
			else
			{
				var newConstructor = 
					constructor.AddBodyStatements(assignment)
					.AddParameterListParameters(parameter)
					.WithAdditionalAnnotations(Formatter.Annotation);
				editor.ReplaceNode(constructor, newConstructor);
			}
			
			return editor.GetChangedDocument();
		}
	}
}