using Barotrauma;
using Microsoft.Xna.Framework;

namespace BadAir;

public static class Patch_HumanAIController
{
	private const float BotConcernCO2 = 2500f;

	private const float BotDangerCO2 = 5500f;

	private const float BotConcernSmoke = 10f;

	private const float BotDangerSmoke = 35f;

	public static void CalculateHullSafety_Postfix(Hull hull, bool ignoreOxygen, ref float __result)
	{
		if (ignoreOxygen || hull == null || __result <= 0f || !HullAtmosphere.TryGet(hull, out HullAtmosphere atmosphere))
		{
			return;
		}
		float num = 1f;
		if (atmosphere.CO2 > 2500f)
		{
			num = MathHelper.Lerp(1f, 0f, MathUtils.InverseLerp(2500f, 5500f, atmosphere.CO2));
		}
		if (atmosphere.Smoke > 10f)
		{
			float num2 = MathHelper.Lerp(1f, 0f, MathUtils.InverseLerp(10f, 35f, atmosphere.Smoke));
			if (num2 < num)
			{
				num = num2;
			}
		}
		if (num < 1f)
		{
			__result *= num;
		}
	}
}

