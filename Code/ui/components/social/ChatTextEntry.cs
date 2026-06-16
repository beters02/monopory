using System;
using Sandbox.UI;

namespace Sandbox.ui.components;

public sealed class ChatTextEntry : TextEntry
{
	public Action OnCloseRequested { get; set; }

	public override void OnKeyTyped( char key )
	{
        
		if ( key == '=' )
		{
			OnCloseRequested?.Invoke();
			return;
		}

		base.OnKeyTyped( key );
	}
}
