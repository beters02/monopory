using System;

public sealed class ChangeDiceColorTest : Component
{   
    bool didInit = false;
    bool isChanged = false;
    ParticleGradient defaultColorTint;

    ParticleGradient NewColorTint = Color.Red;

    private void UpdateDiceColor()
    {
        ParticleGradient color = isChanged ? defaultColorTint : NewColorTint;
        isChanged = !isChanged;

        int dicecounter = -1;
        foreach ( DiceComponent dice in Scene.GetComponentsInChildren<DiceComponent>() )
        {
            dicecounter++;
            foreach (GameObject child in dice.GameObject.Children)
                if (child.Name.Contains("Side_") && child.Name.Contains("Bg"))
                    child.GetComponent<Decal>().ColorTint = color;
        }
    }

    private ParticleGradient GetDefaultBackgroundColorTint()
    {
        foreach ( DiceComponent dice in Scene.GetComponentsInChildren<DiceComponent>() )
        {
            foreach (GameObject child in dice.GameObject.Children)
                if (child.Name.Contains("Side_") && child.Name.Contains("Bg"))
                    return child.GetComponent<Decal>().ColorTint;
        }

        return Color.White;
    }

	protected override void OnUpdate()
	{
        Log.Info(defaultColorTint);
		if (Input.Pressed("ParticleTest") && !didInit)
        {
            didInit = true;
            defaultColorTint = GetDefaultBackgroundColorTint();
            UpdateDiceColor();
        }
	}
}