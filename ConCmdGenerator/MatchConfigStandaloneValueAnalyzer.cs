using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Monopory.Analyzers;

[DiagnosticAnalyzer( LanguageNames.CSharp )]
public sealed class MatchConfigStandaloneValueAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor StandaloneValueTypeMismatch = new(
		id: "MONO001",
		"StandaloneValue type does not match MatchConfig property type",
		"StandaloneValue type '{0}' does not match property '{1}' type '{2}'",
		"MatchConfig",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true );

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create( StandaloneValueTypeMismatch );

	public override void Initialize( AnalysisContext context )
	{
		context.ConfigureGeneratedCodeAnalysis( GeneratedCodeAnalysisFlags.None );
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction( AnalyzeProperty, SyntaxKind.PropertyDeclaration );
	}

	private static void AnalyzeProperty( SyntaxNodeAnalysisContext context )
	{
		var property = (PropertyDeclarationSyntax)context.Node;
		var attribute = FindMatchConfigOptionAttribute( property );
		if ( attribute is null )
			return;

		var standaloneValueArgument = FindStandaloneValueArgument( attribute );
		if ( standaloneValueArgument is null )
			return;

		var propertySymbol = context.SemanticModel.GetDeclaredSymbol( property, context.CancellationToken );
		if ( propertySymbol is null )
			return;

		var standaloneValueTypeInfo = context.SemanticModel.GetTypeInfo( standaloneValueArgument.Expression, context.CancellationToken );
		var standaloneValueType = standaloneValueTypeInfo.Type ?? standaloneValueTypeInfo.ConvertedType;
		if ( standaloneValueType is null )
			return;

		if ( SymbolEqualityComparer.Default.Equals( propertySymbol.Type, standaloneValueType ) )
			return;

		var diagnostic = Diagnostic.Create(
			StandaloneValueTypeMismatch,
			standaloneValueArgument.Expression.GetLocation(),
			standaloneValueType.ToDisplayString( SymbolDisplayFormat.MinimallyQualifiedFormat ),
			propertySymbol.Name,
			propertySymbol.Type.ToDisplayString( SymbolDisplayFormat.MinimallyQualifiedFormat ) );

		context.ReportDiagnostic( diagnostic );
	}

	private static AttributeSyntax? FindMatchConfigOptionAttribute( PropertyDeclarationSyntax property )
	{
		foreach ( var attributeList in property.AttributeLists )
		{
			foreach ( var attribute in attributeList.Attributes )
			{
				var name = attribute.Name.ToString();
				if ( name is "MatchConfigOption" or "MatchConfigOptionAttribute" )
					return attribute;

				if ( name.EndsWith( ".MatchConfigOption" ) || name.EndsWith( ".MatchConfigOptionAttribute" ) )
					return attribute;
			}
		}

		return null;
	}

	private static AttributeArgumentSyntax? FindStandaloneValueArgument( AttributeSyntax attribute )
	{
		if ( attribute.ArgumentList is null )
			return null;

		foreach ( var argument in attribute.ArgumentList.Arguments )
		{
			if ( argument.NameEquals?.Name.Identifier.ValueText == "StandaloneValue" )
				return argument;
		}

		return null;
	}
}
