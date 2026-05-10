using Sandbox;

public sealed class MonopolyBoard : Component
{
	[Property] public List<MonopolySpace> Spaces { get; set; } = new();

	public MonopolySpace GetSpace( int index )
	{
		if ( Spaces.Count == 0 )
			return null;

		index = ((index % Spaces.Count) + Spaces.Count) % Spaces.Count;
		return Spaces[index];
	}

	public Vector3 GetSpacePosition( int index )
	{
		var space = GetSpace( index );
		return space?.TokenPosition ?? Vector3.Zero;
	}
}