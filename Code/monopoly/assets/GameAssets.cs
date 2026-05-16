public static class GameAssets
{
	public static class Sounds
	{
		public static readonly GameSound Click = new ( "sounds/effects/click.sound" );
		public static readonly GameSound ClickAndOpen = new ( "sounds/effects/click-and-open.sound" );
		public static readonly GameSound ClickAndClose = new ( "sounds/effects/click-and-close.sound" );
		public static readonly GameSound Unassigned = new( "" );
		public static readonly GameSound Warning = new( "sounds/effects/uipack-retro6.mp3" );
		public static readonly GameSound Error = new( "sounds/effects/uipack-retro11.mp3" );

		public static class Popup
		{
			public static readonly GameSound Info = Unassigned;
			public static readonly GameSound Success = Unassigned;
			public static readonly GameSound Warning = Error;
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