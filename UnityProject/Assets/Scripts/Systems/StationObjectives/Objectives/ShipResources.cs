using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using Strings;
using Systems.Cargo;

namespace StationObjectives
{
	/// <summary>
	/// An objective to ship items to nanotrasen
	/// </summary>
	[CreateAssetMenu(menuName = "ScriptableObjects/StationObjectives/ShipResources")]
	public class ShipResources : StationObjective
	{
		/// <summary>
		/// The pool of possible resources to ship
		/// </summary>
		[SerializeField]
		private ItemDictionary ItemPool = null;

		/// <summary>
		/// The resource to ship
		/// </summary>
		private string ItemName;

		/// <summary>
		/// The number of items needed to ship to complete the objective
		/// </summary>
		private int Amount;

		/// <summary>
		/// Current amount of the item sold.
		/// </summary>
		private int AmountSold = 0;

		private List<Vector2> asteroidLocations = new List<Vector2>();

		protected override bool CheckCompletion()
		{
			var finalReport = new StringBuilder(victoryDescription);
			finalReport.Replace("SHIPPEDVAL", $"{AmountSold}");
			victoryDescription = finalReport.ToString();
			Logger.Log($"amount sold {AmountSold}");
			Logger.Log($"amount {Amount}");
			return Complete;
		}
		private void OnEnable()
		{
			EventManager.AddHandler(EVENT.ItemSold, CheckItemSold);
		}
		private void OnDisable()
		{
			EventManager.RemoveHandler(EVENT.ItemSold, CheckItemSold);
		}
		public class ResourceTracker
		{
			public int RequiredAmount;
			public Dictionary<string, int> CurrentAmount;

			public ResourceTracker(int requiredAmount, Dictionary<string, int> currentAmounts)
			{
				RequiredAmount = requiredAmount;
				CurrentAmount = currentAmounts;
			}

			public void AddToTracker(string resource)
			{
				if (CurrentAmount.ContainsKey(resource) == false)
				{
					Logger.LogWarning($"ResourceTracker tried to add to non-existent resource {resource}!");
					return;
				}

				CurrentAmount[resource] = CurrentAmount[resource]++;
			}
		}
		protected override void Setup()
		{
			foreach (var body in GameManager.Instance.SpaceBodies)
			{
				if (body.TryGetComponent<Asteroid>(out _))
				{
					asteroidLocations.Add(body.ServerState.Position);
				}
			}

			int randomPosCount = Random.Range(1, 5);
			for (int i = 0; i <= randomPosCount; i++)
			{
				asteroidLocations.Add(GameManager.Instance.RandomPositionInSolarSystem());
			}
			asteroidLocations = asteroidLocations.OrderBy(x => Random.value).ToList();

			var possibleItems = ItemPool.ToList();

			if (possibleItems.Count == 0)
			{
				Logger.LogWarning("Unable to find any items to ship. This shouldn't happen!");
			}

			var itemEntry = possibleItems.PickRandom();
			if (itemEntry.Key == null)
			{
				Logger.LogError($"Objective failed because the item type chosen is somehow destroyed." +
								" Definitely a programming bug. ", Category.Round);
				return;
			}

			ItemName = itemEntry.Key.Item().InitialName;

			if (string.IsNullOrEmpty(ItemName))
			{
				Logger.LogError($"Objective failed because the InitialName has not been" +
								$" set on this objects ItemAttributes. " +
								$"Item: {itemEntry.Key.Item().gameObject.name}", Category.Round);
				return;
			}
			// randomizes the amount needed to complete the shipment, with a minimum of 2/3rds of the default value and a maximum of 1 and 1/3rd of the default
			Amount = Random.Range(itemEntry.Value - itemEntry.Value / 3, itemEntry.Value + itemEntry.Value / 3);
			if (Amount <= 0)
			{
				Amount = 1;
			}
			var toTrack = new Dictionary<string, int>
			{
				{ItemName, 0},
			};
			ResourceTracker tracker = new ResourceTracker(Amount, toTrack);
			var report = new StringBuilder();
			report.AppendFormat(ReportTemplates.DeliveryStationObjective, Amount);
			report.Replace("MATERIAL", ItemName);
			foreach (var location in asteroidLocations)
			{
				report.AppendFormat(" <size=24>{0}</size> ", Vector2Int.RoundToInt(location));
			}
			description = report.ToString();
			Complete = false;
			AmountSold = 0;
			var vicReport = new StringBuilder();
			vicReport.AppendFormat(ReportTemplates.DeliveryStationObjectiveEnd, Amount);
			vicReport.Replace("MATERIAL", ItemName);
			victoryDescription = vicReport.ToString();
		}
		private void CheckItemSold()
		{
			var item = CargoManager.Instance.GetExportedItem();
			var attributes = item.gameObject.GetComponent<Attributes>();
			string exportName = System.String.Empty;
			if (attributes)
			{
				if (string.IsNullOrEmpty(attributes.InitialName))
				{
					exportName = attributes.ArticleName;
				}
				else
				{
					exportName = attributes.InitialName;
				}
			}
			else
			{
				exportName = item.gameObject.ExpensiveName();
			}
			if (exportName == ItemName)
			{
				var stackable = item.gameObject.GetComponent<Stackable>();
				if (stackable)
				{
					AmountSold += stackable.Amount;
				}
				else
				{
					AmountSold++;
				}
			}
			if (AmountSold >= Amount)
			{
				Complete = true;
			}
		}
	}
}