public static class GameAssetsHotload
{
	[EditorEvent.Hotload]
	private static void OnHotload()
	{
		GameAssets.ReloadAll();
		DiceSkinCatalog.ReloadAll();
		Log.Info( "GameAssets cache cleared after hotload." );
	}

	
}
