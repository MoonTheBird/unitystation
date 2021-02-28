using System;
using System.Collections;
using UnityEngine;
using Mirror;
using AddressableReferences;

namespace Objects.Kitchen
{
	/// <summary>
	/// A machine into which players can insert items for cooking. If the item has the Cookable component,
	/// the item will be cooked once enough time has lapsed as determined in that component.
	/// Otherwise, any food item that doesn't have the cookable component will be cooked using
	/// the legacy way, of converting to cooked when the grill's timer finishes.
	/// </summary>
	public class Grill : NetworkBehaviour
	{
		[SerializeField]
		private AddressableAudioSource doorSFX = null; // SFX the grill should make when opening/closing.

		[SerializeField] private AddressableAudioSource startSFX = null;

		[SerializeField]
		[Tooltip("The looped audio source to play while the grill is open and on.")]
		private AudioSource AmbientAudio = default;

		[SerializeField]
		[Tooltip("Child GameObject that is responsible for the glow of the grill.")]
		private GameObject GrillGlow = default;

		private RegisterTile registerTile;
		private SpriteHandler spriteHandler;
		private ItemStorage storage;
		[NonSerialized]
		public ItemSlot storageSlot;
		private Cookable storedCookable;

		[SyncVar(hook = nameof(OnSyncPlayAudioLoop))]
		private bool playAudioLoop;

		public bool IsOperating => currentState is GrillClosedOn || currentState is GrillOpenOn;
		public bool HasContents => storageSlot.IsOccupied;
		public Vector3Int WorldPosition => registerTile.WorldPosition;

		public GrillState currentState;


		#region Lifecycle

		private void Awake()
		{
			EnsureInit();
		}

		private void EnsureInit()
		{
			registerTile = GetComponent<RegisterTile>();
			spriteHandler = GetComponentInChildren<SpriteHandler>();
			storage = GetComponent<ItemStorage>();

			SetState(new GrillClosedIdle(this));
		}

		private void Start()
		{
			storageSlot = storage.GetIndexedItemSlot(0);
		}

		private void OnDisable()
		{
			UpdateManager.Remove(CallbackType.UPDATE, UpdateMe);
		}

		#endregion Lifecycle

		/// <summary>
		/// Reduce the grill's timer, add that time to food cooking.
		/// </summary>
		private void UpdateMe()
		{
			if (!IsOperating) return;
			CheckCooked();
		}

		/// <summary>
		/// Return the size of the storage.
		/// </summary>
		public int StorageSize()
		{
			return storage.StorageSize();
		}

		private void SetState(GrillState newState)
		{
			currentState = newState;
		}

		#region Requests

		/// <summary>
		/// Starts or stops the grill, depending on the grill's current state.
		/// </summary>
		public void RequestToggleActive()
		{
			currentState.ToggleActive();
		}

		/// <summary>
		/// Opens or closes the grill's door, depending on the grill's current state.
		/// If closing, will attempt to add the currently held item to the grill.
		/// </summary>
		/// <param name="fromSlot">The slot with which to check if an item is held, for inserting an item when closing.</param>
		public void RequestDoorInteraction(ItemSlot fromSlot = null)
		{
			currentState.DoorInteraction(fromSlot);
		}

		#endregion Requests

		private void TransferToGrill(ItemSlot fromSlot)
		{
			if (fromSlot == null || fromSlot.IsEmpty) return;
			if (!Inventory.ServerTransfer(fromSlot, storage.GetNextFreeIndexedSlot())) return;
			if (storageSlot.ItemObject.TryGetComponent(out Cookable cookable))
			{
				storedCookable = cookable;
			}
		}

		private void OpenGrillAndEjectContents()
		{
			SoundManager.PlayNetworkedAtPos(doorSFX, WorldPosition, sourceObj: gameObject);

			Vector2 spritePosWorld = spriteHandler.transform.position;
			Vector2 grillInteriorCenterAbs = spritePosWorld + new Vector2(-0.075f, -0.075f);
			Vector2 grillInteriorCenterRel = grillInteriorCenterAbs - WorldPosition.To2Int();

			// Looks nicer if we drop the item in the middle of the sprite's representation of the grill's interior.

			foreach (var slot in storage.GetItemSlots())
			{
				if (slot.IsOccupied == true)
				{
					Inventory.ServerDrop(slot, grillInteriorCenterRel);
				}
			}

		}

		private void GrillOn()
		{
			UpdateManager.Add(CallbackType.UPDATE, UpdateMe);
			SoundManager.PlayNetworkedAtPos(startSFX, WorldPosition, sourceObj: gameObject);
			playAudioLoop = true;
		}

		private void GrillOff()
		{
			UpdateManager.Remove(CallbackType.UPDATE, UpdateMe);
			playAudioLoop = false;
		}

		private void CheckCooked()
		{
			foreach (var slot in storage.GetItemSlots())
			{
				if (slot.IsOccupied == true)
				{

					if (slot.ItemObject.TryGetComponent(out Cookable slotCooked))
					{

						// True if the item's total cooking time exceeds the item's minimum cooking time.
						if (slotCooked.AddCookingTime(Time.deltaTime) == true)
						{
							// Swap item for its cooked version, if applicable.
							if (slotCooked.CookedProduct == null) return;

							Despawn.ServerSingle(slotCooked.gameObject);
							GameObject cookedItem = Spawn.ServerPrefab(slotCooked.CookedProduct).GameObject;
							Inventory.ServerAdd(cookedItem, slot);
						}

					}

				}

			}

		}

		private void OnSyncPlayAudioLoop(bool oldState, bool newState)
		{
			if (newState)
			{
				StartCoroutine(DelayGrillRunningSFX());
			}
			else
			{
				AmbientAudio.Stop();
			}
		}

		// We delay the running SFX so the starting SFX has time to play.
		private IEnumerator DelayGrillRunningSFX()
		{
			yield return WaitFor.Seconds(0.25f);

			// Check to make sure the state hasn't changed in the meantime.
			if (playAudioLoop) AmbientAudio.Play();
		}

		#region GrillStates

		public abstract class GrillState
		{
			public string StateMsgForExamine = "an unknown state";
			protected Grill grill;

			public abstract void ToggleActive();
			public abstract void DoorInteraction(ItemSlot fromSlot);
		}

		private class GrillClosedIdle : GrillState
		{
			public GrillClosedIdle(Grill grill)
			{
				this.grill = grill;
				StateMsgForExamine = "closed and off";
				grill.spriteHandler.ChangeSprite(0);
				grill.GrillGlow.SetActive(false);
				grill.GrillOff();
			}

			public override void ToggleActive()
			{
				grill.GrillOn();
				grill.SetState(new GrillClosedOn(grill));
			}

			public override void DoorInteraction(ItemSlot fromSlot)
			{
				grill.OpenGrillAndEjectContents();
				grill.SetState(new GrillOpenIdle(grill));
			}
		}

		private class GrillOpenIdle : GrillState
		{
			public GrillOpenIdle(Grill grill)
			{
				this.grill = grill;
				StateMsgForExamine = "open and off";
				grill.spriteHandler.ChangeSprite(1);
				grill.GrillGlow.SetActive(false);
				grill.GrillOff();
			}

			public override void ToggleActive()
			{
				grill.GrillOn();
				grill.SetState(new GrillOpenOn(grill));
			}

			public override void DoorInteraction(ItemSlot fromSlot)
			{
				// Close if nothing's in hand.
				if (fromSlot.Item == null)
				{
					grill.SetState(new GrillClosedIdle(grill));
					return;
				}

				grill.TransferToGrill(fromSlot);
			}
		}

		private class GrillOpenOn : GrillState
		{
			public GrillOpenOn(Grill grill)
			{
				this.grill = grill;
				StateMsgForExamine = "running and open";
				grill.GrillGlow.SetActive(true);
				grill.spriteHandler.ChangeSprite(2);
			}

			public override void ToggleActive()
			{
				grill.SetState(new GrillOpenIdle(grill));
			}

			public override void DoorInteraction(ItemSlot fromSlot)
			{
				// Close if nothing's in hand.
				if (fromSlot.Item == null)
				{
					grill.SetState(new GrillClosedOn(grill));
					return;
				}

				grill.TransferToGrill(fromSlot);
			}
		}

		private class GrillClosedOn : GrillState
		{
			public GrillClosedOn(Grill grill)
			{
				this.grill = grill;
				StateMsgForExamine = "running and closed";
				grill.GrillGlow.SetActive(false);
				grill.spriteHandler.ChangeSprite(2);
			}

			public override void ToggleActive()
			{
				grill.SetState(new GrillClosedIdle(grill));
			}

			public override void DoorInteraction(ItemSlot fromSlot)
			{
				grill.OpenGrillAndEjectContents();
				grill.SetState(new GrillOpenOn(grill));
			}
		}

		#endregion GrillStates
	}
}
