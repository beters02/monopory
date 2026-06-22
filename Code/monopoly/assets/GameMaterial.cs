using System;
using Sandbox;

public sealed class GameMaterial
{
	public GameMaterial( string path )
    {
        Path = path ?? "";
    }

	public string Path { get; init; }

    private Material loadedMaterial;

    public Material Material
    {
        get
        {
            if ( !IsAssigned )
                return null;

            loadedMaterial ??= Material.Load( Path );
            return loadedMaterial;
        }
    }

	public bool IsAssigned => !string.IsNullOrWhiteSpace( Path );

	public bool Preload()
	{
		return Material is not null;
	}

	public bool Reload()
	{
		loadedMaterial = null;
		return Preload();
	}

	public override string ToString() => Path;

	public static implicit operator GameMaterial( string path ) => new( path );
}
