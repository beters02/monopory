using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	private bool CanCurrentPlayerAct( Connection caller )
	{
		if ( !CanAcceptGameplayInput() )
			return false;

		if ( caller == null )
			return false;

		var currentPlayer = CurrentPlayer;

		if ( currentPlayer == null || currentPlayer.IsBankrupt )
			return false;

		// Host can always act during local testing
		if ( Networking.IsHost && caller == Connection.Local )
			return true;

		return currentPlayer.OwnerId == caller.SteamId;
	}

	[Rpc.Host]
	public void RequestSetReady( bool isReady )
	{
		var player = GetPlayerForConnection( Rpc.Caller );
		TrySetReady( player, isReady );
	}

	[Rpc.Host]
	public void RequestStartGame()
	{
		if ( !IsHostCaller( Rpc.Caller ) )
			return;

		TryStartGame();
	}

	[Rpc.Host]
	public void RequestPauseGame()
	{
		if ( !IsHostCaller( Rpc.Caller ) )
			return;

		TryPauseGame();
	}

	[Rpc.Host]
	public void RequestResumeGame()
	{
		if ( !IsHostCaller( Rpc.Caller ) )
			return;

		TryResumeGame();
	}

	[Rpc.Host]
	public void RequestReturnToLobby()
	{
		if ( !IsHostCaller( Rpc.Caller ) )
			return;

		TryReturnToLobby();
	}

	[Rpc.Host]
	public void RequestRollDice(int amount = -1)
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;
		_ = RollDiceAsync(amount);
	}

	[Rpc.Host]
	public void RequestEndTurn()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;

		EndTurn();
	}

	[Rpc.Host]
	public void RequestBuyPendingProperty()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;
		
		BuyPendingProperty();
	}

	[Rpc.Host]
	public void RequestSkipPendingProperty()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;
		SkipPendingProperty();
	}

	[Rpc.Host]
	public void RequestStartPendingAuction()
	{
		if ( !CanCurrentPlayerAct( Rpc.Caller ) )
			return;

		AuctionPendingProperty();
	}

	[Rpc.Host]
	public void RequestAuctionBid( int bidAmount )
	{
		if ( !CanAcceptGameplayInput() )
			return;

		var bidderIndex = GetPlayerIndexForCaller( Rpc.Caller );
		if ( bidderIndex < 0 )
			return;

		PlaceAuctionBid( bidderIndex, bidAmount );
	}
}
