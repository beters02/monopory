using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#pragma warning disable CA2255

public interface IGameConVar
{
	string Name { get; }
	Type ValueType { get; }
	object BoxedValue { get; set; }
}

public sealed class GameConVar<TValue> : IGameConVar
{
	private readonly Func<TValue> getter;
	private readonly Action<TValue> setter;

	public string Name { get; }
	public Type ValueType => typeof( TValue );

	public TValue Value
	{
		get => getter();
		set => setter( value );
	}

	public object BoxedValue
	{
		get => Value;
		set => Value = (TValue)value;
	}

	public GameConVar( string name, Func<TValue> getter, Action<TValue> setter )
	{
		Name = name;
		this.getter = getter;
		this.setter = setter;
	}
}

public static class GameConVars
{
	private static readonly Dictionary<string, IGameConVar> ConVars = new();

	public static IReadOnlyDictionary<string, IGameConVar> All => ConVars;

	public static GameConVar<TValue> Register<TValue>( GameConVar<TValue> conVar )
	{
		ConVars[conVar.Name] = conVar;
		return conVar;
	}
}

public static class DebugConVar
{
	public const string Name = "debug";
	public static readonly GameConVar<bool> ConVar = new( Name, () => Value, value => Value = value );

	[ModuleInitializer]
	public static void Register()
	{
		GameConVars.Register( ConVar );
	}

	[ConVar( Name )]
	public static bool Value { get; set; } = false;
}
