using System.Threading.Tasks;
using Sandbox;
using Sandbox.Services;

public class AuthTest : Component
{
	protected override void OnAwake()
	{
		base.OnAwake();

        _ = getToken();
	}

    public async Task getToken()
    {
        var token = await Auth.GetToken("sbox-network-storage");
        Log.Info(token);
    }
}