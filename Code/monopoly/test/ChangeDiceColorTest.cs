using System;

public sealed class ChangeDiceColorTest : Component
{

    DecalDefinition ForegroundDef;
    DecalDefinition BackgroundDef;

	protected override void OnUpdate()
	{
		if (Input.Pressed("ParticleTest"))
        {

            if (ForegroundDef is null)
            {
                ForegroundDef = new();
                ForegroundDef.Tint = ColorUtils.FromHex( "#d9b45f" );

                int dicecounter = -1;
                foreach ( DiceComponent dice in Scene.GetComponentsInChildren<DiceComponent>() )
                {
                    dicecounter++;
                    Log.Info($"Dice{dicecounter}");
                    foreach (GameObject child in dice.GameObject.Children)
                    {
                        Log.Info($"Dice{child.Name}");
                        if (child.Name.Contains("Side_"))
                        {
                            
                            if (child.Name.Contains("Bg"))
                            {
                                child.GetComponent<Decal>().ColorTint = Color.Red;
                            }
                        }
                    }
                }
            }

            
        }
	}
}