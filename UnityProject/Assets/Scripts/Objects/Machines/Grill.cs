using System;
using System.Collections;
using UnityEngine;
using Mirror;
using AddressableReferences;
using System.Collections.Generic;
using SoundMessages;
using System.Linq;

namespace Objects.Kitchen
{
	/// <summary>
	/// A machine into which players can insert items for cooking. If the item has the Cookable component,
	/// the item will be cooked once enough time has lapsed as determined in that component.
	/// Otherwise, any food item that doesn't have the cookable component will be cooked using
	/// the legacy way, of converting to cooked when the grill's timer finishes.
	/// </summary>
	public class Grill : NetworkBehaviour, ICheckedInteractable<HandApply>, IRightClickable,
	IServerLifecycle
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
		public bool IsClosed => currentState is GrillClosedIdle || currentState is GrillClosedOn;
		[NonSerialized]
		public readonly BoolEvent OnClosedChanged = new BoolEvent();
		public List<ObjectBehaviour> ServerHeldItems => serverHeldItems;
		private List<ObjectBehaviour> serverHeldItems = new List<ObjectBehaviour>();
		public Vector3Int WorldPosition => registerTile.WorldPosition;
		private PushPull pushPull;
		private Matrix Matrix => registerTile.Matrix;

		public GrillState currentState;
		[SyncVar(hook = nameof(SyncStatus))]
		private GrillStatus statusSync;


		#region Lifecycle

		private void Awake()
		{
			EnsureInit();
			GetComponent<Integrity>().OnWillDestroyServer.AddListener(OnWillDestroyServer);
		}

		private void EnsureInit()
		{
			registerTile = GetComponent<RegisterTile>();
			spriteHandler = GetComponentInChildren<SpriteHandler>();
			storage = GetComponent<ItemStorage>();
			if (registerTile != null) return;

			registerTile = GetComponent<RegisterCloset>();
			pushPull = GetComponent<PushPull>();

			SetState(new GrillClosedIdle(this));
		}
		private PushPull PushPull
		{
			get
			{
				if (pushPull == null)
				{
					Logger.LogErrorFormat("Grill {0} has no PushPull component! All contained items will appear at HiddenPos!", Category.Transform, gameObject.ExpensiveName());
				}
				return pushPull;
			}
		}
		public void OnParentChangeComplete(uint parentNetId)
		{
			foreach (ObjectBehaviour objectBehaviour in serverHeldItems)
			{
				objectBehaviour.registerTile.ServerSetNetworkedMatrixNetID(parentNetId);
			}
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
		/// grill
		/// </summary>
		private void UpdateMe()
		{
			if (!IsOperating) return;
			CheckCooked();
		}

		private void SetState(GrillState newState)
		{
			currentState = newState;
		}
		private void SyncStatus(GrillStatus oldValue, GrillStatus value)
		{
			EnsureInit();
			statusSync = value;
			if (value == GrillStatus.Open)
			{
				OnClosedChanged.Invoke(false);
			}
			else
			{
				OnClosedChanged.Invoke(true);
			}
			UpdateSpritesOnStatusChange();
		}
		protected virtual void UpdateSpritesOnStatusChange()
		{
			if (statusSync == GrillStatus.Open)
			{
				spriteHandler.ChangeSprite((int)GrillStatus.Open);
			}
			else
			{
				spriteHandler.ChangeSprite((int)GrillStatus.Closed);
			}
		}

		#region Requests

		/// <summary>
		/// Starts or stops the grill, depending on the grill's current state.
		/// </summary>
		public void RequestToggleActive()
		{
			currentState.ToggleActive();
		}
		public virtual void OnSpawnServer(SpawnInfo info)
		{
			//always spawn open
			SyncStatus(statusSync, GrillStatus.Open);
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
		[Server]
		private void ServerAddInternalItemInternal(ObjectBehaviour toAdd, bool force = false)
		{
			if (toAdd == null || serverHeldItems.Contains(toAdd) || (!IsClosed && !force)) return;
			serverHeldItems.Add(toAdd);
			toAdd.parentContainer = pushPull;
			toAdd.VisibleState = false;
		}

		#endregion Requests

		public void OnDespawnServer(DespawnInfo info)
		{
			//make sure we despawn what we are holding
			foreach (var heldItem in serverHeldItems)
			{
				Despawn.ServerSingle(heldItem.gameObject);
			}
			serverHeldItems.Clear();
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
		[Server]
		public void ServerAddInternalItem(ObjectBehaviour toAdd)
		{
			ServerAddInternalItemInternal(toAdd);
		}
		private void OnWillDestroyServer(DestructionInfo arg0)
		{
			// failsafe: drop all contents immediately
			ServerHandleContentsOnStatusChange(false);
			SyncStatus(statusSync, GrillStatus.Open);
		}
		[Server]
		public void ServerToggleClosed(bool? nowClosed = null)
		{
			AudioSourceParameters audioSourceParameters = new AudioSourceParameters(pitch: 1f);

			SoundManager.PlayNetworkedAtPos(doorSFX, registerTile.WorldPositionServer, audioSourceParameters, sourceObj: gameObject);

			ServerSetIsClosed(nowClosed.GetValueOrDefault(!IsClosed));
		}
		[Server]
		private void ServerSetIsClosed(bool nowClosed)
		{
			ServerHandleContentsOnStatusChange(nowClosed);
			if (nowClosed)
			{
				statusSync = GrillStatus.Closed;
			}
			else
			{
				statusSync = GrillStatus.Open;
			}
		}
		[Server]
		protected virtual void ServerHandleContentsOnStatusChange(bool willClose)
		{
			if (willClose)
			{
				CloseItemHandling();
			}
			else
			{
				OpenItemHandling();
			}
		}
		private void CloseItemHandling()
		{
			var itemsOnCloset = Matrix.Get<ObjectBehaviour>(registerTile.LocalPositionServer, ObjectType.Item, true)
				.Where(ob => ob != null && ob.gameObject != gameObject)
				.Where(ob =>
				{
					return true;
				});

			foreach (var objectBehaviour in itemsOnCloset)
			{
				ServerAddInternalItemInternal(objectBehaviour, true);
			}
		}
		private void OpenItemHandling()
		{
			foreach (ObjectBehaviour item in serverHeldItems)
			{
				if (!item) continue;

				CustomNetTransform netTransform = item.GetComponent<CustomNetTransform>();
				//avoids blinking of premapped items when opening first time in another place:
				Vector3Int pos = registerTile.WorldPositionServer;
				netTransform.AppearAtPosition(pos);
				if (PushPull && PushPull.Pushable.IsMovingServer)
				{
					netTransform.InertiaDrop(pos, PushPull.Pushable.SpeedServer,
						PushPull.InheritedImpulse.To2Int());
				}
				else
				{
					item.VisibleState = true; //should act identical to line above
				}
				item.parentContainer = null;
			}
			serverHeldItems.Clear();
		}
		public override void OnStartClient()
		{
			EnsureInit();

			SyncStatus(statusSync, statusSync);
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

				grill.ServerAddInternalItem(fromSlot.ItemObject.GetComponent<ObjectBehaviour>());
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

				grill.ServerAddInternalItem(fromSlot.ItemObject.GetComponent<ObjectBehaviour>());
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
				grill.SetState(new GrillOpenOn(grill));
			}
		}

		#endregion GrillStates
		#region Interaction-ContextMenu
		public RightClickableResult GenerateRightClickOptions()
		{
			var result = RightClickableResult.Create();

			if (WillInteract(HandApply.ByLocalPlayer(gameObject), NetworkSide.Client))
			{
				var optionName = IsClosed ? "Open" : "Close";
				result.AddElement("Toggle Door", RightClickInteract, nameOverride: optionName);
				optionName = IsOperating ? "On" : "Off";
				result.AddElement("Toggle Power", RightClickInteract, nameOverride: optionName);
			}

			return result;
		}
		private void RightClickInteract()
		{
			InteractionUtils.RequestInteract(HandApply.ByLocalPlayer(gameObject), this);
		}
		#endregion Interaction-ContextMenu
		public bool WillInteract(HandApply interaction, NetworkSide side)
		{
			if (!DefaultWillInteract.Default(interaction, side)) return false;

			//only allow interactions targeting this closet
			if (interaction.TargetObject != gameObject) return false;

			return true;
		}
		public void ServerPerformInteraction(HandApply interaction)
		{
			if (interaction.HandObject != null)
			{
				if (!IsClosed)
				{
					Vector3 targetPosition = interaction.TargetObject.WorldPosServer().RoundToInt();
					Vector3 performerPosition = interaction.Performer.WorldPosServer();
					Inventory.ServerDrop(interaction.HandSlot, targetPosition - performerPosition);
				}
			}
			else if (interaction.HandObject == null)
			{
				ServerToggleClosed();
			}
		}
	}
	public enum GrillStatus
	{
		Closed,
		Open
	}
}
