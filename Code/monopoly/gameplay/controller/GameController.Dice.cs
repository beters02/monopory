using System;
using System.Threading.Tasks;
using Sandbox;

public sealed partial class GameController : Component
{
	[Property, Group( "Physical Dice" )] public DiceComponent DieA { get; set; }
	[Property, Group( "Physical Dice" )] public DiceComponent DieB { get; set; }
	[Property, Group( "Physical Dice" )] public Vector3 DiceThrowCenter { get; set; } = Vector3.Zero;
	[Property, Group( "Physical Dice" )] public float DiceThrowHeight { get; set; } = 110f;
	[Property, Group( "Physical Dice" )] public float DiceSpawnSpacing { get; set; } = 12f;
	[Property, Group( "Physical Dice" )] public float DiceMinDropSpeed { get; set; } = 260f;
	[Property, Group( "Physical Dice" )] public float DiceMaxDropSpeed { get; set; } = 540f;
	[Property, Group( "Physical Dice" )] public float DiceMinHorizontalSpeed { get; set; } = 35f;
	[Property, Group( "Physical Dice" )] public float DiceMaxHorizontalSpeed { get; set; } = 95f;
	[Property, Group( "Physical Dice" )] public float DiceMinSpinSpeed { get; set; } = 12f;
	[Property, Group( "Physical Dice" )] public float DiceMaxSpinSpeed { get; set; } = 30f;
	[Property, Group( "Physical Dice" )] public float DiceSettleTimeout { get; set; } = 5f;
	[Property, Group( "Physical Dice" )] public bool UsePhysicalDice { get; set; } = true;

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

	private async Task<(int DieA, int DieB)> RollPhysicalDiceAsync( float throwStrength )
	{
		if ( !TryGetPhysicalDice( out var dieA, out var dieB ) )
			return (Game.Random.Int( 1, 6 ), Game.Random.Int( 1, 6 ));

		var strength = Math.Clamp( throwStrength, 0f, 1f );
		var dropSpeed = DiceMinDropSpeed.LerpTo( DiceMaxDropSpeed, strength );
		var horizontalSpeed = DiceMinHorizontalSpeed.LerpTo( DiceMaxHorizontalSpeed, strength );
		var spin = DiceMinSpinSpeed.LerpTo( DiceMaxSpinSpeed, strength );
		var center = GetDiceThrowCenter();
		var side = GetDiceThrowSideAxis();
		var forward = Vector3.Cross( Vector3.Up, side ).Normal;

		var originA = center + Vector3.Up * DiceThrowHeight - side * DiceSpawnSpacing * 0.5f;
		var originB = center + Vector3.Up * DiceThrowHeight + side * DiceSpawnSpacing * 0.5f;
		var velocityA = Vector3.Down * dropSpeed + forward * horizontalSpeed + side * (horizontalSpeed * 0.25f);
		var velocityB = Vector3.Down * (dropSpeed * 0.94f) + forward * (horizontalSpeed * 0.85f) - side * (horizontalSpeed * 0.2f);
		var rotationA = Rotation.Random;
		var rotationB = Rotation.Random;
		var spinA = new Vector3( Game.Random.Float( -1f, 1f ), Game.Random.Float( -1f, 1f ), Game.Random.Float( -1f, 1f ) ).Normal * spin;
		var spinB = new Vector3( Game.Random.Float( -1f, 1f ), Game.Random.Float( -1f, 1f ), Game.Random.Float( -1f, 1f ) ).Normal * spin;

		IsResolvingPhysicalDice = true;
		PhysicalDiceStartedAt = Time.Now;

		try
		{
			ThrowPhysicalDice( originA, originB, rotationA, rotationB, velocityA, velocityB, spinA, spinB );

			var startedAt = Time.Now;
			while ( Time.Now - startedAt < DiceSettleTimeout )
			{
				if ( dieA.IsSettled() && dieB.IsSettled() )
					break;

				await Task.Frame();
			}

			return (dieA.GetTopFaceValue(), dieB.GetTopFaceValue());
		}
		finally
		{
			IsResolvingPhysicalDice = false;
			PhysicalDiceStartedAt = 0f;
		}
	}

	private Vector3 GetDiceThrowCenter()
	{
		if ( DiceThrowCenter != Vector3.Zero )
			return DiceThrowCenter;

		return Board?.GameObject?.WorldPosition ?? Vector3.Zero;
	}

	private static Vector3 GetDiceThrowSideAxis()
	{
		var yaw = Game.Random.Float( 0f, MathF.PI * 2f );
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
