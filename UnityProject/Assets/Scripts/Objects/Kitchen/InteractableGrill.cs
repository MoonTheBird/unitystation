using UnityEngine;
using System.Collections;

namespace Objects.Kitchen
{
	/// <summary>
	/// Allows Grill to be interacted with. Player can put food on the grill to cook it.
	/// The grill can be interacted with to check its current state.
	/// </summary>
	[RequireComponent(typeof(Grill))]
	public class InteractableGrill : RegisterObject, IExaminable, ICheckedInteractable<PositionalHandApply>,
			IRightClickable, ICheckedInteractable<ContextMenuApply>
	{

		[SerializeField]
		[Tooltip("The GameObject in this hierarchy that contains the SpriteClickRegion component defining the door of the grill.")]
		private SpriteClickRegion doorRegion = default;

		[SerializeField]
		[Tooltip("The GameObject in this hierarchy that contains the SpriteClickRegion component defining the grill's power button.")]
		private SpriteClickRegion powerRegion = default;

		private Grill grill;

		protected override void Awake()
		{
			base.Awake();
			grill = GetComponent<Grill>();
			OnParentChangeComplete.AddListener(ReparentContainedObjectsOnParentChangeComplete);
		}
		private void ReparentContainedObjectsOnParentChangeComplete()
		{
			if (grill != null)
			{
				// update the parent of each of the items in the closet
				grill.OnParentChangeComplete(NetworkedMatrixNetId);
			}
		}

		public string Examine(Vector3 worldPos = default)
		{
			return $"The grill is currently {grill.currentState.StateMsgForExamine}.";
		}
		public bool WillInteract(PositionalHandApply interaction, NetworkSide side)
		{
			return DefaultWillInteract.Default(interaction, side);
		}
		public bool WillInteract(ContextMenuApply interaction, NetworkSide side)
		{
			return DefaultWillInteract.Default(interaction, side);
		}
		public void ServerPerformInteraction(PositionalHandApply interaction)
		{
			if (doorRegion.Contains(interaction.WorldPositionTarget))
			{
				grill.RequestDoorInteraction(interaction.HandSlot);
			}
			else if (powerRegion.Contains(interaction.WorldPositionTarget))
			{
				grill.RequestToggleActive();
			}
		}
		public void ServerPerformInteraction(ContextMenuApply interaction)
		{
			switch (interaction.RequestedOption)
			{
				case "ToggleActive":
					grill.RequestToggleActive();
					break;
				case "ToggleDoor":
					grill.RequestDoorInteraction();
					break;
			}
		}


		public RightClickableResult GenerateRightClickOptions()
		{
			var result = RightClickableResult.Create();
			var activateInteraction = ContextMenuApply.ByLocalPlayer(gameObject, "ToggleActive");
			if (!WillInteract(activateInteraction, NetworkSide.Client)) return result;
			result.AddElement("Turn On or Off", () => ContextMenuOptionClicked(activateInteraction));

			var ejectInteraction = ContextMenuApply.ByLocalPlayer(gameObject, "ToggleDoor");
			result.AddElement("Open/Close", () => ContextMenuOptionClicked(ejectInteraction));

			return result;
		}

		private void ContextMenuOptionClicked(ContextMenuApply interaction)
		{
			InteractionUtils.RequestInteract(interaction, this);
		}
	}
}