using System;
using System.Collections.Generic;

public sealed class GameConVar
{
	public string Name { get; }
	public MemberDescription Member { get; }
	public ConVarAttribute Attribute { get; }

	public Type ValueType
	{
		get
		{
			if ( Member is PropertyDescription property )
				return property.PropertyType;

			if ( Member is FieldDescription fieldDescription )
				return fieldDescription.FieldType;

			return typeof( object );
		}
	}

	public object BoxedValue
	{
		get
		{
			if ( Member is PropertyDescription property )
				return property.GetValue( null );

			if ( Member is FieldDescription fieldDescription )
				return fieldDescription.GetValue( null );

			return null;
		}
		set
		{
			if ( Member is PropertyDescription property )
			{
				property.SetValue( null, value );
				return;
			}

			if ( Member is FieldDescription fieldDescription )
				fieldDescription.SetValue( null, value );
		}
	}

	public GameConVar( string name, MemberDescription member, ConVarAttribute attribute )
	{
		Name = name;
		Member = member;
		Attribute = attribute;
	}
}

public static class GameConVars
{
	private static readonly Dictionary<string, GameConVar> ConVars = new();
	private static bool registered;

	public static IReadOnlyDictionary<string, GameConVar> All
	{
		get
		{
			EnsureRegistered();
			return ConVars;
		}
	}

	private static void EnsureRegistered()
	{
		if ( registered )
			return;

		registered = true;

		foreach ( var type in Game.TypeLibrary.GetTypes() )
		{
			foreach ( var member in type.Members )
			{
				if ( !member.IsStatic )
					continue;

				var attribute = member.GetCustomAttribute<ConVarAttribute>();
				if ( attribute is null )
					continue;

				var name = attribute.Name;
				if ( string.IsNullOrWhiteSpace( name ) )
					name = member.Name;

				ConVars[name] = new GameConVar( name, member, attribute );
			}
		}
	}
}

public static class DebugConVar
{
	public const string Name = "debug";

	[ConVar( Name )]
	public static bool Value { get; set; } = false;
}
