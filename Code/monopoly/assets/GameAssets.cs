using System;

public sealed class GameAssetCategoryAttribute : Attribute {}

public static partial class GameAssets
{
	private static bool uiAssetsPrewarmed;

	public static void PrewarmUiAssets()
	{
		if ( uiAssetsPrewarmed )
			return;

		uiAssetsPrewarmed = true;

		foreach ( (var type, GameAssetCategoryAttribute attribute) in TypeLibrary.GetTypesWithAttribute<GameAssetCategoryAttribute>() )
			PreloadAssets( type );

		KeyboardIcons.Preload();
		MouseIcons.Preload();
	}

	private static void PreloadAssets( TypeDescription type )
	{
		if ( type is null )
			return;

		foreach ( var field in type.Fields )
			PreloadAsset( field );
	}

	private static void PreloadAsset( FieldDescription field )
	{
		var asset = field.GetValue( null );
		if ( asset is null )
			return;

		var assetType = Game.TypeLibrary.GetType( field.FieldType );
		var preloadMethod = assetType?.GetMethod( "Preload" );
		preloadMethod?.Invoke( asset );
	}

	[GameAssetCategory]
	public static class Images
	{
		public static readonly GameImage Chance = new ( "textures/Chance.png" );
	}

	[GameAssetCategory]
	public static class Icons
	{
		public static readonly GameIcon CameraWhiteFixedSvg = new ( "textures/icons/camera_white_fixed.svg" );
	}

	[GameAssetCategory]
	public static class Materials
	{
		public static readonly GameMaterial House = new ( "materials/pieces/house.vmat" );
		public static readonly GameMaterial Hotel = new ( "materials/pieces/hotel.vmat" );
		public static readonly GameMaterial Piece = new ( "materials/pieces/piece.vmat" );
		public static readonly GameMaterial BankruptedPiece = new ( "materials/pieces/piece_bankrupt.vmat" );
	}

	[GameAssetCategory]
	public static class Sounds
	{
		public static readonly GameSound Click = new ( "sounds/effects/click.sound" );
		public static readonly GameSound ClosingClick = new("sounds/effects/closing-click.sound" );
		public static readonly GameSound ClickAndOpen = new ( "sounds/effects/click-and-open.sound" );
		public static readonly GameSound ClickAndClose = new ( "sounds/effects/click-and-close.sound" );
		public static readonly GameSound CardFlip = new ( "sounds/effects/card-deal.sound" );
		public static readonly GameSound Unassigned = new( "" );
		public static readonly GameSound Warning = new( "sounds/effects/uipack-retro12.sound" );
		public static readonly GameSound Error = new( "sounds/effects/ui-error.sound" );
		public static readonly GameSound DiceImpact = new( "sounds/effects/dice-impact.sound" );
		public static readonly GameSound TokenStep = new( "sounds/effects/footstep/footstep-piece.sound" );
		public static readonly GameSound PianoBingBingBing = new ( "sounds/effects/uipack-modern16.sound" );
		public static readonly GameSound TradeReceived = new ( "sounds/effects/uipack-retro8.sound" );
		public static readonly GameSound TradeNegotiated = new ( "sounds/effects/uipack-retro9.sound" );
		public static readonly GameSound Success = new ( "sounds/effects/ui-success.sound" );
		public static readonly GameSound ChatReceived = new ( "sounds/effects/chat-message-received.sound" );
		public static readonly GameSound ChatSent = new ( "sounds/effects/chat-message-sent.sound" );
		public static readonly GameSound ChatMentioned = new ( "sounds/effects/chat-message-mentioned.sound" );
		public static readonly GameSound AuctionStart = new ( "sounds/effects/auction-start.sound" );
		public static readonly GameSound AuctionBid = new ( "sounds/effects/auction-bid.sound" );
		public static readonly GameSound Pluh = new ( "sounds/effects/pluh.sound" );

		public static class Popup
		{
			public static readonly GameSound Info = Unassigned;
			public static readonly GameSound Success = Sounds.Success;
			public static readonly GameSound Warning = Sounds.Warning;
			public static readonly GameSound Danger = Error;

			public static GameSound ForKind( PopupKind kind )
			{
				return kind switch
				{
					PopupKind.Success => Success,
					PopupKind.Warning => Warning,
					PopupKind.Danger => Danger,
					PopupKind.Confirmation => Warning,
					_ => Info
				};
			}
		}
	}

	[GameAssetCategory]
	public static class Soundtracks
	{
		public static readonly GameSoundtrack Menu0 = new ( "sounds/music/menu_soundtrack.sound", 0f, 3f );
		public static readonly GameSoundtrack Game0 = new ( "sounds/music/game_soundtrack.sound", 6f, 3f );
	}

	[GameAssetCategory]
	public static partial class KeyboardIcons {}

	[GameAssetCategory]
	public static partial class MouseIcons {}

}
