using System.Collections.Generic;

namespace Sandbox;

[AssetType( Name = "CARL Retarget Job", Extension = "crtjob", Category = "Tools" )]
public sealed class CitizenRetargetJob : GameResource
{
	[Property] public string SourceFbxPath { get; set; } = string.Empty;
	[Property] public string SourceProfilePath { get; set; } = string.Empty;
	[Property] public string MappingProfilePath { get; set; } = "tools/citizen_retarget/profiles/ual2_to_citizen.crtmap";
	[Property] public string TargetVmdlPath { get; set; } = "models/citizen_custom/citizen_retarget.vmdl";
	[Property] public string OutputAnimationFolder { get; set; } = "models/citizen_custom/animations/citizen_retarget";
	[Property] public string SequencePrefix { get; set; } = "citizen_retarget_";
	[Property] public string TargetPosePresetId { get; set; } = "citizen_t_pose_calibrated_v1";
	[Property] public CitizenRetargetRootMotionMode RootMotionMode { get; set; } = CitizenRetargetRootMotionMode.Keep;
	[Property] public bool ImportHands { get; set; } = true;
	[Property] public bool GeneratePreviewVideo { get; set; }
	[Property] public bool GenerateComparisonVideo { get; set; }
	[Property] public bool AutoOpenModelDoc { get; set; }
	[Property] public List<string> SelectedClipNames { get; set; } = new();
	[Property] public string LastImportedClip { get; set; } = string.Empty;
	[Property] public string LastSuccessfulRunId { get; set; } = string.Empty;
	[Property] public string LastSuccessfulSequenceName { get; set; } = string.Empty;
	[Property] public string LastManifestPath { get; set; } = string.Empty;
	[Property] public List<RetargetRunHistoryEntry> RecentRuns { get; set; } = new();
}
