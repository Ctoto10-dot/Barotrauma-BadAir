using Barotrauma;
using Microsoft.Xna.Framework;

namespace BadAir;

public static class Patch_Hull
{
	public static void OxygenPercentage_Postfix(Hull __instance, ref float __result)
	{
		if (!(__instance.Volume <= 0f))
		{
			float num = 1f - __instance.WaterVolume / __instance.Volume;
			if (!(num >= 0.999f))
			{
				__result = MathHelper.Clamp(__result / MathHelper.Clamp(num, 0.02f, 1f), 0f, 100f);
			}
		}
	}
}

