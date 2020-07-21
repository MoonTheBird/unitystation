using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Crafting
{
	[CreateAssetMenu(fileName = "ComplexRecipe", menuName = "ScriptableObjects/Recipe/ComplexRecipe")]
	public class ComplexRecipe : ScriptableObject
	{
		[Tooltip("A crafting recipe using items on the ground around the player.")]
		[SerializeField]
		[ArrayElementTitle("name", "unnamed")]
		private List<Ingredients> ingredients = null;

		[Tooltip("The prefab to spawn on completion.")]
		[SerializeField]
		private GameObject spawnPrefab = null;
		public GameObject SpawnPrefab => spawnPrefab;

		[Tooltip("Time it takes (in seconds) to craft.")]
		[SerializeField]
		private float buildTime = 1;
		public float BuildTime => buildTime;

		[Tooltip("Number of instances of the prefab that will be spawned.")]
		[SerializeField]
		private int spawnAmount = 1;
		public int SpawnAmount => spawnAmount;

		public IEnumerable<Ingredients> IngredientsNumber => ingredients.OrderBy(ent => ent.Name);

		[Serializable]
		public class Ingredients
		{
			[Tooltip("Name the player sees.")]
			[SerializeField]
			private string name = null;
			public string Name => name;

			[Tooltip("Prefab of ingredient used.")]
			[SerializeField]
			private GameObject prefab = null;
			public GameObject Prefab => prefab;

			[Tooltip("How much of this ingredient is used in making this.")]
			[SerializeField]
			private int cost = 1;
			public int Cost => cost;
		}
		public GameObject ServerBuild(SpawnDestination at, List<Ingredients> ingredients)
		{
			foreach(Ingredients ingredientscount in ingredients)
			{
				if()
			}

			return Spawn.ServerPrefab(spawnPrefab, at, spawnAmount)?.GameObject;
		}
	}
}
