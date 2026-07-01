using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Barotrauma;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;

namespace BadAir;

public static class AtmosphereSim
{
	public const float AmbientCO2 = 400f;

	public const float MaxCO2 = 100000f;

	public const float Co2PerBreatherPerSecond = 20f;

	public const float ReferenceVolume = 100000f;

	public const float BreathCo2Output = 2000000f;

	public const float MinHullVolume = 20000f;

	public const float VentilatedScrubPerSecond = 0.5f;

	public const float UnfilteredScrubPerSecond = 0.12f;

	public const float BaselineLeakPerSecond = 0.0005f;

	public const float DiffusionPerSecond = 2f;

	public const float StratifyPerSecond = 0.2f;

	public const float FireCo2OutputPerWidth = 150000f;

	public const float ToolCo2OutputPerFireDamage = 300000f;

	public const float BreachVentPerSecond = 0.5f;

	public const float MaxSmoke = 100f;

	public const float SmokeOutputPerWidth = 9000f;

	public const float SmokeDiffusionPerSecond = 2.5f;

	public const float SmokeRisePerSecond = 0.3f;

	public const float SmokeSettlePerSecond = 0.03f;

	public const float VentilatedSmokeScrubPerSecond = 0.12f;

	public const float FilteredSmokeScrubPerSecond = 0.45f;

	public const float WaterDouseSmokePerSecond = 2f;

	public const float SmokeGeneratorDamageThreshold = 30f;

	public const float SmokeGeneratorDamagePerSecond = 4f;

	public const float FilterWearPerSecond = 0.05f;

	public const float FilterWearReferenceCO2 = 4000f;

	public const float SmokeFilterWearPerSecond = 2f;

	public const float SafeCO2 = 1500f;

	public const float DangerCO2 = 8000f;

	public const float HypercapniaRampPerSecond = 3f;

	public const float HypercapniaRecoverPerSecond = 5f;

	public const float SafeSmoke = 3f;

	public const float DangerSmoke = 45f;

	public const float SmokeRampPerSecond = 6f;

	public const float SmokeRecoverPerSecond = 8f;

	public const float SevereSmoke = 60f;

	public const float SevereSmokeRecoverPerSecond = 0.3f;

	public const float ArrestOxygenDrainPerSecond = 3f;

	public const float OxygeniteBreathHealPerSecond = 6f;

	public const float OxygeniteTreatmentDrainPerSecond = 1.5f;

	public const float OxygenBreathHealPerSecond = 0.5f;

	public const float ShardScrubPerSecond = 0.005f;

	public const float ShardScanInterval = 1f;

	private static readonly Identifier HypercapniaId = IdentifierExtensions.ToIdentifier("ba_hypercapnia");

	private static readonly Identifier SmokeId = IdentifierExtensions.ToIdentifier("ba_smoke_inhalation");

	private static readonly Identifier DivingTag = IdentifierExtensions.ToIdentifier("diving");

	private static readonly Identifier OxygenSourceTag = IdentifierExtensions.ToIdentifier("oxygensource");

	private static readonly Identifier OxygeniteTankId = IdentifierExtensions.ToIdentifier("oxygenitetank");

	private static readonly Identifier OxygenFilterId = IdentifierExtensions.ToIdentifier("ba_oxygenfilter");

	private static readonly Identifier AirFilterTag = IdentifierExtensions.ToIdentifier("airfilter");

	private static readonly Identifier OxygenLowId = IdentifierExtensions.ToIdentifier("oxygenlow");

	private static readonly Identifier OxygeniteShardId = IdentifierExtensions.ToIdentifier("oxygeniteshard");

	private static AfflictionPrefab? hypercapniaPrefab;

	private static bool prefabResolved;

	private static AfflictionPrefab? smokePrefab;

	private static bool smokePrefabResolved;

	private static AfflictionPrefab? oxygenLowPrefab;

	private static bool oxygenLowResolved;

	private static readonly ConditionalWeakTable<Character, StrongBox<float>> arrestCeiling = new ConditionalWeakTable<Character, StrongBox<float>>();

	private static readonly ConditionalWeakTable<Character, StrongBox<float>>.CreateValueCallback OxygenCeilingFactory = (Character c) => new StrongBox<float>(c.Oxygen);

	private static List<OxygenGenerator>? generatorCache;

	private static readonly HashSet<Hull> ventilatedHulls = new HashSet<Hull>();

	private static readonly HashSet<Hull> filteredHulls = new HashSet<Hull>();

	private static List<Reactor>? reactorCache;

	private static readonly HashSet<Submarine> poweredStations = new HashSet<Submarine>();

	private static float shardScanTimer;

	private static double lastTime = -1.0;

	private static object? lastRound;

	public static void Update()
	{
		object loaded = Level.Loaded;
		if (loaded != lastRound)
		{
			Reset();
			lastRound = loaded;
		}
		AtmospherePersistence.RestoreIfPending();
		double totalTime = Timing.TotalTime;
		if (lastTime < 0.0)
		{
			lastTime = totalTime;
			return;
		}
		float num = (float)(totalTime - lastTime);
		lastTime = totalTime;
		if (!(num <= 0f) && !(num > 0.5f))
		{
			Tick(num);
		}
	}

	public static void UpdateClient()
	{
		object loaded = Level.Loaded;
		if (loaded != lastRound)
		{
			Reset();
			lastRound = loaded;
		}
		double totalTime = Timing.TotalTime;
		if (lastTime < 0.0)
		{
			lastTime = totalTime;
			return;
		}
		float num = (float)(totalTime - lastTime);
		lastTime = totalTime;
		if (!(num <= 0f) && !(num > 0.5f))
		{
			TickCharacters(num);
		}
	}

	public static void Reset()
	{
		lastTime = -1.0;
		prefabResolved = false;
		hypercapniaPrefab = null;
		smokePrefabResolved = false;
		smokePrefab = null;
		oxygenLowResolved = false;
		oxygenLowPrefab = null;
		generatorCache = null;
		reactorCache = null;
		shardScanTimer = 0f;
	}

	public static void Tick(float deltaTime)
	{
		TickCharacters(deltaTime);
		TickHulls(deltaTime);
	}

	public static void TickCharacters(float deltaTime)
	{
		List<Character> characterList = Character.CharacterList;
		for (int i = 0; i < characterList.Count; i++)
		{
			Character val = characterList[i];
			if (val != null && !val.IsDead && val.NeedsAir && !val.HasAbilityFlag((AbilityFlags)2))
			{
				Hull currentHull = val.CurrentHull;
				bool flag = val.AnimController != null && ((Ragdoll)val.AnimController).HeadInWater;
				Item breathingOxygenSource = GetBreathingOxygenSource(val);
				if (currentHull != null && !flag && breathingOxygenSource == null)
				{
					HullAtmosphere hullAtmosphere = HullAtmosphere.Get(currentHull);
					float num = Math.Max(EffectiveAirVolume(currentHull), 20000f);
					hullAtmosphere.CO2 += 2000000f / num * deltaTime;
					ApplyHypercapnia(val, hullAtmosphere.CO2, deltaTime);
					ApplySmoke(val, hullAtmosphere.Smoke, breathingHullAir: true, deltaTime);
				}
				else if (breathingOxygenSource != null && breathingOxygenSource.Prefab.Identifier == OxygeniteTankId)
				{
					float amount = 6f * deltaTime;
					HealAffliction(val, SmokeId, amount);
					HealAffliction(val, HypercapniaId, amount);
					breathingOxygenSource.Condition = Math.Max(0f, breathingOxygenSource.Condition - 1.5f * deltaTime);
					arrestCeiling.Remove(val);
				}
				else if (breathingOxygenSource != null)
				{
					float amount2 = 0.5f * deltaTime;
					HealAffliction(val, SmokeId, amount2);
					HealAffliction(val, HypercapniaId, amount2);
					arrestCeiling.Remove(val);
				}
				else
				{
					ApplyHypercapnia(val, 400f, deltaTime);
					ApplySmoke(val, 0f, breathingHullAir: false, deltaTime);
				}
			}
		}
	}

	public static void TickHulls(float deltaTime)
	{
		List<Hull> hullList2 = Hull.HullList;
		for (int l = 0; l < hullList2.Count; l++)
		{
			List<Gap> connectedGaps = hullList2[l].ConnectedGaps;
			for (int m = 0; m < connectedGaps.Count; m++)
			{
				Gap val4 = connectedGaps[m];
				if (val4 == null || !val4.IsRoomToRoom || val4.Open <= 0f || ((MapEntity)val4).linkedTo.Count < 2)
				{
					continue;
				}
				MapEntity obj = ((MapEntity)val4).linkedTo[0];
				Hull val5 = (Hull)(object)((obj is Hull) ? obj : null);
				if (val5 == null)
				{
					continue;
				}
				MapEntity obj2 = ((MapEntity)val4).linkedTo[1];
				Hull val6 = (Hull)(object)((obj2 is Hull) ? obj2 : null);
				if (val6 == null || hullList2[l] != val5)
				{
					continue;
				}
				HullAtmosphere atmosphere;
				bool num3 = HullAtmosphere.TryGet(val5, out atmosphere);
				HullAtmosphere atmosphere2;
				bool flag2 = HullAtmosphere.TryGet(val6, out atmosphere2);
				if (!num3 && !flag2)
				{
					continue;
				}
				if (atmosphere == null)
				{
					atmosphere = HullAtmosphere.Get(val5);
				}
				if (atmosphere2 == null)
				{
					atmosphere2 = HullAtmosphere.Get(val6);
				}
				float num4 = EffectiveAirVolume(val5);
				float num5 = EffectiveAirVolume(val6);
				if (num4 + num5 < 1f)
				{
					continue;
				}
				float num6 = MathHelper.Clamp(2f * val4.Open * deltaTime, 0f, 0.5f);
				float num7 = (atmosphere.CO2 * num4 + atmosphere2.CO2 * num5) / (num4 + num5);
				atmosphere.CO2 += (num7 - atmosphere.CO2) * num6;
				atmosphere2.CO2 += (num7 - atmosphere2.CO2) * num6;
				float num8 = MathHelper.Clamp(2.5f * val4.Open * deltaTime, 0f, 0.5f);
				float num9 = (atmosphere.Smoke * num4 + atmosphere2.Smoke * num5) / (num4 + num5);
				atmosphere.Smoke += (num9 - atmosphere.Smoke) * num8;
				atmosphere2.Smoke += (num9 - atmosphere2.Smoke) * num8;
				if (!val4.IsHorizontal)
				{
					bool num10 = ((Entity)val5).WorldPosition.Y >= ((Entity)val6).WorldPosition.Y;
					HullAtmosphere hullAtmosphere3 = (num10 ? atmosphere : atmosphere2);
					HullAtmosphere hullAtmosphere4 = (num10 ? atmosphere2 : atmosphere);
					float num11 = (num10 ? num4 : num5);
					float num12 = (num10 ? num5 : num4);
					float num13 = 0.2f * val4.Open * deltaTime * (hullAtmosphere3.CO2 - 400f);
					if (num13 > 0f && num12 >= 1f)
					{
						hullAtmosphere3.CO2 -= num13;
						hullAtmosphere4.CO2 += num13 * num11 / num12;
					}
					float num14 = 0.3f * val4.Open * deltaTime * hullAtmosphere4.Smoke;
					if (num14 > 0f && num11 >= 1f)
					{
						hullAtmosphere4.Smoke -= num14;
						hullAtmosphere3.Smoke += num14 * num12 / num11;
					}
				}
			}
		}
		ventilatedHulls.Clear();
		filteredHulls.Clear();
		RefreshPoweredStations();
		List<OxygenGenerator> generators = GetGenerators();
		for (int n = 0; n < generators.Count; n++)
		{
			OxygenGenerator val7 = generators[n];
			if (((val7 != null) ? ((ItemComponent)val7).Item : null) == null || ((Entity)((ItemComponent)val7).Item).Removed || val7.CurrFlow <= 0f)
			{
				continue;
			}
			Item generatorFilter = GetGeneratorFilter(val7);
			Submarine submarine = ((Entity)((ItemComponent)val7).Item).Submarine;
			bool? obj3;
			if (submarine == null)
			{
				obj3 = null;
			}
			else
			{
				SubmarineInfo info = submarine.Info;
				obj3 = ((info != null) ? new bool?(info.IsPlayer) : ((bool?)null));
			}
			bool? flag3 = obj3;
			bool valueOrDefault = flag3 == true;
			bool flag4 = !valueOrDefault || generatorFilter != null;
			bool flag5 = generatorFilter != null && generatorFilter.Prefab.Identifier == OxygenFilterId;
			float num15 = 0f;
			float num16 = 0f;
			Hull currentHull2 = ((ItemComponent)val7).Item.CurrentHull;
			if (currentHull2 != null && HullAtmosphere.TryGet(currentHull2, out HullAtmosphere atmosphere3))
			{
				num15 = atmosphere3.Smoke;
				num16 = atmosphere3.CO2;
			}
			foreach (MapEntity item in ((MapEntity)((ItemComponent)val7).Item).linkedTo)
			{
				Item val8 = (Item)(object)((item is Item) ? item : null);
				if (val8 == null)
				{
					continue;
				}
				Vent component = val8.GetComponent<Vent>();
				Hull val9 = ((component != null) ? ((ItemComponent)component).Item.CurrentHull : null);
				if (val9 == null)
				{
					continue;
				}
				ventilatedHulls.Add(val9);
				if (flag4)
				{
					filteredHulls.Add(val9);
				}
				if (HullAtmosphere.TryGet(val9, out HullAtmosphere atmosphere4))
				{
					if (atmosphere4.Smoke > num15)
					{
						num15 = atmosphere4.Smoke;
					}
					if (atmosphere4.CO2 > num16)
					{
						num16 = atmosphere4.CO2;
					}
				}
			}
			if (num15 > 30f)
			{
				float num17 = (num15 - 30f) / 70f;
				if (flag5)
				{
					generatorFilter.Condition = Math.Max(0f, generatorFilter.Condition - 2f * num17 * deltaTime);
				}
				else if (generatorFilter == null && valueOrDefault && ((ItemComponent)val7).Item.Condition > 0f)
				{
					((ItemComponent)val7).Item.Condition = Math.Max(0f, ((ItemComponent)val7).Item.Condition - 4f * num17 * deltaTime);
				}
			}
			if (flag5)
			{
				float num18 = num16 - 400f;
				if (num18 > 0f)
				{
					float num19 = 0.05f * (num18 / 4000f) * deltaTime;
					generatorFilter.Condition = Math.Max(0f, generatorFilter.Condition - num19);
				}
			}
		}

		List<Hull> hullList3 = Hull.HullList;
		for (int num20 = 0; num20 < hullList3.Count; num20++)
		{
			Hull val10 = hullList3[num20];

			List<FireSource> fireSources = val10.FireSources;
			if (fireSources != null && fireSources.Count > 0)
			{
				float num2 = Math.Max(EffectiveAirVolume(val10), 20000f);
				HullAtmosphere hullAtmosphere2 = HullAtmosphere.Get(val10);
				for (int k = 0; k < fireSources.Count; k++)
				{
					FireSource val3 = fireSources[k];
					if (val3 != null)
					{
						hullAtmosphere2.CO2 += 150000f * val3.Size.X / num2 * deltaTime;
						hullAtmosphere2.Smoke += 9000f * val3.Size.X / num2 * deltaTime;
					}
				}
			}
			float num21 = OpenOutsideGapAmount(val10);
			float num22 = EffectiveAirVolume(val10);
			if (val10.Oxygen > num22)
			{
				float num23 = val10.Oxygen - num22;
				val10.Oxygen = num22;
				if (num21 <= 0f)
				{
					Hull val11 = HullAbove(val10);
					if (val11 != null)
					{
						val11.Oxygen += num23;
					}
				}
			}
			if (!HullAtmosphere.TryGet(val10, out HullAtmosphere atmosphere5))
			{
				continue;
			}
			bool flag6 = ventilatedHulls.Contains(val10);
			Submarine submarine2 = ((Entity)val10).Submarine;
			SubmarineType? obj4;
			if (submarine2 == null)
			{
				obj4 = null;
			}
			else
			{
				SubmarineInfo info2 = submarine2.Info;
				obj4 = ((info2 != null) ? new SubmarineType?(info2.Type) : ((SubmarineType?)null));
			}
			SubmarineType? val12 = obj4;
			bool isOutpost = val12.HasValue && ((int)val12.Value == 1 || (int)val12.Value == 2);
			bool isFiltered = filteredHulls.Contains(val10);
			float co2Scrub;
			float smokeScrub;
			if (isOutpost)
			{
				co2Scrub = 100f;
				smokeScrub = 100f;
			}
			else if (isFiltered)
			{
				co2Scrub = 0.5f;
				smokeScrub = 0.05f;
			}
			else
			{
				co2Scrub = 0.0005f;
				smokeScrub = 0.01f;
			}
			float num26 = MathHelper.Clamp((co2Scrub + 0.5f * num21) * deltaTime, 0f, 1f);
			atmosphere5.CO2 -= (atmosphere5.CO2 - 400f) * num26;
			float num27 = ((val10.Volume > 0f) ? MathHelper.Clamp(val10.WaterVolume / val10.Volume, 0f, 1f) : 0f);
			float num28 = smokeScrub + 0.5f * num21 + 2f * num27;
			atmosphere5.Smoke -= atmosphere5.Smoke * MathHelper.Clamp(num28 * deltaTime, 0f, 1f);
			atmosphere5.CO2 = MathHelper.Clamp(atmosphere5.CO2, 0f, 100000f);
			atmosphere5.Smoke = MathHelper.Clamp(atmosphere5.Smoke, 0f, 100f);
		}
		shardScanTimer += deltaTime;
		if (shardScanTimer >= 1f)
		{
			ProcessOxygeniteShards(shardScanTimer);
			shardScanTimer = 0f;
		}
	}

	public static float EffectiveAirVolume(Hull hull)
	{
		if (hull.Volume <= 0f)
		{
			return 0f;
		}
		float num = MathHelper.Clamp(1f - hull.WaterVolume / hull.Volume, 0f, 1f);
		return hull.Volume * num;
	}

	private static readonly string[] BreathingGearTags = new string[] { "diving", "deepdiving", "gasmask", "firefightersuit", "hazmatsuit" };

	private static Item? GetBreathingOxygenSource(Character character)
	{
		for (int i = 0; i < character.Inventory.Capacity; i++)
		{
			Item itemAt = character.Inventory.GetItemAt(i);
			if (itemAt != null && character.HasEquippedItem(itemAt) && itemAt.OwnInventory != null)
			{
				bool isBreathingGear = false;
				foreach (string tag in BreathingGearTags)
				{
					if (itemAt.HasTag(tag))
					{
						isBreathingGear = true;
						break;
					}
				}

				if (isBreathingGear)
				{
					foreach (Item allItem in ((Inventory)itemAt.OwnInventory).AllItems)
					{
						if (allItem != null && allItem.Condition > 0f && allItem.HasTag(OxygenSourceTag))
						{
							return allItem;
						}
					}
				}
			}
		}
		return null;
	}

	private static void HealAffliction(Character character, Identifier id, float amount)
	{
		CharacterHealth characterHealth = character.CharacterHealth;
		if (characterHealth != null && !(amount <= 0f))
		{
			float afflictionStrengthByIdentifier = characterHealth.GetAfflictionStrengthByIdentifier(id, true);
			if (!(afflictionStrengthByIdentifier <= 0f))
			{
				characterHealth.ReduceAfflictionOnAllLimbs(id, Math.Min(amount, afflictionStrengthByIdentifier), (ActionType?)null, (Character)null);
			}
		}
	}

	private static float OpenOutsideGapAmount(Hull hull)
	{
		float num = 0f;
		List<Gap> connectedGaps = hull.ConnectedGaps;
		for (int i = 0; i < connectedGaps.Count; i++)
		{
			Gap val = connectedGaps[i];
			if (val != null && !val.IsRoomToRoom && val.Open > num)
			{
				num = val.Open;
			}
		}
		return num;
	}

	private static Hull? HullAbove(Hull hull)
	{
		Hull result = null;
		float y = ((Entity)hull).WorldPosition.Y;
		List<Gap> connectedGaps = hull.ConnectedGaps;
		for (int i = 0; i < connectedGaps.Count; i++)
		{
			Gap val = connectedGaps[i];
			if (val != null && val.IsRoomToRoom && !val.IsHorizontal && !(val.Open <= 0f) && ((MapEntity)val).linkedTo.Count >= 2)
			{
				MapEntity obj = (((object)((MapEntity)val).linkedTo[0] == hull) ? ((MapEntity)val).linkedTo[1] : ((MapEntity)val).linkedTo[0]);
				Hull val2 = (Hull)(object)((obj is Hull) ? obj : null);
				if (val2 != null && ((Entity)val2).WorldPosition.Y > y)
				{
					y = ((Entity)val2).WorldPosition.Y;
					result = val2;
				}
			}
		}
		return result;
	}

	public static float StrengthFromCO2(float co2)
	{
		if (co2 <= 1500f)
		{
			return 0f;
		}
		return MathHelper.Clamp((co2 - 1500f) / 6500f, 0f, 1f) * 100f;
	}

	public static float AirToxicityPercent(float co2)
	{
		return MathHelper.Clamp((co2 - 400f) / 7600f, 0f, 1f) * 100f;
	}

	public static float StrengthFromSmoke(float smoke)
	{
		if (smoke <= 3f)
		{
			return 0f;
		}
		return MathHelper.Clamp((smoke - 3f) / 42f, 0f, 1f) * 100f;
	}

	private static void ApplyHypercapnia(Character character, float co2, float deltaTime)
	{
		AfflictionPrefab val = ResolveHypercapniaPrefab();
		if (val != null)
		{
			float num = StrengthFromCO2(co2);
			if (NeuroTraumaCompat.Active)
			{
				NeuroTraumaCompat.ApplyAcidosis(character, num, deltaTime);
				num = Math.Min(num, 85f);
			}
			float rampPerSecond = 3f * OxygenResistanceFactor(character);
			DriveAffliction(character, val, HypercapniaId, num, rampPerSecond, 5f, deltaTime);
		}
	}

	private static void ApplySmoke(Character character, float smoke, bool breathingHullAir, float deltaTime)
	{
		AfflictionPrefab val = ResolveSmokePrefab();
		if (val == null)
		{
			return;
		}
		CharacterHealth characterHealth = character.CharacterHealth;
		float num = ((characterHealth != null) ? characterHealth.GetAfflictionStrengthByIdentifier(SmokeId, true) : 0f);
		float recoverPerSecond = ((num >= 60f) ? 0.3f : 8f);
		float num2 = StrengthFromSmoke(smoke);
		bool active = NeuroTraumaCompat.Active;
		if (active)
		{
			num2 = Math.Min(num2, 85f);
		}
		float rampPerSecond = 6f * OxygenResistanceFactor(character);
		DriveAffliction(character, val, SmokeId, num2, rampPerSecond, recoverPerSecond, deltaTime);
		if (breathingHullAir && num >= 60f)
		{
			if (active)
			{
				NeuroTraumaCompat.ApplyRespiratoryArrest(character, num, deltaTime);
				NeuroTraumaCompat.ApplyLungDamage(character, num, deltaTime);
				arrestCeiling.Remove(character);
				return;
			}
			StrongBox<float> value = arrestCeiling.GetValue(character, OxygenCeilingFactory);
			value.Value = Math.Max(0f, Math.Min(value.Value, character.Oxygen) - 3f * deltaTime);
			if (character.Oxygen > value.Value)
			{
				character.Oxygen = value.Value;
			}
		}
		else
		{
			arrestCeiling.Remove(character);
		}
	}

	private static void DriveAffliction(Character character, AfflictionPrefab prefab, Identifier id, float target, float rampPerSecond, float recoverPerSecond, float deltaTime)
	{
		CharacterHealth characterHealth = character.CharacterHealth;
		if (characterHealth == null)
		{
			return;
		}
		float afflictionStrengthByIdentifier = characterHealth.GetAfflictionStrengthByIdentifier(id, true);
		float num = ((target > afflictionStrengthByIdentifier) ? rampPerSecond : recoverPerSecond) * deltaTime;
		float num2 = MathHelper.Clamp(target - afflictionStrengthByIdentifier, 0f - num, num);
		if (num2 > 0f)
		{
			Affliction affliction = characterHealth.GetAffliction(id, true);
			if (affliction != null)
			{
				affliction.Strength = Math.Min(affliction.Strength + num2, prefab.MaxStrength);
			}
			else
			{
				characterHealth.ApplyAffliction((Limb)null, prefab.Instantiate(num2, (Character)null), true, false, true);
			}
		}
		else if (num2 < 0f)
		{
			characterHealth.ReduceAfflictionOnAllLimbs(id, 0f - num2, (ActionType?)null, (Character)null);
		}
	}

	private static AfflictionPrefab? ResolveHypercapniaPrefab()
	{
		if (prefabResolved)
		{
			return hypercapniaPrefab;
		}
		prefabResolved = true;
		foreach (AfflictionPrefab prefab in AfflictionPrefab.Prefabs)
		{
			if (prefab.Identifier == HypercapniaId)
			{
				hypercapniaPrefab = prefab;
				break;
			}
		}
		if (hypercapniaPrefab == null)
		{
			Plugin.Log("WARNING: affliction 'ba_hypercapnia' not found — is Content/Afflictions.xml loaded?");
		}
		return hypercapniaPrefab;
	}

	private static AfflictionPrefab? ResolveOxygenLowPrefab()
	{
		if (oxygenLowResolved)
		{
			return oxygenLowPrefab;
		}
		oxygenLowResolved = true;
		foreach (AfflictionPrefab prefab in AfflictionPrefab.Prefabs)
		{
			if (prefab.Identifier == OxygenLowId)
			{
				oxygenLowPrefab = prefab;
				break;
			}
		}
		return oxygenLowPrefab;
	}

	private static float OxygenResistanceFactor(Character character)
	{
		AfflictionPrefab val = ResolveOxygenLowPrefab();
		CharacterHealth characterHealth = character.CharacterHealth;
		if (val == null || characterHealth == null)
		{
			return 1f;
		}
		float resistance = characterHealth.GetResistance(val, (LimbType)12);
		return MathHelper.Clamp(1f - resistance, 0f, 1f);
	}

	private static void ProcessOxygeniteShards(float deltaTime)
	{
		List<Item> itemList = Item.ItemList;
		for (int i = 0; i < itemList.Count; i++)
		{
			Item val = itemList[i];
			if (val != null && !((Entity)val).Removed && val.ParentInventory == null && !(val.Prefab.Identifier != OxygeniteShardId))
			{
				Hull currentHull = val.CurrentHull;
				if (currentHull != null && HullAtmosphere.TryGet(currentHull, out HullAtmosphere atmosphere) && !(atmosphere.CO2 <= 400f))
				{
					atmosphere.CO2 -= (atmosphere.CO2 - 400f) * MathHelper.Clamp(0.08f * deltaTime, 0f, 1f);
				}
			}
		}
	}

	private static AfflictionPrefab? ResolveSmokePrefab()
	{
		if (smokePrefabResolved)
		{
			return smokePrefab;
		}
		smokePrefabResolved = true;
		foreach (AfflictionPrefab prefab in AfflictionPrefab.Prefabs)
		{
			if (prefab.Identifier == SmokeId)
			{
				smokePrefab = prefab;
				break;
			}
		}
		if (smokePrefab == null)
		{
			Plugin.Log("WARNING: affliction 'ba_smoke_inhalation' not found — is Content/Afflictions.xml loaded?");
		}
		return smokePrefab;
	}

	private static Item? GetGeneratorFilter(OxygenGenerator generator)
	{
		List<ItemComponent> components = ((ItemComponent)generator).Item.Components;
		for (int i = 0; i < components.Count; i++)
		{
			ItemComponent obj = components[i];
			ItemContainer val = (ItemContainer)(object)((obj is ItemContainer) ? obj : null);
			if (val == null)
			{
				continue;
			}
			ItemInventory inventory = val.Inventory;
			if (inventory == null)
			{
				continue;
			}
			foreach (Item allItem in ((Inventory)inventory).AllItems)
			{
				if (allItem != null && allItem.Condition > 0f && (allItem.Prefab.Identifier == OxygenFilterId || allItem.HasTag(AirFilterTag)))
				{
					return allItem;
				}
			}
		}
		return null;
	}

	private static List<OxygenGenerator> GetGenerators()
	{
		if (generatorCache != null)
		{
			return generatorCache;
		}
		generatorCache = new List<OxygenGenerator>();
		List<Item> itemList = Item.ItemList;
		for (int i = 0; i < itemList.Count; i++)
		{
			OxygenGenerator component = itemList[i].GetComponent<OxygenGenerator>();
			if (component != null)
			{
				generatorCache.Add(component);
			}
		}
		return generatorCache;
	}

	private static List<Reactor> GetReactors()
	{
		if (reactorCache != null)
		{
			return reactorCache;
		}
		reactorCache = new List<Reactor>();
		List<Item> itemList = Item.ItemList;
		for (int i = 0; i < itemList.Count; i++)
		{
			Reactor component = itemList[i].GetComponent<Reactor>();
			if (component != null)
			{
				reactorCache.Add(component);
			}
		}
		return reactorCache;
	}

	private static void RefreshPoweredStations()
	{
		poweredStations.Clear();
		List<Reactor> reactors = GetReactors();
		for (int i = 0; i < reactors.Count; i++)
		{
			Reactor val = reactors[i];
			if (((val != null) ? ((ItemComponent)val).Item : null) != null && !((Entity)((ItemComponent)val).Item).Removed && ((Entity)((ItemComponent)val).Item).Submarine != null && ((ItemComponent)val).Item.Condition > 0f && (val.PowerOn || val.FissionRate > 0f))
			{
				poweredStations.Add(((Entity)((ItemComponent)val).Item).Submarine);
			}
		}
	}
}

