using UnityEngine;
using System.Collections;

namespace Objects.Kitchen
{
	/// <summary>
	/// Allows Microwave to be interacted with. Player can put food in the microwave to cook it.
	/// The microwave can be interacted with to, for example, check the remaining time.
	/// </summary>
	[RequireComponent(typeof(Grill))]
	public class InteractableGrill : RegisterObject, IExaminable
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
			grill.OnClosedChanged.AddListener(OnClosedChanged);
			OnClosedChanged(grill.IsClosed);
		}
		private void ReparentContainedObjectsOnParentChangeComplete()
		{
			if (grill != null)
			{
				// update the parent of each of the items in the closet
				grill.OnParentChangeComplete(NetworkedMatrixNetId);
			}
		}
		private void OnClosedChanged(bool isClosed)
		{
			//become passable to bullets and people when open
			Passable = !isClosed;
			//switching to item layer if open so bullets pass through it
			if (Passable)
			{
				gameObject.layer = LayerMask.NameToLayer("Items");
			}
			else
			{
				gameObject.layer = LayerMask.NameToLayer("Machines");
			}
		}

		public string Examine(Vector3 worldPos = default)
		{
			return $"The grill is currently {grill.currentState.StateMsgForExamine}.";
		}
	}
}