using System;
using System.Threading.Tasks;
using Sandbox;

public sealed partial class GameController : Component
{
	private int awaitingPhysicalDiceRollIndex = -1;
	private (int DieA, int DieB)? submittedPhysicalDiceResult;

	private void EnsureDiceNetworkOwnership( bool force = false )
	{
		if ( !Networking.IsHost || !TryGetPhysicalDice( out var dieA, out var dieB ) )
			return;

		var owner = GetConnectionForPlayer( CurrentPlayer ) ?? Connection.Host;
		var ownerId = owner?.SteamId ?? 0L;
		if ( owner is null || (!force && lastDiceNetworkOwnerId == ownerId) )
			return;

		AssignDiceNetworkOwnership( dieA, owner );
		AssignDiceNetworkOwnership( dieB, owner );
		lastDiceNetworkOwnerId = ownerId;
	}

	private static void AssignDiceNetworkOwnership( DiceComponent die, Connection owner )
	{
		if ( die?.GameObject is null || owner is null )
			return;

		die.GameObject.NetworkMode = NetworkMode.Object;
		die.GameObject.Network.SetOrphanedMode( NetworkOrphaned.Host );
		die.GameObject.Network.AssignOwnership( owner );
	}

	private void ApplyLocalDiceSkin()
	{
		if ( LocalPlayer is null )
			return;

		var diceSkin = DiceSkinCatalog.GetByIdOrDefault( LocalPlayer.SelectedDiceSkinId );
		var diceSkinSignature = GetDiceSkinSignature( diceSkin );
		if ( string.Equals( lastAppliedLocalDiceSkinId, diceSkinSignature, StringComparison.Ordinal ) )
			return;

		if ( !TryGetPhysicalDice( out var dieA, out var dieB ) )
			return;

		ApplyDiceSkin( dieA, diceSkin );
		ApplyDiceSkin( dieB, diceSkin );
		lastAppliedLocalDiceSkinId = diceSkinSignature;
	}

	private static string GetDiceSkinSignature( DiceSkinDefinition diceSkin )
	{
		if ( diceSkin is null )
			return "";

		return $"{diceSkin.Id}|{diceSkin.BodyMaterialPath}|{diceSkin.BackgroundColor}|{diceSkin.DotColor}";
	}

	private static void ApplyDiceSkin( DiceComponent die, DiceSkinDefinition diceSkin )
	{
		if ( die?.GameObject is null || diceSkin is null )
			return;

		var renderer = die.GameObject.GetComponent<ModelRenderer>();
		if ( renderer is not null )
			renderer.MaterialOverride = string.IsNullOrWhiteSpace( diceSkin.BodyMaterialPath )
				? null
				: Material.Load( diceSkin.BodyMaterialPath );

		foreach ( var child in die.GameObject.Children )
		{
			var decal = child.GetComponent<Decal>();
			if ( decal is null )
				continue;

			if ( child.Name.Contains( "_Bg", StringComparison.OrdinalIgnoreCase ) )
				decal.ColorTint = diceSkin.BackgroundColor;
			else if (
				child.Name.Contains( "Side_", StringComparison.OrdinalIgnoreCase ) ||
				child.Name.Contains( "Dot", StringComparison.OrdinalIgnoreCase ) ||
				child.Name.Contains( "Pip", StringComparison.OrdinalIgnoreCase ) ||
				child.Name.Contains( "Fg", StringComparison.OrdinalIgnoreCase ) )
				decal.ColorTint = diceSkin.DotColor;
		}
	}

	public bool TryGetPhysicalDice( out DiceComponent dieA, out DiceComponent dieB )
	{
		dieA = IsGameplayDie( DieA ) ? DieA : null;
		dieB = IsGameplayDie( DieB ) ? DieB : null;

		if ( (dieA is null || dieB is null) && Scene is not null )
		{
			var dice = Scene.GetAllComponents<DiceComponent>()
				.Where( IsGameplayDie )
				.OrderBy( die => die.GameObject.Name )
				.ToList();

			var assignedDieA = dieA ?? dice.ElementAtOrDefault( 0 );
			dieA = assignedDieA;
			dieB ??= dice.FirstOrDefault( die => die != assignedDieA );
		}

		return UsePhysicalDice && dieA is not null && dieB is not null;
	}

	private static bool IsGameplayDie( DiceComponent die )
	{
		if ( die?.GameObject is null || !die.IsValid() )
			return false;

		for ( var current = die.GameObject; current is not null; current = current.Parent )
		{
			if ( current.Name.Contains( "DicePreview", StringComparison.OrdinalIgnoreCase ) ||
				current.Name.Contains( "PiecePreview", StringComparison.OrdinalIgnoreCase ) )
				return false;
		}

		return true;
	}

	private async Task<(int DieA, int DieB)> RollPhysicalDiceAsync(
		float throwStrength,
		int rollIndex )
	{
		if ( !TryGetPhysicalDice( out var dieA, out var dieB ) )
			return GetSeededDice( rollIndex );

		var random = CreateDicePhysicsRandom( rollIndex );
		var strength = Math.Clamp( throwStrength, 0f, 1f );
		var dropSpeed = DiceMinDropSpeed.LerpTo( DiceMaxDropSpeed, strength );
		var horizontalSpeed = DiceMinHorizontalSpeed.LerpTo( DiceMaxHorizontalSpeed, strength );
		var spin = DiceMinSpinSpeed.LerpTo( DiceMaxSpinSpeed, strength );
		var center = GetDiceThrowCenter();
		var side = GetDiceThrowSideAxis( random );
		var forward = Vector3.Cross( Vector3.Up, side ).Normal;

		var originA = center + Vector3.Up * DiceThrowHeight - side * DiceSpawnSpacing * 0.5f;
		var originB = center + Vector3.Up * DiceThrowHeight + side * DiceSpawnSpacing * 0.5f;
		var velocityA = Vector3.Down * dropSpeed + forward * horizontalSpeed + side * (horizontalSpeed * 0.25f);
		var velocityB = Vector3.Down * (dropSpeed * 0.94f) + forward * (horizontalSpeed * 0.85f) - side * (horizontalSpeed * 0.2f);
		var rotationA = GetRandomDiceRotation( random );
		var rotationB = GetRandomDiceRotation( random );
		if ( rotationA == rotationB )
			rotationB = GetRandomDiceRotation( random );

		var spinA = GetRandomDiceSpin( random ) * spin;
		var spinB = GetRandomDiceSpin( random ) * spin;

		IsResolvingPhysicalDice = true;
		PhysicalDiceStartedAt = Time.Now;

		try
		{
			var diceOwner = GetConnectionForPlayer( CurrentPlayer ) ?? Connection.Host;
			if ( diceOwner is null )
				return GetFallbackDiceResult();

			EnsureDiceNetworkOwnership();
			awaitingPhysicalDiceRollIndex = rollIndex;
			submittedPhysicalDiceResult = null;
			SetHudIsVisibleAll(false);
			ThrowPhysicalDice( rollIndex, diceOwner.SteamId, originA, originB, rotationA, rotationB, velocityA, velocityB, spinA, spinB );
			return await WaitForPhysicalDiceOwnerResultAsync( rollIndex );
		}
		finally
		{
			awaitingPhysicalDiceRollIndex = -1;
			submittedPhysicalDiceResult = null;
			SetHudIsVisibleAll( true );
			IsResolvingPhysicalDice = false;
			PhysicalDiceStartedAt = 0f;
		}
	}

	private System.Random CreateDicePhysicsRandom( int rollIndex )
	{
		var seedBytes = System.Security.Cryptography.SHA256.HashData(
			System.Text.Encoding.UTF8.GetBytes( $"{privateDiceSeed}:{rollIndex}:physics" ) );
		return new System.Random( BitConverter.ToInt32( seedBytes, 0 ) );
	}

	private static float NextDiceRandomFloat( System.Random random, float min, float max )
	{
		return min + (float)random.NextDouble() * (max - min);
	}

	private static Rotation GetRandomDiceRotation( System.Random random )
	{
		return Rotation.From(
			NextDiceRandomFloat( random, 0f, 360f ),
			NextDiceRandomFloat( random, 0f, 360f ),
			NextDiceRandomFloat( random, 0f, 360f ) );
	}

	private static Vector3 GetRandomDiceSpin( System.Random random )
	{
		return new Vector3(
			NextDiceRandomFloat( random, -1f, 1f ),
			NextDiceRandomFloat( random, -1f, 1f ),
			NextDiceRandomFloat( random, -1f, 1f ) ).Normal;
	}

	private async Task<(int DieA, int DieB)> WaitForPhysicalDiceResultAsync(
		Vector3? initialPositionA = null,
		Vector3? initialPositionB = null )
	{
		if ( !TryGetPhysicalDice( out var dieA, out var dieB ) )
			return GetFallbackDiceResult();

		var waitForReplicatedThrow = initialPositionA.HasValue && initialPositionB.HasValue;
		var observedThrow = !waitForReplicatedThrow;
		var settled = false;
		var startedAt = Time.Now;
		while ( Time.Now - startedAt < DiceSettleTimeout )
		{
			if ( !observedThrow )
			{
				var dieAMoved = (dieA.GameObject.WorldPosition - initialPositionA.Value).Length > 0.5f;
				var dieBMoved = (dieB.GameObject.WorldPosition - initialPositionB.Value).Length > 0.5f;
				observedThrow = dieAMoved && dieBMoved;
			}

			if ( observedThrow && dieA.IsSettled() && dieB.IsSettled() )
			{
				settled = true;
				break;
			}

			await Task.Frame();
		}

		var dieAValue = dieA.GetTopFaceValue();
		var dieBValue = dieB.GetTopFaceValue();
		if ( settled && dieAValue >= 1 && dieAValue <= 6 && dieBValue >= 1 && dieBValue <= 6 )
			return (dieAValue, dieBValue);

		return GetFallbackDiceResult();
	}

	private (int DieA, int DieB) GetFallbackDiceResult()
	{
		if ( LastDieA >= 1 && LastDieA <= 6 && LastDieB >= 1 && LastDieB <= 6 )
			return (LastDieA, LastDieB);

		return (Game.Random.Int( 1, 6 ), Game.Random.Int( 1, 6 ));
	}

	private Vector3 GetDiceThrowCenter()
	{
		if ( DiceThrowCenter != Vector3.Zero )
			return DiceThrowCenter;

		return Board?.GameObject?.WorldPosition ?? Vector3.Zero;
	}

	private static Vector3 GetDiceThrowSideAxis( System.Random random )
	{
		var yaw = NextDiceRandomFloat( random, 0f, MathF.PI * 2f );
		return new Vector3( MathF.Cos( yaw ), MathF.Sin( yaw ), 0f ).Normal;
	}

	[Rpc.Broadcast]
	private void ThrowPhysicalDice(
		int rollIndex,
		long ownerId,
		Vector3 originA,
		Vector3 originB,
		Rotation rotationA,
		Rotation rotationB,
		Vector3 velocityA,
		Vector3 velocityB,
		Vector3 spinA,
		Vector3 spinB )
	{
		if ( Connection.Local?.SteamId != ownerId ||
			!TryGetPhysicalDice( out var dieA, out var dieB ) )
			return;

		_ = ThrowPhysicalDiceWhenOwnedAsync(
			rollIndex,
			dieA,
			dieB,
			originA,
			originB,
			rotationA,
			rotationB,
			velocityA,
			velocityB,
			spinA,
			spinB );
	}

	private async Task ThrowPhysicalDiceWhenOwnedAsync(
		int rollIndex,
		DiceComponent dieA,
		DiceComponent dieB,
		Vector3 originA,
		Vector3 originB,
		Rotation rotationA,
		Rotation rotationB,
		Vector3 velocityA,
		Vector3 velocityB,
		Vector3 spinA,
		Vector3 spinB )
	{
		var ownershipWaitStartedAt = Time.Now;
		while ( !dieA.IsNetworkOwner || !dieB.IsNetworkOwner )
		{
			if ( Time.Now - ownershipWaitStartedAt >= DiceOwnershipWaitTimeout )
				return;

			await Task.Frame();
		}

		dieA.Throw( originA, rotationA, velocityA, spinA );
		dieB.Throw( originB, rotationB, velocityB, spinB );
		await SubmitPhysicalDiceResultWhenSettledAsync( rollIndex, dieA, dieB );
	}

	private async Task SubmitPhysicalDiceResultWhenSettledAsync(
		int rollIndex,
		DiceComponent dieA,
		DiceComponent dieB )
	{
		var startedAt = Time.Now;
		while ( Time.Now - startedAt < DiceSettleTimeout )
		{
			if ( !dieA.IsNetworkOwner || !dieB.IsNetworkOwner )
				return;

			if ( dieA.IsSettled() && dieB.IsSettled() )
			{
				SubmitPhysicalDiceResult( rollIndex, dieA.GetTopFaceValue(), dieB.GetTopFaceValue() );
				return;
			}

			await Task.Frame();
		}
	}

	[Rpc.Host]
	private void SubmitPhysicalDiceResult( int rollIndex, int dieA, int dieB )
	{
		if ( rollIndex != awaitingPhysicalDiceRollIndex ||
			!IsResolvingPhysicalDice ||
			!IsValidDieValue( dieA ) ||
			!IsValidDieValue( dieB ) )
			return;

		var callerPlayer = GetPlayerForCaller( Rpc.Caller );
		if ( callerPlayer is null || callerPlayer != CurrentPlayer )
			return;

		submittedPhysicalDiceResult = (dieA, dieB);
	}

	private async Task<(int DieA, int DieB)> WaitForPhysicalDiceOwnerResultAsync( int rollIndex )
	{
		var startedAt = Time.Now;
		while ( Time.Now - startedAt < DiceOwnershipWaitTimeout + DiceSettleTimeout )
		{
			if ( awaitingPhysicalDiceRollIndex != rollIndex )
				break;

			if ( submittedPhysicalDiceResult.HasValue )
				return submittedPhysicalDiceResult.Value;

			await Task.Frame();
		}

		Log.Warning( $"Dice owner did not submit roll {rollIndex} before timeout; using fallback result." );
		return GetFallbackDiceResult();
	}
}
