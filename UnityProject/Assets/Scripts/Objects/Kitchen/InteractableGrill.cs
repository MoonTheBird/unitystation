using UnityEngine;
using System.Collections;

namespace Objects.Kitchen
{
	/// <summary>
	/// Allows Grill to be interacted with. Player can put food on the grill to cook it.
	/// The grill can be interacted with to check its current state.
	/// </summary>
	[RequireComponent(typeof(Grill))]
	public class InteractableGrill : MonoBehaviour, IExaminable, IRightClickable, ICheckedInteractable<ContextMenuApply>, ICheckedInteractable<HandApply>
	{
		private Grill grill;

		protected void Awake()
		{
			grill = GetComponent<Grill>();
		}

		public string Examine(Vector3 worldPos = default)
		{
			return $"The grill is currently {grill.currentState.StateMsgForExamine}.";
		}
		public bool WillInteract(HandApply interaction, NetworkSide side)
		{
			if (!DefaultWillInteract.Default(interaction, side)) return false;

			return Validations.HasUsedItemTrait(interaction, CommonTraits.Instance.Wrench) == false;
		}
		public bool WillInteract(ContextMenuApply interaction, NetworkSide side)
		{
			return DefaultWillInteract.Default(interaction, side);
		}
		public void ServerPerformInteraction(HandApply interaction)
		{
			if (interaction.HandObject == null)
			{
				grill.RequestToggleActive();
			}
			else if (Validations.HasUsedItemTrait(interaction, CommonTraits.Instance.Wrench))
			{

			}
			else
			{
				grill.RequestDoorInteraction(interaction.HandSlot);
			}
		}
		public void ServerPerformInteraction(ContextMenuApply interaction)
		{
			switch (interaction.RequestedOption)
			{
				case "ToggleActive":
					grill.RequestToggleActive();
					break;
			}
		}


		public RightClickableResult GenerateRightClickOptions()
		{
			var result = RightClickableResult.Create();
			var activateInteraction = ContextMenuApply.ByLocalPlayer(gameObject, "ToggleActive");
			if (!WillInteract(activateInteraction, NetworkSide.Client)) return result;
			result.AddElement("Turn On or Off", () => ContextMenuOptionClicked(activateInteraction));

			return result;
		}

		private void ContextMenuOptionClicked(ContextMenuApply interaction)
		{
			InteractionUtils.RequestInteract(interaction, this);
		}
	}
}