using Sandbox;

public sealed class MonopolyGame : Component
{
	[Property] public List<MonopolyPlayerState> Players { get; set; } = new();

	[Property, Sync] public int CurrentPlayerIndex { get; set; }
	[Property, Sync] public int LastDieA { get; set; }
	[Property, Sync] public int LastDieB { get; set; }

	public MonopolyPlayerState CurrentPlayer =>
		Players.Count == 0 ? null : Players[CurrentPlayerIndex];

	[Button( "Roll Dice" )]
	public void RollDice()
	{
		if ( !Networking.IsHost )
			return;

		if ( CurrentPlayer is null )
			return;

		LastDieA = Game.Random.Int( 1, 6 );
		LastDieB = Game.Random.Int( 1, 6 );

		var total = LastDieA + LastDieB;

		var SpaceIndex = (CurrentPlayer.SpaceIndex + total) % 40;
		Log.Info(SpaceIndex);
		CurrentPlayer.SpaceIndex = SpaceIndex;

		Log.Info( $"Player {CurrentPlayerIndex + 1} rolled {LastDieA} + {LastDieB} = {total}" );

		AdvanceTurn();
	}

	private void AdvanceTurn()
	{
		if ( Players.Count == 0 )
			return;

		CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
	}
}