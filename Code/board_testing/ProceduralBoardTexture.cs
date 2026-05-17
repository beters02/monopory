using System;
using System.Net;
using System.Text;
using Sandbox;

namespace BoardTesting;

/// <summary>
/// Experimental SVG-to-texture Monopoly board renderer. This is a quick way to
/// iterate on board art while keeping a real mesh plane available for shadows.
/// </summary>
public sealed class ProceduralBoardTexture : Component
{
	[Property] public bool GenerateOnStart { get; set; } = true;
	[Property] public bool GenerateInEditorOnEnable { get; set; } = true;
	[Property] public bool CreatePreviewPlane { get; set; } = true;
	[Property] public bool ClearExistingPreview { get; set; } = true;
	[Property] public int TextureSize { get; set; } = 2048;
	[Property] public float PreviewWorldSize { get; set; } = 488f;
	[Property] public Color BoardColor { get; set; } = new Color( 0.05f, 0.42f, 0.54f );
	[Property] public Color CenterColor { get; set; } = new Color( 0.60f, 0.72f, 0.75f );
	[Property] public Color SpaceColor { get; set; } = new Color( 0.91f, 0.90f, 0.84f );
	[Property] public Color LineColor { get; set; } = new Color( 0.08f, 0.10f, 0.10f );
	[Property] public Color TextColor { get; set; } = new Color( 0.08f, 0.07f, 0.06f );
	[Property] public float OuterPaddingPercent { get; set; } = 0.035f;
	[Property] public float AccentDepthPercent { get; set; } = 0.24f;
	[Property] public float BorderWidthPercent { get; set; } = 0.006f;
	[Property] public bool SaveDebugPng { get; set; } = true;

	public Texture GeneratedTexture { get; private set; }
	public Material GeneratedMaterial { get; private set; }
	public Bitmap GeneratedBitmap { get; private set; }
	public string DebugPngPath { get; private set; }

	private GameObject previewObject;

	protected override void OnEnabled()
	{
		if ( GenerateInEditorOnEnable && !Game.IsPlaying )
			Generate();
	}

	protected override void OnStart()
	{
		if ( GenerateOnStart )
			Generate();
	}

	[Button( "Generate Board Texture" )]
	public void Generate()
	{
		TextureSize = Math.Clamp( TextureSize, 512, 8192 );

		var svg = BuildSvg();
		GeneratedTexture?.Dispose();
		GeneratedBitmap = Bitmap.CreateFromSvgString( svg, TextureSize, TextureSize, null, null, null );
		GeneratedTexture = GeneratedBitmap?.ToTexture( false );

		if ( SaveDebugPng && GeneratedBitmap is not null )
		{
			FileSystem.Data.CreateDirectory( "board_testing" );
			DebugPngPath = FileSystem.Data.GetFullPath( "board_testing/generated_board_texture.png" );
			FileSystem.Data.WriteAllBytes( "board_testing/generated_board_texture.png", GeneratedBitmap.ToPng() );
		}

		GeneratedMaterial = Material.Create( "materials/board_testing/generated_board_texture_preview.vmat", "shaders/complex.shader", true );
		GeneratedMaterial.Set( "TextureColor", GeneratedTexture );
		GeneratedMaterial.Set( "TextureRoughness", Texture.White );
		GeneratedMaterial.Set( "g_flRoughnessScaleFactor", 0.72f );
		GeneratedMaterial.Set( "g_flMetalness", 0f );
		GeneratedMaterial.Set( "g_vColorTint", Color.White );
		GeneratedMaterial.Set( "g_flModelTintAmount", 0f );
		GeneratedMaterial.Set( "g_vTexCoordOffset", Vector2.Zero );
		GeneratedMaterial.Set( "g_vTexCoordScale", Vector2.One );

		if ( CreatePreviewPlane )
			RebuildPreviewPlane();

		Log.Info( $"ProceduralBoardTexture generated {TextureSize}x{TextureSize} texture. Valid: {GeneratedTexture is not null && !GeneratedTexture.IsError}. Debug PNG: {DebugPngPath}" );
	}

	private string BuildSvg()
	{
		var size = TextureSize;
		var pad = size * OuterPaddingPercent;
		var board = size - pad * 2f;
		var corner = board * 0.155f;
		var tile = (board - corner * 2f) / 9f;
		var border = MathF.Max( 2f, size * BorderWidthPercent );
		var accentDepth = corner * AccentDepthPercent;

		var sb = new StringBuilder();
		sb.AppendLine( $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 {size} {size}\">" );
		sb.AppendLine( $"<rect x=\"0\" y=\"0\" width=\"{size}\" height=\"{size}\" fill=\"{Css( BoardColor )}\"/>" );

		var left = pad;
		var top = pad;
		var right = pad + board;
		var bottom = pad + board;
		var innerLeft = left + corner;
		var innerTop = top + corner;
		var innerSize = board - corner * 2f;

		sb.AppendLine( $"<rect x=\"{innerLeft:0.###}\" y=\"{innerTop:0.###}\" width=\"{innerSize:0.###}\" height=\"{innerSize:0.###}\" fill=\"{Css( CenterColor )}\" stroke=\"{Css( LineColor )}\" stroke-width=\"{border:0.###}\"/>" );

		foreach ( var def in BoardData.CreateSpaceDefs() )
		{
			var rect = GetSpaceRect( def.Index, left, top, right, bottom, corner, tile );
			DrawSpace( sb, def, rect, border, accentDepth );
		}

		sb.AppendLine( "</svg>" );
		return sb.ToString();
	}

	private void DrawSpace( StringBuilder sb, SpaceDef def, Rect rect, float border, float accentDepth )
	{
		var fill = Css( GetSpaceColor( def ) );
		sb.AppendLine( $"<rect x=\"{rect.Left:0.###}\" y=\"{rect.Top:0.###}\" width=\"{rect.Width:0.###}\" height=\"{rect.Height:0.###}\" fill=\"{fill}\" stroke=\"{Css( LineColor )}\" stroke-width=\"{border:0.###}\"/>" );

		if ( def.Type == SpaceType.Property && def.ColorGroup != ColorGroup.None )
		{
			var accent = GetAccentRect( def.Index, rect, accentDepth );
			sb.AppendLine( $"<rect x=\"{accent.Left:0.###}\" y=\"{accent.Top:0.###}\" width=\"{accent.Width:0.###}\" height=\"{accent.Height:0.###}\" fill=\"{Css( GetColorGroupColor( def.ColorGroup ) )}\" stroke=\"{Css( LineColor )}\" stroke-width=\"{border * 0.65f:0.###}\"/>" );
		}

		DrawLabel( sb, def, rect );
	}

	private void DrawLabel( StringBuilder sb, SpaceDef def, Rect rect )
	{
		var text = WebUtility.HtmlEncode( def.DisplayName.Replace( "\n", " " ) );
		var side = GetBoardSide( def.Index );
		var fontSize = def.Index % 10 == 0 ? rect.Height * 0.18f : MathF.Min( rect.Width, rect.Height ) * 0.20f;
		var cx = rect.Center.x;
		var cy = rect.Center.y;
		var width = MathF.Max( 1f, rect.Width * 0.82f );
		var angle = side switch
		{
			BoardSide.Left => 90f,
			BoardSide.Top => 180f,
			BoardSide.Right => -90f,
			_ => 0f
		};

		sb.AppendLine(
			$"<text x=\"{cx:0.###}\" y=\"{cy:0.###}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"{fontSize:0.###}\" font-weight=\"800\" fill=\"{Css( TextColor )}\" transform=\"rotate({angle:0.###} {cx:0.###} {cy:0.###})\" textLength=\"{width:0.###}\" lengthAdjust=\"spacingAndGlyphs\">{text}</text>"
		);
	}

	private void RebuildPreviewPlane()
	{
		if ( ClearExistingPreview )
			ClearPreviewPlane();

		previewObject = new GameObject( true, "GeneratedBoardTexturePreview" );
		previewObject.SetParent( GameObject );
		previewObject.LocalPosition = Vector3.Up * 0.15f;

		var half = PreviewWorldSize * 0.5f;
		var mesh = new PolygonMesh();
		var vertices = mesh.AddVertices(
			new Vector3( -half, -half, 0f ),
			new Vector3( half, -half, 0f ),
			new Vector3( half, half, 0f ),
			new Vector3( -half, half, 0f )
		);
		var face = mesh.AddFace( vertices );
		mesh.SetFaceMaterial( face, GeneratedMaterial );
		mesh.SetTextureScale( face, Vector2.One );
		mesh.SetTextureOffset( face, Vector2.Zero );
		mesh.SetFaceTextureCoords(
			face,
			new[]
			{
				new Vector2( 0f, 1f ),
				new Vector2( 1f, 1f ),
				new Vector2( 1f, 0f ),
				new Vector2( 0f, 0f )
			}
		);

		mesh.Rebuild();

		var meshComponent = previewObject.Components.Create<MeshComponent>( false );
		meshComponent.Mesh = mesh;
		meshComponent.SmoothingAngle = 0f;
		meshComponent.Color = Color.White;
		meshComponent.RebuildMesh();
		meshComponent.Enabled = true;
	}

	private void ClearPreviewPlane()
	{
		previewObject?.Destroy();
		previewObject = null;

		foreach ( var child in GameObject.Children.Where( x => x.Name == "GeneratedBoardTexturePreview" ).ToArray() )
			child.Destroy();
	}

	private static Rect GetSpaceRect( int index, float left, float top, float right, float bottom, float corner, float tile )
	{
		if ( index == 0 )
			return new Rect( right - corner, bottom - corner, corner, corner );
		if ( index == 10 )
			return new Rect( left, bottom - corner, corner, corner );
		if ( index == 20 )
			return new Rect( left, top, corner, corner );
		if ( index == 30 )
			return new Rect( right - corner, top, corner, corner );

		if ( index is > 0 and < 10 )
			return new Rect( right - corner - index * tile, bottom - corner, tile, corner );

		if ( index is > 10 and < 20 )
			return new Rect( left, bottom - corner - (index - 10) * tile, corner, tile );

		if ( index is > 20 and < 30 )
			return new Rect( left + corner + (index - 21) * tile, top, tile, corner );

		return new Rect( right - corner, top + corner + (index - 31) * tile, corner, tile );
	}

	private static Rect GetAccentRect( int index, Rect rect, float depth )
	{
		return GetBoardSide( index ) switch
		{
			BoardSide.Bottom => new Rect( rect.Left, rect.Top, rect.Width, depth ),
			BoardSide.Left => new Rect( rect.Right - depth, rect.Top, depth, rect.Height ),
			BoardSide.Top => new Rect( rect.Left, rect.Bottom - depth, rect.Width, depth ),
			BoardSide.Right => new Rect( rect.Left, rect.Top, depth, rect.Height ),
			_ => rect
		};
	}

	private static BoardSide GetBoardSide( int index )
	{
		if ( index < 10 )
			return BoardSide.Bottom;
		if ( index < 20 )
			return BoardSide.Left;
		if ( index < 30 )
			return BoardSide.Top;

		return BoardSide.Right;
	}

	private static string Css( Color color )
	{
		return $"#{ToByte( color.r ):X2}{ToByte( color.g ):X2}{ToByte( color.b ):X2}";
	}

	private static int ToByte( float value )
	{
		return (int)(Math.Clamp( value, 0f, 1f ) * 255f);
	}

	private static Color GetSpaceColor( SpaceDef def )
	{
		return def.Type switch
		{
			SpaceType.Chance => new Color( 0.95f, 0.82f, 0.28f ),
			SpaceType.CommunityChest => new Color( 0.35f, 0.73f, 0.85f ),
			SpaceType.Tax => new Color( 0.92f, 0.92f, 0.86f ),
			SpaceType.Railroad => new Color( 0.82f, 0.84f, 0.82f ),
			SpaceType.Utility => new Color( 0.81f, 0.88f, 0.72f ),
			_ => new Color( 0.92f, 0.88f, 0.78f )
		};
	}

	private static Color GetColorGroupColor( ColorGroup colorGroup )
	{
		return colorGroup switch
		{
			ColorGroup.Brown => new Color( 0.40f, 0.20f, 0.10f ),
			ColorGroup.LightBlue => new Color( 0.43f, 0.82f, 0.95f ),
			ColorGroup.Pink => new Color( 0.87f, 0.28f, 0.60f ),
			ColorGroup.Orange => new Color( 0.95f, 0.47f, 0.12f ),
			ColorGroup.Red => new Color( 0.82f, 0.08f, 0.10f ),
			ColorGroup.Yellow => new Color( 0.96f, 0.84f, 0.15f ),
			ColorGroup.Green => new Color( 0.12f, 0.55f, 0.24f ),
			ColorGroup.DarkBlue => new Color( 0.05f, 0.13f, 0.48f ),
			_ => Color.White
		};
	}
}
