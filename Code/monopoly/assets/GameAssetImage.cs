using System;
using Sandbox;

public abstract class GameAssetImage<TSelf> where TSelf : GameAssetImage<TSelf>
{
	protected GameAssetImage( string path )
	{
		Path = path ?? "";
	}

	public string Path { get; }

	private Texture loadedTexture;

	public Texture Texture
	{
		get
		{
			if ( !IsAssigned )
				return null;

			loadedTexture ??= Texture.Load( Path );
			return loadedTexture;
		}
	}

	public bool IsAssigned => !string.IsNullOrWhiteSpace( Path );

	public bool Preload() => Texture is not null;

	public override string ToString() => Path;
}