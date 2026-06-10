using Sandbox;
using System;
using System.Text.RegularExpressions;

public static class GameSaveService
{
	public const int CurrentSchemaVersion = 1;
	public const double DefaultAutosaveRetentionHours = 48;
	private const string SaveDirectory = "saves";
	private const string ManualSaveDirectory = "saves/manual";
	private const string AutosaveDirectory = "saves/autosaves";
	private const string LastLoadedFileName = "saves/last-loaded-save.txt";
	private const string AutosavePrefix = "autosave";

	public static TimeSpan AutosaveRetention { get; set; } = TimeSpan.FromHours( DefaultAutosaveRetentionHours );

	public static IReadOnlyList<GameSaveSummary> ListSaves()
	{
		return ListSaves( null );
	}

	public static IReadOnlyList<GameSaveSummary> ListSaves( GameSaveType? saveType )
	{
		EnsureSaveDirectory();
		DeleteExpiredAutosaves();

		var lastLoadedSaveId = GetLastLoadedSaveId();
		var saves = new List<GameSaveSummary>();
		foreach ( var path in GetSavePaths( saveType ) )
		{
			try
			{
				var save = FileSystem.Data.ReadJson<GameSaveFile>( path );
				if ( save?.Summary is null || string.IsNullOrWhiteSpace( save.Summary.SaveId ) )
					continue;

				if ( save.Summary.SchemaVersion != CurrentSchemaVersion )
					continue;

				save.Summary.IsLastLoadedRestorePoint = string.Equals( save.Summary.SaveId, lastLoadedSaveId, StringComparison.Ordinal );
				saves.Add( save.Summary );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Could not read save file '{path}': {ex.Message}" );
			}
		}

		return saves
			.OrderByDescending( save => save.IsLastLoadedRestorePoint )
			.ThenBy( save => save.GameIdentifier )
			.ThenByDescending( save => ParseTimestamp( save.UpdatedAtUtc ) )
			.ToList();
	}

	public static GameSaveFile ReadSave( string saveId )
	{
		if ( string.IsNullOrWhiteSpace( saveId ) )
			return null;

		var path = FindSavePath( saveId );
		if ( !FileSystem.Data.FileExists( path ) )
			return null;

		try
		{
			var save = FileSystem.Data.ReadJson<GameSaveFile>( path );
			if ( save?.Summary is null || save.Snapshot is null )
				return null;

			if ( save.Summary.SchemaVersion != CurrentSchemaVersion )
				return null;

			return save;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Could not read save '{saveId}': {ex.Message}" );
			return null;
		}
	}

	public static bool WriteSave( GameSaveFile save, GameSaveType type )
	{
		if ( save?.Summary is null || save.Snapshot is null )
			return false;

		EnsureSaveDirectory();
		DeleteExpiredAutosaves();

		save.Summary.SaveType = type;
		save.Summary.SchemaVersion = CurrentSchemaVersion;
		save.Summary.GameVersion = MonopolyApp.GameVersion;
		if ( string.IsNullOrWhiteSpace( save.Summary.GameIdentifier ) )
			save.Summary.GameIdentifier = CreateGameIdentifier( save.Summary.PlayerNames );

		if ( string.IsNullOrWhiteSpace( save.Summary.SaveId ) )
			save.Summary.SaveId = CreateSaveId( save.Summary.DisplayName, save.Summary.GameIdentifier );

		if ( type == GameSaveType.Autosave )
			save.Summary.SaveId = $"{AutosavePrefix}-{SanitizeSaveId( save.Summary.GameIdentifier )}";

		var now = DateTimeOffset.UtcNow.ToString( "O" );
		if ( string.IsNullOrWhiteSpace( save.Summary.CreatedAtUtc ) )
			save.Summary.CreatedAtUtc = now;

		save.Summary.UpdatedAtUtc = now;

		try
		{
			FileSystem.Data.WriteJson( GetSavePath( save.Summary.SaveId, type ), save );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Could not write save '{save.Summary.SaveId}': {ex.Message}" );
			return false;
		}
	}

	public static bool DeleteSave( string saveId )
	{
		if ( string.IsNullOrWhiteSpace( saveId ) )
			return false;

		var path = FindSavePath( saveId );
		if ( !FileSystem.Data.FileExists( path ) )
			return false;

		try
		{
			FileSystem.Data.DeleteFile( path );
			if ( string.Equals( saveId, GetLastLoadedSaveId(), StringComparison.Ordinal ) )
				SetLastLoadedSaveId( "" );

			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Could not delete save '{saveId}': {ex.Message}" );
			return false;
		}
	}

	public static string GetLastLoadedSaveId()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( LastLoadedFileName ) )
				return "";

			return FileSystem.Data.ReadAllText( LastLoadedFileName )?.Trim() ?? "";
		}
		catch
		{
			return "";
		}
	}

	public static void SetLastLoadedSaveId( string saveId )
	{
		EnsureSaveDirectory();
		FileSystem.Data.WriteAllText( LastLoadedFileName, saveId ?? "" );
	}

	public static string GetAutosaveId( string gameIdentifier )
	{
		return $"{AutosavePrefix}-{SanitizeSaveId( gameIdentifier )}";
	}

	public static string CreateSaveId( string displayName, string gameIdentifier = "" )
	{
		var name = string.IsNullOrWhiteSpace( displayName ) ? "manual-save" : displayName.Trim().ToLowerInvariant();
		name = Regex.Replace( name, @"[^a-z0-9\-_\s]+", "" );
		name = Regex.Replace( name, @"\s+", "-" ).Trim( '-' );
		if ( string.IsNullOrWhiteSpace( name ) )
			name = "manual-save";

		var gamePart = string.IsNullOrWhiteSpace( gameIdentifier ) ? "" : $"{SanitizeSaveId( gameIdentifier )}-";
		return $"{gamePart}{name}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
	}

	public static string CreateGameIdentifier( IReadOnlyList<string> playerNames )
	{
		var seed = string.Join( "-", playerNames ?? new List<string>() );
		if ( string.IsNullOrWhiteSpace( seed ) )
			seed = "game";

		var normalized = Regex.Replace( seed.ToLowerInvariant(), @"[^a-z0-9]+", "" );
		if ( normalized.Length > 10 )
			normalized = normalized[..10];

		if ( string.IsNullOrWhiteSpace( normalized ) )
			normalized = "game";

		return $"{normalized}-{DateTimeOffset.UtcNow:MMdd-HHmm}";
	}

	private static string GetSavePath( string saveId, GameSaveType saveType )
	{
		var directory = saveType == GameSaveType.Autosave ? AutosaveDirectory : ManualSaveDirectory;
		return $"{directory}/{SanitizeSaveId( saveId )}.json";
	}

	private static string FindSavePath( string saveId )
	{
		var manualPath = GetSavePath( saveId, GameSaveType.Manual );
		if ( FileSystem.Data.FileExists( manualPath ) )
			return manualPath;

		var autosavePath = GetSavePath( saveId, GameSaveType.Autosave );
		if ( FileSystem.Data.FileExists( autosavePath ) )
			return autosavePath;

		var legacyPath = $"{SaveDirectory}/{SanitizeSaveId( saveId )}.json";
		return legacyPath;
	}

	private static string SanitizeSaveId( string saveId )
	{
		saveId = saveId ?? "";
		saveId = Regex.Replace( saveId, @"[^a-zA-Z0-9\-_]+", "-" ).Trim( '-' );
		return string.IsNullOrWhiteSpace( saveId ) ? "save" : saveId;
	}

	private static void EnsureSaveDirectory()
	{
		FileSystem.Data.CreateDirectory( SaveDirectory );
		FileSystem.Data.CreateDirectory( ManualSaveDirectory );
		FileSystem.Data.CreateDirectory( AutosaveDirectory );
	}

	private static void DeleteExpiredAutosaves()
	{
		if ( AutosaveRetention <= TimeSpan.Zero )
			return;

		foreach ( var fileName in FileSystem.Data.FindFile( AutosaveDirectory, "*.json" ) )
		{
			var path = $"{AutosaveDirectory}/{fileName}";
			try
			{
				var save = FileSystem.Data.ReadJson<GameSaveFile>( path );
				if ( save?.Summary is null || save.Summary.SaveType != GameSaveType.Autosave )
					continue;

				var timestamp = ParseTimestamp( save.Summary.UpdatedAtUtc );
				if ( timestamp == DateTimeOffset.MinValue )
					timestamp = ParseTimestamp( save.Summary.CreatedAtUtc );

				if ( timestamp == DateTimeOffset.MinValue || DateTimeOffset.UtcNow - timestamp <= AutosaveRetention )
					continue;

				FileSystem.Data.DeleteFile( path );
				if ( string.Equals( save.Summary.SaveId, GetLastLoadedSaveId(), StringComparison.Ordinal ) )
					SetLastLoadedSaveId( "" );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Could not inspect autosave '{path}' for retention cleanup: {ex.Message}" );
			}
		}
	}

	private static IEnumerable<string> GetSavePaths( GameSaveType? saveType )
	{
		EnsureSaveDirectory();

		if ( saveType is null or GameSaveType.Manual )
		{
			foreach ( var fileName in FileSystem.Data.FindFile( ManualSaveDirectory, "*.json" ) )
				yield return $"{ManualSaveDirectory}/{fileName}";
		}

		if ( saveType is null or GameSaveType.Autosave )
		{
			foreach ( var fileName in FileSystem.Data.FindFile( AutosaveDirectory, "*.json" ) )
				yield return $"{AutosaveDirectory}/{fileName}";
		}

		if ( saveType is null )
		{
			foreach ( var fileName in FileSystem.Data.FindFile( SaveDirectory, "*.json" ) )
				yield return $"{SaveDirectory}/{fileName}";
		}
	}

	private static DateTimeOffset ParseTimestamp( string value )
	{
		return DateTimeOffset.TryParse( value, out var parsed ) ? parsed : DateTimeOffset.MinValue;
	}
}
