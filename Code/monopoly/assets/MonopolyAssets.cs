public static class MonopolyAssets
{
	public static class Sounds
	{
		public static readonly MonopolySound Click = new ( "sounds/effects/click.sound" );
		public static readonly MonopolySound ClickAndOpen = new ( "sounds/effects/click-and-open.sound" );
		public static readonly MonopolySound ClickAndClose = new ( "sounds/effects/click-and-close.sound" );
		public static readonly MonopolySound Unassigned = new( "" );
		public static readonly MonopolySound Warning = new( "sounds/effects/uipack-retro6.mp3" );
		public static readonly MonopolySound Error = new( "sounds/effects/uipack-retro11.mp3" );

		public static class Popup
		{
			public static readonly MonopolySound Info = Unassigned;
			public static readonly MonopolySound Success = Unassigned;
			public static readonly MonopolySound Warning = Error;
			public static readonly MonopolySound Danger = Error;

			public static MonopolySound ForKind( MonopolyPopupKind kind )
			{
				return kind switch
				{
					MonopolyPopupKind.Success => Success,
					MonopolyPopupKind.Warning => Warning,
					MonopolyPopupKind.Danger => Danger,
					_ => Info
				};
			}
		}
	}
}