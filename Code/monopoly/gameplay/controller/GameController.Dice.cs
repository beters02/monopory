using System;
using System.Threading.Tasks;
using Sandbox;

public sealed partial class GameController : Component
{
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

			if ( child.Name.Contains( "Bg", StringComparison.OrdinalIgnoreCase ) )
				decal.ColorTint = diceSkin.BackgroundColor;
			else if (
				child.Name.Contains( "Dot", StringComparison.OrdinalIgnoreCase ) ||
				child.Name.Contains( "Pip", StringComparison.OrdinalIgnoreCase ) ||
				child.Name.Contains( "Fg", StringComparison.OrdinalIgnoreCase ) )
				decal.ColorTint = diceSkin.DotColor;
		}
	}

	public bool TryGetPhysicalDice( out DiceComponent dieA, out DiceComponent dieB )
	{
		dieA = DieA;
		dieB = DieB;

		if ( (dieA is null || dieB is null) && Scene is not null )
		{
			var dice = Scene.GetAllComponents<DiceComponent>()
				.Where( die => die is not null )
				.OrderBy( die => die.GameObject.Name )
				.ToList();

			var assignedDieA = dieA ?? dice.ElementAtOrDefault( 0 );
			dieA = assignedDieA;
			dieB ??= dice.FirstOrDefault( die => die != assignedDieA );
		}

		return UsePhysicalDice && dieA is not null && dieB is not null;
	}

	private async Task<(int DieA, int DieB)> RollPhysicalDiceAsync(
		float throwStrength,
		int expectedDieA,
		int expectedDieB,
		int rollIndex )
	{
		if ( !TryGetPhysicalDice( out var dieA, out var dieB ) )
			return (expectedDieA, expectedDieB);

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
			SetHudIsVisibleAll(false);
			ThrowPhysicalDice( originA, originB, rotationA, rotationB, velocityA, velocityB, spinA, spinB );
			await WaitForPhysicalDiceResultAsync();
			SetHudIsVisibleAll(true);
			return (expectedDieA, expectedDieB);
		}
		finally
		{
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

	private async Task<(int DieA, int DieB)> WaitForPhysicalDiceResultAsync()
	{
		if ( !TryGetPhysicalDice( out var dieA, out var dieB ) )
			return GetFallbackDiceResult();

		var startedAt = Time.Now;
		while ( Time.Now - startedAt < DiceSettleTimeout )
		{
			if ( dieA.IsSettled() && dieB.IsSettled() )
				break;

			await Task.Frame();
		}

		var dieAValue = dieA.GetTopFaceValue();
		var dieBValue = dieB.GetTopFaceValue();
		if ( dieAValue >= 1 && dieAValue <= 6 && dieBValue >= 1 && dieBValue <= 6 )
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
		Vector3 originA,
		Vector3 originB,
		Rotation rotationA,
		Rotation rotationB,
		Vector3 velocityA,
		Vector3 velocityB,
		Vector3 spinA,
		Vector3 spinB )
	{
		if ( !TryGetPhysicalDice( out var dieA, out var dieB ) )
			return;

		dieA.Throw( originA, rotationA, velocityA, spinA );
		dieB.Throw( originB, rotationB, velocityB, spinB );
	}
}
