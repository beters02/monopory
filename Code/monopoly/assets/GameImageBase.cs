using System;
using Sandbox;

public abstract class GameImageBase<TSelf> where TSelf : GameImageBase<TSelf>
{
	protected GameImageBase( string path )
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

	public bool Reload()
	{
		loadedTexture = null;
		return Preload();
	}

	public override string ToString() => Path;
}