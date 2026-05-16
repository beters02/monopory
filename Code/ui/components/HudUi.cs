using System;

namespace Sandbox.ui.components;

public static class HudUi
{
	public static bool IsPurchasableSpace( SpaceDef def )
	{
		return def is not null &&
			def.Price > 0 &&
			(def.Type == SpaceType.Property || def.Type == SpaceType.Railroad || def.Type == SpaceType.Utility);
	}

	public static string GetPropertyColorClass( SpaceDef def )
	{
		if ( def is null )
			return "none";

		if ( def.ColorGroup != ColorGroup.None )
			return def.ColorGroupClass;

		return GetSpecialPropertyColorGroup( def.Type );
	}

	public static string GetSpecialPropertyColorGroup( SpaceType type )
	{
		return type switch
		{
			SpaceType.Railroad => "railroad",
			SpaceType.Utility => "utility",
			_ => "none"
		};
	}

	public static int GetPlayersHash( GameController game )
	{
		if ( game?.Players is null )
			return 0;

		var hash = new HashCode();

		foreach ( var player in game.Players )
		{
			hash.Add( player?.OwnerId ?? 0 );
			hash.Add( player?.PlayerName ?? "" );
			hash.Add( player?.Money ?? 0 );
			hash.Add( player?.SpaceIndex ?? 0 );
			hash.Add( player?.IsAssigned ?? false );
			hash.Add( player?.IsBankrupt ?? false );
			hash.Add( player?.IsReady ?? false );
		}

		return hash.ToHashCode();
	}

	public static int GetPropertyOwnersHash( GameController game )
	{
		if ( game is null )
			return 0;

		var hash = new HashCode();

		for ( var spaceIndex = 0; spaceIndex < 40; spaceIndex++ )
		{
			hash.Add( spaceIndex );
			hash.Add( game.GetOwnerIndexForSpace( spaceIndex ) );
		}

		return hash.ToHashCode();
	}

	public static int GetMortgagedPropertiesHash( GameController game )
	{
		if ( game?.MortgagedProperties is null )
			return 0;

		var hash = new HashCode();

		foreach ( var entry in game.MortgagedProperties.OrderBy( entry => entry.Key ) )
		{
			hash.Add( entry.Key );
			hash.Add( entry.Value );
		}

		return hash.ToHashCode();
	}
}
