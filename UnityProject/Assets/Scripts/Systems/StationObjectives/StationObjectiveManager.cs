using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Linq;
using DiscordWebhook;
using DatabaseAPI;
using Messages.Server.LocalGuiMessages;
using Strings;
using Managers;
using Random = UnityEngine.Random;

namespace StationObjectives
{
	public class StationObjectiveManager : MonoBehaviour
	{
		public GameManager gameManager;

		[SerializeField]
		[Tooltip("Stores all station objective data.")]
		private StationObjectiveData stationObjectiveData = null;

		public static StationObjectiveManager Instance;

		[NonSerialized] public StationObjective activeObjective;

		private List<Vector2> asteroidLocations = new List<Vector2>();

		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
			}
			else
			{
				Destroy(gameObject);
			}
		}
		void OnEnable()
		{
			EventManager.AddHandler(EVENT.RoundEnded, ResetObjectives);
		}
		void OnDisable()
		{
			EventManager.RemoveHandler(EVENT.RoundEnded, ResetObjectives);
		}

		//public ;

		public void RemindStation()
		{
			GameManager.Instance.CentComm.MakeCommandReport(StationObjectiveReport(),CentComm.UpdateSound.Notice);
		}
		private string StationObjectiveReport()
		{
			var report = new StringBuilder(activeObjective.Description);
			report.Replace("STATIONNAME", MatrixManager.MainStationMatrix.GameObject.scene.name);

			foreach (var location in asteroidLocations)
			{
				report.AppendFormat(" <size=24>{0}</size> ", Vector2Int.RoundToInt(location));
			}

			return report.ToString();
		}

		public void ServerChooseObjective()
		{
			foreach (var body in gameManager.SpaceBodies)
			{
				if (body.TryGetComponent<Asteroid>(out _))
				{
					asteroidLocations.Add(body.ServerState.Position);
				}
			}

			int randomPosCount = Random.Range(1, 5);
			for (int i = 0; i <= randomPosCount; i++)
			{
				asteroidLocations.Add(gameManager.RandomPositionInSolarSystem());
			}
			asteroidLocations = asteroidLocations.OrderBy(x => Random.value).ToList();

			activeObjective = stationObjectiveData.GetRandomObjective();
			GameManager.Instance.CentComm.MakeQuietCommandReport(StationObjectiveReport());
		}

		public void ShowStationStatusReport()
		{
			StringBuilder statusSB = new StringBuilder($"<color=white><size=60><b>End of Round Report</b></size></color>\n\n", 200);

			var message = $"End of Round Report on {ServerData.ServerConfig.ServerName}\n";

			statusSB.AppendLine(GetObjectiveStatus());
			message += $"\n{GetObjectiveStatusPoor()}";

			DiscordWebhookMessage.Instance.AddWebHookMessageToQueue(DiscordWebhookURLs.DiscordWebhookAnnouncementURL, message, "");

			// Send the message
			Chat.AddGameWideSystemMsgToChat(statusSB.ToString());
		}

		public string GetObjectiveStatus()
		{
			StringBuilder objSB = new StringBuilder($"<color=blue>Objective of <b>{MatrixManager.MainStationMatrix.GameObject.scene.name}</b>:</color>\n", 200);
			objSB.Append($"{activeObjective.VictoryDescription}\n");
			objSB.AppendLine(activeObjective.IsComplete() ? "<color=green><b>Completed</b></color>" : "<color=red><b>Failed</b></color>");
			return objSB.ToString();
		}

		public string GetObjectiveStatusPoor()
		{
			var message = $"Objective of {MatrixManager.MainStationMatrix.GameObject.scene.name}:\n";
			message += $"{activeObjective.VictoryDescription}";
			message += activeObjective.IsComplete() ? "Completed\n" : "Failed\n";
			return message;
		}

		public void ResetObjectives()
		{
			activeObjective.IsComplete();
			activeObjective = null;
		}
	}
}