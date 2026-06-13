using System;

public static partial class AppSettingsSchema
{
    private static readonly List<InputAction> editableKeybinds = new();

    private static readonly string[] EditableKeybindNames =
	[
		"RollDice",
		"EndTurn"
	];

    private static List<InputAction> GetEditableKeybinds()
	{
		editableKeybinds.Clear();

		foreach ( var actionName in EditableKeybindNames )
		{
			if ( TryGetInputActionFromString(actionName, out InputAction inputAction) )
            {
                editableKeybinds.Add(inputAction);
                continue;
            }

            Log.Warning($"Could not find InputAction from EditableKeybind name {actionName}");
		}

		return editableKeybinds;
	}

    private static bool TryGetInputActionFromString(string actionName, out InputAction inputAction)
    {
        var action = Input.GetActions().FirstOrDefault( candidate => string.Equals( candidate?.Name, actionName, StringComparison.Ordinal ) );
        if ( action is not null )
        {
            inputAction = action;
            return true;
        }

        inputAction = null;
        return false;
    }
}