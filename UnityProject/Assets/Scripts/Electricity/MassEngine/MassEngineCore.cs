using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Radiation;

namespace MassEngine
{
	public class MassEngineCore : MonoBehaviour, ICheckedInteractable<HandApply>
	{
		public float editorPresentTritium;
		public float editorPresentHydrogen;
		public float editorPresentHelium;
		[SerializeField]
		private float energyEditorFlux = 760000; // set in editor to find a nice value to base power generation off of! default is plasma gen power
		[SerializeField]
		private float stage1Upgrade;
		[SerializeField]
		private float stage2Upgrade;
		[SerializeField]
		private float stage3Upgrade;
		[SerializeField]
		private float stage35Upgrade;
		[SerializeField]
		private float stage4Upgrade;
		[SerializeField]
		private float stage45Upgrade;
		[SerializeField]
		private float stage5Upgrade;

		[HideInInspector]
		public decimal OutputEnergy;
		[HideInInspector]
		public int energyCollectorNum;
		public RadiationProducer radiationProducer;
		private RegisterObject registerObject;


		[HideInInspector]
		[Range(0, 5)]
		public decimal massStage;

		private void OnEnable()
		{
			if (CustomNetworkManager.Instance._isServer == false) return;

			UpdateManager.Add(CycleUpdate, 1);
		}
		private void OnDisable()
		{
			if (CustomNetworkManager.Instance._isServer == false) return;

			UpdateManager.Remove(CallbackType.PERIODIC_UPDATE, CycleUpdate);
		}

		private void Awake()
		{
			radiationProducer = this.GetComponent<RadiationProducer>();
			registerObject = this.GetComponent<RegisterObject>();
		}

		public void CycleUpdate()
		{
			switch (massStage)
			{
				case 0:
					return;
				case 1:
					Stage1Update();
					return;
				case 2:
					return;
				case 3:
					return;
				case 3.5M:
					return;
				case 4:
					return;
				case 4.5M:
					return;
				case 5:
					BigBang();
					UpdateManager.Remove(CallbackType.PERIODIC_UPDATE, CycleUpdate); // absolutely necessary so that the thing doesn't explode multiple times
					Despawn.ServerSingle(this.gameObject);
					return;
			}
		}
		/// <summary>
		/// temporary thing until I implement the laser thing
		/// </summary>
		public bool WillInteract( HandApply interaction, NetworkSide side)
		{
			if (!DefaultWillInteract.Default(interaction, side))
				return false;
			if (massStage != 0 || interaction.HandObject != null)
				return false;
			return true;
		}
		/// <summary>
		/// temporary thing until I implement the laser thing
		/// </summary>
		public void ServerPerformInteraction(HandApply interaction)
		{
			if (interaction.HandObject == null)
			{
				ToolUtils.ServerUseToolWithActionMessages(interaction, 10,
					"You begin to open the M.A.S.S. Solar Containment Device...",
					$"{interaction.Performer.ExpensiveName()} starts to deconstruct the ReactorTurbine...",
					"You unleash the star. You should back up a bit.",
					$"{interaction.Performer.ExpensiveName()} deconstruct the ReactorTurbine.",
					() =>
					{
						massStage = 1;
					});
			}
		}

		public void Stage1Update()
		{
			// all temporary editable editor values until i can determine some good things
			OutputEnergy = (decimal) (editorPresentTritium * energyEditorFlux);
			if (editorPresentTritium >= stage1Upgrade)
			{
				massStage = 2;
			}
		}
		/// <summary>
		/// if this doesn't crash my computer i'd be pretty damn pleased
		/// </summary>
		public void BigBang()
		{
			Logger.LogError(" M.A.S.S. Core !!!Uh Oh!!!", Category.Editor);
			Explosions.Explosion.StartExplosion(registerObject.LocalPosition, 240000, registerObject.Matrix); // twice the size of the nuclear reactor blast, this oughtta be fun
		}
	}
}