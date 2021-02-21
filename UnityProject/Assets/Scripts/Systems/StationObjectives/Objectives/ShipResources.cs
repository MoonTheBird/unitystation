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

		protected override bool CheckCompletion()
		{
			var finalReport = new StringBuilder(victoryDescription);
			finalReport.Replace("SHIPPEDVAL", $"{AmountSold}");
			victoryDescription = finalReport.ToString();
			Logger.Log($"amount sold {AmountSold}");
			Logger.Log($"amount {Amount}");
			if (AmountSold >= Amount)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
		private void OnEnable()
		{
			EventManager.AddHandler(EVENT.ItemSold, CheckItemSold);
		}
		private void OnDisable()
		{
			EventManager.RemoveHandler(EVENT.ItemSold, CheckItemSold);
		}
		protected override void Setup()
		{
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

			Amount = Random.Range(itemEntry.Value - itemEntry.Value / 3, itemEntry.Value + itemEntry.Value / 3);
			if (Amount <= 0)
			{
				Amount = 1;
			}
			var report = new StringBuilder();
			report.AppendFormat(ReportTemplates.DeliveryStationObjective, Amount);
			report.Replace("MATERIAL", ItemName);
			description = report.ToString();
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
				if (string.IsNullOrEmpty(attributes.ExportName))
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
					Logger.Log(AmountSold.ToString());
				}
				else
				{
					AmountSold++;
					Logger.Log(AmountSold.ToString() + " and not stackable");
				}
			}
		}
	}
}