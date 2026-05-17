public static class GameAssets
{

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
		public static readonly GameSound CardFlip = new ( "sounds/effects/cardfx-deal2.mp3" );
		public static readonly GameSound Unassigned = new( "" );
		public static readonly GameSound Warning = new( "sounds/effects/uipack-retro12.mp3" );
		public static readonly GameSound Error = new( "sounds/effects/uipack-retro6.mp3" );
		
		public static class Popup
		{
			public static readonly GameSound Info = Unassigned;
			public static readonly GameSound Success = new( "sounds/effects/uipack-african4" );
			public static readonly GameSound Warning = Sounds.Warning;
			public static readonly GameSound Danger = Error;

			public static GameSound ForKind( PopupKind kind )
			{
				return kind switch
				{
					PopupKind.Success => Success,
					PopupKind.Warning => Warning,
					PopupKind.Danger => Danger,
					_ => Info
				};
			}
		}
	}
}
