using System;
using System.Reflection;
using Sandbox;

public partial class MonopolyApp : Component
{
#if STANDALONE
	private static bool sceneTraceStaticCollectionsInitialized;

	private static void InitializeSceneTraceStaticCollections()
	{
		if ( sceneTraceStaticCollectionsInitialized )
			return;

		sceneTraceStaticCollectionsInitialized = true;

		InitializeSceneTraceStaticCollection( "_traceIgnoreSingle" );
		InitializeSceneTraceStaticCollection( "_traceIgnoreHierarchy" );
	}

	private static void InitializeSceneTraceStaticCollection( string fieldName )
	{
		const BindingFlags StaticFieldFlags = BindingFlags.NonPublic | BindingFlags.Static;

		try
		{
			var field = typeof( SceneTrace ).GetField( fieldName, StaticFieldFlags );
			if ( field is null || !IsImmutableArrayType( field.FieldType ) )
				return;

			var value = field.GetValue( null );
			var isDefault = field.FieldType.GetProperty( "IsDefault" )?.GetValue( value ) as bool?;
			if ( isDefault != true )
				return;

			var emptyValue = field.FieldType.GetField( "Empty", BindingFlags.Public | BindingFlags.Static )?.GetValue( null );
			if ( emptyValue is null )
				return;

			field.SetValue( null, emptyValue );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"Could not initialize SceneTrace.{fieldName} for standalone reflection cleanup: {exception.Message}" );
		}
	}

	private static bool IsImmutableArrayType( Type type )
	{
		return type.IsGenericType &&
			type.GetGenericTypeDefinition().FullName == "System.Collections.Immutable.ImmutableArray`1";
	}
#else
	private static void InitializeSceneTraceStaticCollections()
	{
	}
#endif
}
