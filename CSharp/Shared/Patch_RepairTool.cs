using System;
using Barotrauma;
using Barotrauma.Items.Components;

namespace BadAir;

public static class Patch_RepairTool
{
	public static void Use_Postfix(RepairTool __instance, float deltaTime, Character character, bool __result)
	{
		if (__result && character != null && !(__instance.FireDamage <= 0f))
		{
			Hull currentHull = character.CurrentHull;
			if (currentHull != null)
			{
				float num = Math.Max(AtmosphereSim.EffectiveAirVolume(currentHull), 20000f);
				HullAtmosphere.Get(currentHull).CO2 += 300000f * __instance.FireDamage / num * deltaTime;
			}
		}
	}
}

