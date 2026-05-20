using System;
using System.Collections.Generic;
using Sandbox;

public sealed class CharacterAttachmentSlots : Component
{
	[Property] public string HatAttachmentId { get; set; } = CharacterAttachmentData.GetDefaultId( CharacterAttachmentSlot.Hat );
	[Property] public string GlassesAttachmentId { get; set; }
	[Property] public string BackAttachmentId { get; set; }
	[Property] public string HandAttachmentId { get; set; }

	private static readonly string[] BoneNameFallbacks =
	{
		"head",
		"Head",
		"HEAD",
		"mixamorig:Head",
		"mixamorig_Head",
		"b_head",
		"Bip01 Head",
		"ValveBiped.Bip01_Head1",
		"neck",
		"Neck"
	};

	private readonly Dictionary<CharacterAttachmentSlot, AttachedSlotState> attachedSlots = new();
	private SkinnedModelRenderer bodyRenderer;

	protected override void OnStart()
	{
		bodyRenderer = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		RefreshAttachments();
	}

	protected override void OnUpdate()
	{
		if ( bodyRenderer is null || !bodyRenderer.IsValid() )
			bodyRenderer = GameObject.GetComponentInChildren<SkinnedModelRenderer>();

		RefreshAttachments();
	}

	protected override void OnDestroy()
	{
		ClearAllAttachments();
	}

	private void RefreshAttachments()
	{
		if ( bodyRenderer is null )
			return;

		bodyRenderer.CreateBoneObjects = true;

		RefreshSlot( CharacterAttachmentSlot.Hat, HatAttachmentId );
		RefreshSlot( CharacterAttachmentSlot.Glasses, GlassesAttachmentId );
		RefreshSlot( CharacterAttachmentSlot.Back, BackAttachmentId );
		RefreshSlot( CharacterAttachmentSlot.Hand, HandAttachmentId );
	}

	private void RefreshSlot( CharacterAttachmentSlot slot, string attachmentId )
	{
		if ( string.IsNullOrWhiteSpace( attachmentId ) )
		{
			ClearSlot( slot );
			return;
		}

		if ( !CharacterAttachmentData.TryGetDefinition( attachmentId, out var definition ) )
		{
			Log.Warning( $"CharacterAttachmentSlots could not resolve attachment id '{attachmentId}' for slot '{slot}'." );
			ClearSlot( slot );
			return;
		}

		if ( definition.Slot != slot )
		{
			Log.Warning( $"CharacterAttachmentSlots attachment id '{attachmentId}' belongs to slot '{definition.Slot}', not '{slot}'." );
			ClearSlot( slot );
			return;
		}

		if ( !ShouldRebuildSlot( slot, definition ) )
			return;

		ClearSlot( slot );

		var boneObject = TryGetBoneObject( definition.BoneName, out var resolvedBoneName );
		if ( boneObject is null )
			return;

		var model = Model.Load( definition.ModelPath );
		if ( model is null )
		{
			Log.Warning( $"CharacterAttachmentSlots could not load model '{definition.ModelPath}' for attachment '{definition.Id}'." );
			return;
		}

		var attachmentObject = new GameObject( true, $"{slot}Attachment" );
		attachmentObject.SetParent( boneObject );
		attachmentObject.LocalPosition = definition.LocalPosition;
		attachmentObject.LocalRotation = Rotation.From( definition.LocalAngles );
		attachmentObject.LocalScale = definition.LocalScale;

		var modelRenderer = attachmentObject.Components.Create<ModelRenderer>();
		modelRenderer.Model = model;
		modelRenderer.MaterialOverride = bodyRenderer.MaterialOverride;
		modelRenderer.Tint = bodyRenderer.Tint;

		attachedSlots[slot] = new AttachedSlotState
		{
			AttachmentId = definition.Id,
			BoneName = resolvedBoneName,
			Object = attachmentObject,
			Renderer = modelRenderer
		};
	}

	private bool ShouldRebuildSlot( CharacterAttachmentSlot slot, CharacterAttachmentDefinition definition )
	{
		if ( !attachedSlots.TryGetValue( slot, out var state ) )
			return true;

		if ( state.Object is null || !state.Object.IsValid() || state.Renderer is null || !state.Renderer.IsValid() )
			return true;

		return !string.Equals( state.AttachmentId, definition.Id, StringComparison.OrdinalIgnoreCase )
			|| !string.Equals( state.BoneName, definition.BoneName, StringComparison.OrdinalIgnoreCase );
	}

	private GameObject TryGetBoneObject( string preferredBoneName, out string resolvedBoneName )
	{
		resolvedBoneName = null;

		foreach ( var boneName in GetBoneCandidates( preferredBoneName ) )
		{
			var boneObject = bodyRenderer.GetBoneObject( boneName );
			if ( boneObject is null )
				continue;

			resolvedBoneName = boneName;
			return boneObject;
		}

		return null;
	}

	private IEnumerable<string> GetBoneCandidates( string preferredBoneName )
	{
		if ( !string.IsNullOrWhiteSpace( preferredBoneName ) )
			yield return preferredBoneName.Trim();

		foreach ( var fallback in BoneNameFallbacks )
		{
			if ( string.Equals( fallback, preferredBoneName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			yield return fallback;
		}
	}

	private void ClearAllAttachments()
	{
		foreach ( var slot in attachedSlots.Keys.ToArray() )
			ClearSlot( slot );
	}

	private void ClearSlot( CharacterAttachmentSlot slot )
	{
		if ( !attachedSlots.Remove( slot, out var state ) )
			return;

		if ( state.Object is not null && state.Object.IsValid() )
			state.Object.Destroy();
	}

	private sealed class AttachedSlotState
	{
		public string AttachmentId { get; init; }
		public string BoneName { get; init; }
		public GameObject Object { get; init; }
		public ModelRenderer Renderer { get; init; }
	}
}
