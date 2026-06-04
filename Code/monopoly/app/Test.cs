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

    public async Task<Forkbox.Steamworks.AuthToken> getToken()
    {
        return Forkbox.Steamworks.SteamUser.GetAuthToken(null);
    }
}