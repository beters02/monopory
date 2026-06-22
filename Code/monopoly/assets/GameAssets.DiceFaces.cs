using System;
using Sandbox;

public static partial class GameAssets
{
	[GameAssetCategory]
	public static class DiceFaces
	{
		public static readonly GameMaterial OnePips = new( "textures/dice/new/dice-six-faces-one.vmat" );
		public static readonly GameMaterial OneBackground = new( "textures/dice/new/dice-six-faces-one-bg.vmat" );
		public static readonly GameMaterial TwoPips = new( "textures/dice/new/dice-six-faces-two.vmat" );
		public static readonly GameMaterial TwoBackground = new( "textures/dice/new/dice-six-faces-two-bg.vmat" );
		public static readonly GameMaterial ThreePips = new( "textures/dice/new/dice-six-faces-three.vmat" );
		public static readonly GameMaterial ThreeBackground = new( "textures/dice/new/dice-six-faces-three-bg.vmat" );
		public static readonly GameMaterial FourPips = new( "textures/dice/new/dice-six-faces-four.vmat" );
		public static readonly GameMaterial FourBackground = new( "textures/dice/new/dice-six-faces-four-bg.vmat" );
		public static readonly GameMaterial FivePips = new( "textures/dice/new/dice-six-faces-five.vmat" );
		public static readonly GameMaterial FiveBackground = new( "textures/dice/new/dice-six-faces-five-bg.vmat" );
		public static readonly GameMaterial SixPips = new( "textures/dice/new/dice-six-faces-six.vmat" );
		public static readonly GameMaterial SixBackground = new( "textures/dice/new/dice-six-faces-six-bg.vmat" );

		public static Material GetMaterial( int pipValue, bool isBackground )
		{
			var faceMaterial = (pipValue, isBackground) switch
			{
				(1, false) => OnePips,
				(1, true) => OneBackground,
				(2, false) => TwoPips,
				(2, true) => TwoBackground,
				(3, false) => ThreePips,
				(3, true) => ThreeBackground,
				(4, false) => FourPips,
				(4, true) => FourBackground,
				(5, false) => FivePips,
				(5, true) => FiveBackground,
				(6, false) => SixPips,
				(6, true) => SixBackground,
				_ => OnePips
			};

			return faceMaterial.Material;
		}
	}
}
