using System;
using Sandbox;

public class DebugHelper
{
	
	public bool IsEnabled {get; private set;} = true;

	public DebugHelper() {}
	public DebugHelper(bool isEnabled) => SetEnabled(isEnabled);

	public void SetEnabled(bool isEnabled) => DoSetEnabled(isEnabled);

	public void LogInfo(FormattableString msg) => DoLogInfo(msg);
	public void LogInfo(object msg) => DoLogInfo(msg);

	public void LogWarning(FormattableString msg) => DoLogWarning(msg);
	public void LogWarning(object msg) => DoLogWarning(msg);

	public void LogError(FormattableString msg) => DoLogError(msg);
	public void LogError(object msg) => DoLogError(msg);

	private void DoLogInfo(object msg)
	{
        if (!IsEnabled) return;
		Log.Info(msg);
	}

	private void DoLogInfo(FormattableString msg)
	{
		if (!IsEnabled) return;
		Log.Info(msg);
	}

	private void DoLogWarning(object msg)
	{
        if (!IsEnabled) return;
		Log.Warning(msg);
	}

	private void DoLogWarning(FormattableString msg)
	{
		if (!IsEnabled) return;
		Log.Warning(msg);
	}

	private void DoLogError(object msg)
	{
        if (!IsEnabled) return;
		Log.Error(msg);
	}

	private void DoLogError(FormattableString msg)
	{
		if (!IsEnabled) return;
		Log.Error(msg);
	}

	private void DoSetEnabled(bool isEnabled) => IsEnabled = isEnabled;

}