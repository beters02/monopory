using System;
using System.Collections.Generic;

public class Logger
{
    public bool IsEnabled {get; private set;} = true;
    public string Prefix {get; private set;} = "DefaultLogger";
	public bool ErrorAutoSends {get; private set;} = true;
	public bool WarningAutoSends {get; private set;} = false;
	public int PendingCount => logs.Count;

	private readonly List<LogEntry> logs = new();

	public Logger(string prefix)
    {
        Prefix = prefix;
    }
	public Logger(string prefix, bool isEnabled)
    {
        SetEnabled(isEnabled);
        Prefix = prefix;
    }

	public void SetEnabled(bool isEnabled) => DoSetEnabled(isEnabled);

	public void Info(FormattableString msg) => DoLogInfo(msg);
	public void Info(object msg) => DoLogInfo(msg);

	public void Warning(FormattableString msg) => DoLogWarning(msg);
	public void Warning(object msg) => DoLogWarning(msg);

	public void Error(FormattableString msg) => DoLogError(msg);
	public void Error(object msg) => DoLogError(msg);
	public void Send(bool clearAfterSend = true)
	{
		if (!IsEnabled) return;

		foreach (var entry in logs)
		{
			switch (entry.Level)
			{
				case LogLevel.Warning:
					Log.Warning(entry.Message);
					break;

				case LogLevel.Error:
					Log.Error(entry.Message);
					break;

				default:
					Log.Info(entry.Message);
					break;
			}
		}

		if (clearAfterSend)
			logs.Clear();
	}

	public void Clear() => logs.Clear();

	private void DoLogInfo(object msg)
	{
		Buffer(LogLevel.Info, msg);
	}

	private void DoLogInfo(FormattableString msg)
	{
		Buffer(LogLevel.Info, msg);
	}

	private void DoLogWarning(object msg)
	{
		Buffer(LogLevel.Warning, msg);
		if (WarningAutoSends)
			Send();
	}

	private void DoLogWarning(FormattableString msg)
	{
		Buffer(LogLevel.Warning, msg);
		if (WarningAutoSends)
			Send();
	}

	private void DoLogError(object msg)
	{
		Buffer(LogLevel.Error, msg);
		if (ErrorAutoSends)
			Send();
	}

	private void DoLogError(FormattableString msg)
	{
		Buffer(LogLevel.Error, msg);
		if (ErrorAutoSends)
			Send();
	}

	private void DoSetEnabled(bool isEnabled) => IsEnabled = isEnabled;

	private void Buffer(LogLevel level, object msg)
	{
		if (!IsEnabled) return;

		var message = $"[{Prefix}] {msg}";
		if (logs.Count > 0 && logs[^1].Level == level && logs[^1].Message == message)
			return;

		logs.Add(new LogEntry(level, message));
	}

	private enum LogLevel
	{
		Info,
		Warning,
		Error
	}

	private readonly struct LogEntry
	{
		public LogLevel Level { get; }
		public string Message { get; }

		public LogEntry(LogLevel level, string message)
		{
			Level = level;
			Message = message;
		}
	}
}
