using Sandbox;

public sealed class PlayerToken : Component
{
	[Property] public Board Board { get; set; }
	[Property] public PlayerState PlayerState { get; set; }

	[Property] public float HeightOffset { get; set; } = 8f;
	[Property] public float MoveSpeed { get; set; } = 15f;

	protected override void OnUpdate()
	{
		if ( Board is null || PlayerState is null )
			return;

		var target = Board.GetSpacePosition( PlayerState.SpaceIndex ) + Vector3.Up * HeightOffset;

        if (GameObject.WorldPosition != target)
        {
            GameObject.WorldPosition = GameObject.WorldPosition.LerpTo(
                target,
                Time.Delta * MoveSpeed
            );
        }

		
	}
}