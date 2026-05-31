using System;
using System.Globalization;
using Sandbox;

public readonly record struct TokenPhysicsState( Vector3 Position, Vector3 Velocity, float StartedAt )
{
	public string Key => $"{Position.x},{Position.y},{Position.z}|{Velocity.x},{Velocity.y},{Velocity.z}|{StartedAt}";
}

public sealed partial class GameController : Component
{
	private const float MaxReplicatedTokenThrowSpeed = 1200f;

	public bool TryGetTokenPhysicsState( int playerIndex, out TokenPhysicsState state, out string key )
	{
		state = default;
		key = null;

		if ( playerIndex < 0 || TokenPhysicsStates is null )
			return false;

		if ( !TokenPhysicsStates.TryGetValue( playerIndex, out var payload ) || string.IsNullOrWhiteSpace( payload ) )
			return false;

		if ( !TryParseTokenPhysicsState( payload, out state ) )
			return false;

		key = payload;
		return true;
	}

	public void PublishTokenPhysicsState( int playerIndex, Vector3 position, Vector3 velocity )
	{
		if ( !Networking.IsHost || playerIndex < 0 || playerIndex >= Players.Count )
			return;

		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || !player.IsAssigned )
			return;

		velocity = ClampTokenThrowVelocity( velocity );
		if ( velocity.Length <= 0f )
			return;

		TokenPhysicsStates[playerIndex] = FormatTokenPhysicsState( new TokenPhysicsState( position, velocity, Time.Now ) );
	}

	public void ClearTokenPhysicsState( int playerIndex )
	{
		if ( !Networking.IsHost || playerIndex < 0 )
			return;

		TokenPhysicsStates.Remove( playerIndex );
	}

	private bool CanControlTokenPhysics( Connection caller, int playerIndex )
	{
		var player = Players.ElementAtOrDefault( playerIndex );
		if ( player is null || !player.IsAssigned || player.IsBankrupt )
			return false;

		if ( !CanPlayerThrowToken( playerIndex ) )
			return false;

		if ( Networking.IsHost && caller == Connection.Local )
			return true;

		return caller is not null && player.OwnerId == caller.SteamId;
	}

	public bool CanLocalPlayerThrowToken( PlayerState player )
	{
		var playerIndex = GetPlayerIndex( player );
		return CanPlayerThrowToken( playerIndex );
	}

	private bool CanPlayerThrowToken( int playerIndex )
	{
		if ( playerIndex < 0 )
			return false;

		if ( !RestrictPieceThrowToCurrentTurn )
			return true;

		if ( MatchState != MatchLifecycleState.InGame )
			return false;

		return CurrentPlayerIndex == playerIndex;
	}

	private static Vector3 ClampTokenThrowVelocity( Vector3 velocity )
	{
		if ( velocity.Length <= MaxReplicatedTokenThrowSpeed )
			return velocity;

		return velocity.Normal * MaxReplicatedTokenThrowSpeed;
	}

	private static string FormatTokenPhysicsState( TokenPhysicsState state )
	{
		return string.Join(
			"|",
			FormatVector( state.Position ),
			FormatVector( state.Velocity ),
			state.StartedAt.ToString( CultureInfo.InvariantCulture )
		);
	}

	private static string FormatVector( Vector3 value )
	{
		return string.Join(
			",",
			value.x.ToString( CultureInfo.InvariantCulture ),
			value.y.ToString( CultureInfo.InvariantCulture ),
			value.z.ToString( CultureInfo.InvariantCulture )
		);
	}

	private static bool TryParseTokenPhysicsState( string payload, out TokenPhysicsState state )
	{
		state = default;

		var parts = payload.Split( '|' );
		if ( parts.Length != 3 )
			return false;

		if ( !TryParseVector( parts[0], out var position ) ||
			!TryParseVector( parts[1], out var velocity ) ||
			!float.TryParse( parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var startedAt ) )
		{
			return false;
		}

		state = new TokenPhysicsState( position, velocity, startedAt );
		return true;
	}

	private static bool TryParseVector( string payload, out Vector3 value )
	{
		value = default;

		var parts = payload.Split( ',' );
		if ( parts.Length != 3 )
			return false;

		if ( !float.TryParse( parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x ) ||
			!float.TryParse( parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y ) ||
			!float.TryParse( parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z ) )
		{
			return false;
		}

		value = new Vector3( x, y, z );
		return true;
	}
}
