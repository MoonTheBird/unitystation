using Machines;
using MassEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MassEnergyCollector : MonoBehaviour
{
	[SerializeField] private float tickRate = 1;
	private float tickCount;

	public ModuleSupplyingDevice moduleSupplyingDevice;

	public MassEngineCore massCore;

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
			moduleSupplyingDevice.ProducingWatts = (float)massCore.OutputEnergy/8;
		}
		else
		{
			moduleSupplyingDevice.ProducingWatts = 0;
		}
	}
}
