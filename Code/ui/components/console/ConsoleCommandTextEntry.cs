using System;
using Sandbox.UI;

namespace Sandbox.ui.components;

public sealed class ConsoleCommandTextEntry : TextEntry
{
	public Action OnConsoleCloseRequested { get; set; }

	public override void OnKeyTyped( char key )
	{
		if ( key == '=' )
		{
			OnConsoleCloseRequested?.Invoke();
			return;
		}

		base.OnKeyTyped( key );
	}
}
