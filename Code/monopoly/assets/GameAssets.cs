public static class GameAssets
{
	private static bool uiAssetsPrewarmed;

	public static void PrewarmUiAssets()
	{
		if ( uiAssetsPrewarmed )
			return;

		uiAssetsPrewarmed = true;

		Images.Chance.Preload();

		Materials.House.Preload();
		Materials.Hotel.Preload();

		Sounds.Click.Preload();
		Sounds.ClosingClick.Preload();
		Sounds.ClickAndOpen.Preload();
		Sounds.ClickAndClose.Preload();
		Sounds.CardFlip.Preload();
		Sounds.Warning.Preload();
		Sounds.Error.Preload();
		Sounds.DiceImpact.Preload();
		Sounds.TokenStep.Preload();
		Sounds.Popup.Success.Preload();
	}

	public static class Images
	{
		public static readonly GameImage Chance = new ( "textures/Chance.png" );
	}

	public static class Materials
	{
		public static readonly GameMaterial House = new ( "materials/pieces/house.vmat" );
		public static readonly GameMaterial Hotel = new ( "materials/pieces/hotel.vmat" );
	}

	public static class Sounds
	{
		public static readonly GameSound Click = new ( "sounds/effects/click.sound" );
		public static readonly GameSound ClosingClick = new("sounds/effects/closing-click.sound" );
		public static readonly GameSound ClickAndOpen = new ( "sounds/effects/click-and-open.sound" );
		public static readonly GameSound ClickAndClose = new ( "sounds/effects/click-and-close.sound" );
		public static readonly GameSound CardFlip = new ( "sounds/effects/card-deal.sound" );
		public static readonly GameSound Unassigned = new( "" );
		public static readonly GameSound Warning = new( "sounds/effects/uipack-retro12.mp3" );
		public static readonly GameSound Error = new( "sounds/effects/ui-error.sound" );
		public static readonly GameSound DiceImpact = new( "sounds/effects/dice-impact.sound" );
		public static readonly GameSound TokenStep = new( "sounds/effects/footstep/footstep-piece.sound" );
		
		public static class Popup
		{
			public static readonly GameSound Info = Unassigned;
			public static readonly GameSound Success = new( "sounds/effects/ui-success.sound" );
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
}
