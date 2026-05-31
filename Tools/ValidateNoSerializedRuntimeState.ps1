param(
	[string]$Root = "Assets"
)

$ErrorActionPreference = "Stop"

$assetFiles = Get-ChildItem -Path $Root -Recurse -File -Include *.scene,*.prefab
$failures = New-Object System.Collections.Generic.List[string]

foreach ( $file in $assetFiles )
{
	$text = Get-Content -LiteralPath $file.FullName -Raw
	$relative = Resolve-Path -LiteralPath $file.FullName -Relative

	if ( $text -cmatch '"Name"\s*:\s*"PlayerState_\d+"' )
	{
		$failures.Add( "$relative contains serialized PlayerState runtime objects." )
	}

	if ( $text -cmatch '"Name"\s*:\s*"Token_\d+"' )
	{
		$failures.Add( "$relative contains serialized gameplay token runtime objects." )
	}

	if ( $text -cmatch '"MatchState"\s*:\s*"(Starting|InGame|Paused|GameOver)"' )
	{
		$failures.Add( "$relative contains a non-lobby serialized GameController MatchState." )
	}

	if ( $text -cmatch '"Players"\s*:\s*\[\s*\{' )
	{
		$failures.Add( "$relative contains serialized GameController player slot references." )
	}

	if ( $text -cmatch '"PreferredHostOwnerId"\s*:\s*[1-9]' )
	{
		$failures.Add( "$relative contains a serialized preferred host Steam ID." )
	}

	if ( $text -cmatch '"(ChatMessages|PropertyOwners|PropertyImprovements|MortgagedProperties|PendingTrades|TradeViewers|TokenPhysicsStates)"\s*:\s*\{\s*"[^"]+"\s*:' )
	{
		$failures.Add( "$relative contains non-empty serialized runtime NetDictionary state." )
	}
}

if ( $failures.Count -gt 0 )
{
	Write-Error ( "Serialized runtime state found:`n" + ($failures -join "`n") )
	exit 1
}

Write-Host "No serialized runtime match state found."
