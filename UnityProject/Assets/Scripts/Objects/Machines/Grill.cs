using System;
using System.Collections;
using UnityEngine;
using Mirror;
using AddressableReferences;
using System.Collections.Generic;
using SoundMessages;
using System.Linq;
using Items;

namespace Objects.Kitchen
{
	/// <summary>
	/// A machine into which players can insert meat items for grilling.
	/// </summary>
	public class Grill : NetworkBehaviour
	{[SerializeField] private AddressableAudioSource startSFX = null;

		[SerializeField]
		[Tooltip("The looped audio source to play while the grill is open and on.")]
		private AudioSource AmbientAudio = default;

		[SerializeField]
		[Tooltip("Child GameObject that is responsible for the glow of the grill.")]
		private GameObject GrillGlow = default;

		private RegisterTile registerTile;
		private SpriteHandler spriteHandler;

		[SyncVar(hook = nameof(OnSyncPlayAudioLoop))]
		private bool playAudioLoop;
		public Vector3Int WorldPosition => registerTile.WorldPosition;
		private PushPull pushPull;
		private Matrix Matrix => registerTile.Matrix;

		public GrillState currentState;

		public bool IsOperating => currentState is GrillOpenOn;


		#region Lifecycle

		private void Awake()
		{
			EnsureInit();
		}

		private void EnsureInit()
		{
			registerTile = GetComponent<RegisterTile>();
			spriteHandler = GetComponentInChildren<SpriteHandler>();

			SetState(new GrillOpenIdle(this));
			if (registerTile != null) return;
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
			if (!IsOperating)
			{
				return;
			}
			CheckCooked();
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
			var itemsOnGrill = Matrix.Get<ObjectBehaviour>(registerTile.LocalPositionServer, ObjectType.Item, true)
				.Where(ob => ob != null && ob.gameObject != gameObject)
				.Where(ob =>
				{
					return true;
				});
			foreach (var onGrill in itemsOnGrill)
			{
				if (onGrill.gameObject.TryGetComponent(out Cookable slotCooked) && onGrill.gameObject.GetComponent<Edible>() != null && slotCooked.CookedProduct != null)
				{
					// True if the item's total cooking time exceeds the item's minimum cooking time.
					if (slotCooked.AddCookingTime(Time.deltaTime) == true)
					{
						// Swap item for its cooked version, if applicable.
						if (slotCooked.CookedProduct == null) return;

						Despawn.ServerSingle(slotCooked.gameObject);
						var cookedItem = Spawn.ServerPrefab(slotCooked.CookedProduct, registerTile.WorldPositionServer);
						cookedItem.GameObject.Item().ServerSetArticleName("grilled " + cookedItem.GameObject.Item().InitialName);
						cookedItem.GameObject.Item().ServerSetArticleDescription(cookedItem.GameObject.Item().InitialDescription + "\nIt's been finely grilled by an expert.");
						continue;
					}
				}
				else if (onGrill.gameObject.TryGetComponent(out Edible edible) || (edible && slotCooked.CookedProduct == null))
				{
					var item = edible.gameObject.Item();
					//prevents the grill from regrilling already grilled things
					if (item.ArticleName.StartsWith("grilled"))
					{
						continue;
					}
					Cookable cookable;
					// temporarily gives the item the "cookable" component, allowing it to be properly cooked
					if (item.gameObject.GetComponent<Cookable>() != null)
					{
						cookable = item.gameObject.GetComponent<Cookable>();
					}
					else
					{
						cookable = item.gameObject.AddComponent<Cookable>();
					}

					if (cookable.AddCookingTime(Time.deltaTime) == true)
					{
						Destroy(cookable);
						if (item.InitialName == "cheese wheel" || item.InitialName == "cheese wedge")
						{
							item.ServerSetArticleName("grilled cheese");
							item.ServerSetArticleDescription("The name mocks you, for it lacks bread. Still looks delicious though.");
							edible.NutritionLevel = edible.NutritionLevel * 2;
							if (item.gameObject.GetComponentInChildren<SpriteHandler>() == null)
							{
								continue;
							}
							item.gameObject.GetComponentInChildren<SpriteHandler>().SetColor(new Color(0.3F, 0.2F, 0, 1));
							item.gameObject.GetComponent<Edible>().eatSound = new AddressableAudioSource("Assets/Prefabs/Effects/EatFood.prefab");
							continue;
						}
						edible.NutritionLevel = (int)Math.Round(edible.NutritionLevel * 1.5);
						item.ServerSetArticleName("grilled " + item.InitialName);
						item.ServerSetArticleDescription(item.InitialDescription + "\nIt's not something you'd normally eat grilled, but it does look tasty...");
						edible.leavings = null;
						if (item.gameObject.GetComponentInChildren<SpriteHandler>() == null)
						{
							continue;
						}
						item.gameObject.GetComponentInChildren<SpriteHandler>().SetColor(new Color(0.3F, 0.2F, 0, 1));
						item.gameObject.GetComponent<Edible>().eatSound = new AddressableAudioSource("Assets/Prefabs/Effects/EatFood.prefab");
						continue;
					}
				}
				else
				{
					var item = onGrill.gameObject.Item();
					Cookable cookable;
					// temporarily gives the item the "cookable" component, allowing it to be properly cooked
					if (item.gameObject.GetComponent<Cookable>() != null)
					{
						cookable = item.gameObject.GetComponent<Cookable>();
					}
					else
					{
						cookable = item.gameObject.AddComponent<Cookable>();
					}
					if (cookable.AddCookingTime(Time.deltaTime) == true)
					{
						Destroy(cookable);
						item.ServerSetArticleName("grilled " + item.InitialName);
						item.ServerSetArticleDescription(item.InitialDescription + "\nIt's been grilled, for some reason. Probably tastes nasty.");
						if (item.gameObject.GetComponentInChildren<SpriteHandler>() == null)
						{
							continue;
						}
						item.gameObject.GetComponentInChildren<SpriteHandler>().SetColor(new Color(0.3F, 0.2F, 0, 1));
						item.gameObject.AddComponent<Edible>().NutritionLevel = 25;
						item.gameObject.GetComponent<Edible>().NutritionLevel = 25;
						//makes food make the cronch sound
						item.gameObject.GetComponent<Edible>().eatSound = new AddressableAudioSource("Assets/Prefabs/Effects/EatFood.prefab");
						continue;
					}
				}
			}
		}

		private void CookEdible(ItemAttributesV2 item)
		{
			var edible = item.GetComponent<Edible>();

			//prevents the grill from regrilling already grilled things
			if (item.InitialName.StartsWith("grilled"))
			{
				return;
			}

			if (item.InitialName == "cheese wheel" || item.InitialName == "cheese wedge")
			{
				item.ServerSetArticleName("grilled cheese");
				item.ServerSetArticleDescription("The name mocks you, for it lacks bread. Still looks delicious though.");
				edible.NutritionLevel = edible.NutritionLevel * 2;
				if (item.gameObject.GetComponentInChildren<SpriteHandler>() == null)
				{
					return;
				}
				item.gameObject.GetComponentInChildren<SpriteHandler>().SetColor(new Color(0.3F, 0.2F, 0, 1));
				return;
			}
			edible.NutritionLevel = (int)Math.Round(edible.NutritionLevel * 1.5);
			item.ServerSetArticleName("grilled " + item.InitialName);
			item.ServerSetArticleDescription(item.InitialDescription + "\nIt's not something you'd normally eat grilled, but it does look tasty...");
			edible.leavings = null;
			if (item.gameObject.GetComponentInChildren<SpriteHandler>() == null)
			{
				return;
			}
			item.gameObject.GetComponentInChildren<SpriteHandler>().SetColor(new Color(0.3F, 0.2F, 0, 1));
			return;
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
		public override void OnStartClient()
		{
			EnsureInit();
		}

		#region GrillStates

		public abstract class GrillState
		{
			public string StateMsgForExamine = "an unknown state";
			protected Grill grill;

			public abstract void ToggleActive();
			public abstract void DoorInteraction(ItemSlot fromSlot);
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
				Vector3 targetPosition = grill.registerTile.WorldPositionServer;
				Vector3 performerPosition = fromSlot.Player.WorldPositionServer;
				Inventory.ServerDrop(fromSlot, targetPosition - performerPosition);
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
				Vector3 targetPosition = grill.registerTile.WorldPositionServer;
				Vector3 performerPosition = fromSlot.Player.WorldPositionServer;
				Inventory.ServerDrop(fromSlot, targetPosition - performerPosition);
			}
		}

		#endregion GrillStates
	}
}
