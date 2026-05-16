using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class MonopolyGame : Component
{

	public int LocalSelectedSpaceIndex { get; set; } = -1;
	public string LocalSelectedDrawnCardText { get; set; } = "";

	public Logger landingLogger = new("GameLanding");

	public SpaceDef SelectedSpace =>
		Board is not null && LocalSelectedSpaceIndex >= 0 && LocalSelectedSpaceIndex < Board.Spaces.Count
			? Board.GetSpaceDef(LocalSelectedSpaceIndex)
			: null;

	private void SelectSpaceAsync( int spaceIndex, string drawnCardText = "" )
	{
		LocalSelectedSpaceIndex = spaceIndex;
		LocalSelectedDrawnCardText = drawnCardText ?? "";
	}

	public void SelectSpace( int spaceIndex )
	{
		if ( !CanLeaveCurrentSelectedSpace() )
			return;

		if ( spaceIndex == LocalSelectedSpaceIndex )
			spaceIndex = -1;

		SelectSpaceAsync( spaceIndex );
	}

	public bool CanLeaveCurrentSelectedSpace()
	{
		return CurrentPlayerIndex != LocalPlayerIndex ||
			Phase != MonopolyGamePhase.WaitingForBuyDecision ||
			PendingPurchaseSpaceIndex < 0 ||
			LocalSelectedSpaceIndex != PendingPurchaseSpaceIndex;
	}

	private void ShowCardForPlayerWhoLanded( PlayerState player, string drawnCardText = "" )
	{
		var spaceIndex = player.SpaceIndex;
		var connection = GetConnectionForPlayer( player );

		if ( connection is null )
			return;

		using ( Rpc.FilterInclude( connection ) )
		{
			ShowLandedSpaceCard( spaceIndex, drawnCardText );
		}
	}

	[Rpc.Broadcast]
	private void ShowLandedSpaceCard( int spaceIndex, string drawnCardText )
	{
		SelectSpaceAsync( spaceIndex, drawnCardText );
	}
}
