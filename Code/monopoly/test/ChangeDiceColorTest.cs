using System;
using Sandbox;

public sealed class ChangeDiceColorTest : Component
{
	bool didInit = false;
	bool isChanged = false;
	ParticleGradient defaultBackgroundColor = Color.White;
	ParticleGradient defaultDotColor = Color.Black;
	ParticleGradient newBackgroundColor = Color.Red;

	private void UpdateDiceColor()
	{
		var backgroundColor = isChanged ? defaultBackgroundColor : newBackgroundColor;
		isChanged = !isChanged;

		foreach ( var dice in Scene.GetComponentsInChildren<DiceComponent>() )
		{
			var visual = dice.GetComponent<DiceVisual>() ?? dice.Components.Create<DiceVisual>();
			visual.ApplyTintColors( backgroundColor, defaultDotColor );
		}
	}

	private void CaptureDefaultColors()
	{
		foreach ( var dice in Scene.GetComponentsInChildren<DiceComponent>() )
		{
			var visual = dice.GetComponent<DiceVisual>();
			if ( visual is null )
				continue;

			defaultBackgroundColor = visual.GetBackgroundColor();
			defaultDotColor = visual.GetDotColor();
			return;
		}
	}

	protected override void OnUpdate()
	{
		if ( Input.Pressed( "ParticleTest" ) && !didInit )
		{
			didInit = true;
			CaptureDefaultColors();
			UpdateDiceColor();
		}
	}
}
