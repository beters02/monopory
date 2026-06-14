using System.Runtime.CompilerServices;
using Sandbox.VR;

public enum InputCode
{
    LeftShift,
    RightShift,
    Escape
}

public sealed class InputHelperService : Component
{

    private static InputHelperService Instance;

    private static Dictionary<InputCode, bool> InputDownStates = new()
    {
        [InputCode.LeftShift] = false,
        [InputCode.RightShift] = false,
        [InputCode.Escape] = false
    };

	protected override void OnAwake()
	{
		base.OnAwake();

        Instance = this;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
        UpdateAllInputs();
	}

    public static bool IsInputCodeDown( InputCode code )
    {
        if ( !InputDownStates.TryGetValue(code, out bool isDown) )
            return false;

        return isDown;
    }

    //

    private void UpdateAllInputs()
    {
        UpdateLeftShiftDown();
        UpdateRightShiftDown();
        UpdateEscapeDown();
    }

    private void UpdateInputDownState(InputCode str, bool isDown)
    {
        if ( !InputDownStates.TryGetValue(str, out _) )
            return;
        
        InputDownStates[str] = isDown;
    }

    private void UpdateLeftShiftDown() =>
        UpdateInputDownState(InputCode.LeftShift, Input.Down( "Run" ) 
            || Input.Keyboard.Down( "SHIFT" ));

    private void UpdateRightShiftDown() => 
        UpdateInputDownState(InputCode.RightShift, Input.Keyboard.Down( "RightShift" ));

    // Update Escape Down is special because we need to cancel S&box's escape pressed.
    private void UpdateEscapeDown()
    {
        if ( Input.EscapePressed )
            Input.EscapePressed = false;
    }
}