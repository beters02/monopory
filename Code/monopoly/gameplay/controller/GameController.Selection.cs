using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Sandbox;

public sealed partial class GameController : Component
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

		//if ( LocalSelectedSpaceIndex == -1 && spaceIndex != -1 )
		//	GameAssets.Sounds.CardFlip.Play();

		if (spaceIndex != -1 )
			GameAssets.Sounds.CardFlip.Play();
		
		LocalSelectedSpaceIndex = spaceIndex;
		LocalSelectedDrawnCardText = drawnCardText ?? "";
	}

	public void ClearSelectedSpace()
	{
		SelectSpaceAsync( -1 );
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
			Phase != GamePhase.WaitingForBuyDecision ||
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

	private void ClearSelectedSpaceForPlayer( PlayerState player )
	{
		var connection = GetConnectionForPlayer( player );

		if ( connection is null )
			return;

		using ( Rpc.FilterInclude( connection ) )
		{
			ClearLocalSelectedSpaceCard();
		}
	}

	[Rpc.Broadcast]
	private void ClearLocalSelectedSpaceCard()
	{
		ClearSelectedSpace();
	}
}
