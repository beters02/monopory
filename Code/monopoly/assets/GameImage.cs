using System;
using Sandbox;

public sealed class GameImage
{
	public GameImage( string path )
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

	public override string ToString() => Path;

	public static implicit operator GameImage( string path ) => new( path );
}
