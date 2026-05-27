using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Sandbox.ui.components;

public partial class StandaloneConsole
{
#if STANDALONE
	private FileInfo TailedLogFile;
	private long TailedLogPosition;
	private DateTime NextLogPollTime;

	private void PollStandaloneLogFile()
	{
		if ( DateTime.UtcNow < NextLogPollTime )
			return;

		NextLogPollTime = DateTime.UtcNow.AddMilliseconds( 150 );

		var logFile = FindActiveLogFile();
		if ( logFile is null )
			return;

		if ( TailedLogFile is null ||
			!string.Equals( TailedLogFile.FullName, logFile.FullName, StringComparison.OrdinalIgnoreCase ) )
		{
			TailedLogFile = logFile;
			TailedLogPosition = logFile.Exists ? logFile.Length : 0;
			return;
		}

		logFile.Refresh();
		if ( !logFile.Exists )
			return;

		if ( logFile.Length < TailedLogPosition )
			TailedLogPosition = 0;

		if ( logFile.Length == TailedLogPosition )
			return;

		try
		{
			using var stream = new FileStream( logFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete );
			stream.Seek( TailedLogPosition, SeekOrigin.Begin );

			using var reader = new StreamReader( stream, Encoding.UTF8, true );
			var text = reader.ReadToEnd();
			TailedLogPosition = stream.Position;

			foreach ( var line in SplitLogLines( text ) )
				AddLogFileLine( line );
		}
		catch ( Exception exception )
		{
			PendingLogEntries.Enqueue( new ConsoleEntry( $"Could not read active log file: {exception.Message}", "wrn", DateTime.Now ) );
		}
	}

	private static FileInfo FindActiveLogFile()
	{
		try
		{
			var logDirectory = new DirectoryInfo( Path.Combine( Environment.CurrentDirectory, "logs" ) );
			if ( !logDirectory.Exists )
				return null;

			return logDirectory
				.GetFiles( "*.log" )
				.OrderByDescending( file => file.LastWriteTimeUtc )
				.FirstOrDefault();
		}
		catch
		{
			return null;
		}
	}

	private static void AddLogFileLine( string line )
	{
		if ( string.IsNullOrWhiteSpace( line ) )
			return;

		var kind = GetLogFileKind( line );
		var message = StripLogFilePrefix( line );
		PendingLogEntries.Enqueue( new ConsoleEntry( message, kind, DateTime.Now ) );
	}

	private static string GetLogFileKind( string line )
	{
		if ( line.Contains( "Error |", StringComparison.OrdinalIgnoreCase ) ||
			line.Contains( "Exception", StringComparison.OrdinalIgnoreCase ) )
			return "err";

		if ( line.Contains( "Warning |", StringComparison.OrdinalIgnoreCase ) ||
			line.Contains( "Warn |", StringComparison.OrdinalIgnoreCase ) )
			return "wrn";

		return "msg";
	}

	private static string StripLogFilePrefix( string line )
	{
		if ( line.Length > 24 && char.IsDigit( line[0] ) )
		{
			var firstTab = line.IndexOf( '\t' );
			if ( firstTab >= 0 && firstTab + 1 < line.Length )
				return line.Substring( firstTab + 1 );
		}

		return line;
	}
#else
	private void PollStandaloneLogFile()
	{
	}
#endif
}
