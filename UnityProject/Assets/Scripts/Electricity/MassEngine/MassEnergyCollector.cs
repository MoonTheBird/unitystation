using Machines;
using MassEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MassEnergyCollector : MonoBehaviour
{
	[SerializeField] private float tickRate = 1;
	private float tickCount;

	public ModuleSupplyingDevice moduleSupplyingDevice;

	public MassEngineCore massCore;

	[HideInInspector]
	public double fallOffMultiplier = 0.75;

	public void Start()
	{
		moduleSupplyingDevice = this.GetComponent<ModuleSupplyingDevice>();
	}

	private void OnEnable()
	{
		if (CustomNetworkManager.Instance._isServer == false) return;

		UpdateManager.Add(CycleUpdate, 1);
		moduleSupplyingDevice?.TurnOnSupply();
	}

	private void OnDisable()
	{
		if (CustomNetworkManager.Instance._isServer == false) return;

		UpdateManager.Remove(CallbackType.PERIODIC_UPDATE, CycleUpdate);
		moduleSupplyingDevice?.TurnOffSupply();
	}

	// Update is called once per frame
	public void CycleUpdate()
	{
		if (massCore != null)
		{
			if (massCore.energyCollectorNum <= 8)
			{
				moduleSupplyingDevice.ProducingWatts = (((float)massCore.OutputEnergy * (float)fallOffMultiplier) / massCore.energyCollectorNum) * (massCore.energyCollectorNum / 8);
			}
			else if (massCore.energyCollectorNum > 8)
			{
				moduleSupplyingDevice.ProducingWatts = ((float)massCore.OutputEnergy * (float)fallOffMultiplier) / massCore.energyCollectorNum;
			}
		}
		else
		{
			moduleSupplyingDevice.ProducingWatts = 0;
		}
	}
}
