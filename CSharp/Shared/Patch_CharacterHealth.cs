using Barotrauma;

namespace BadAir;

public static class Patch_CharacterHealth
{
	private static readonly Identifier OxygenLowId = IdentifierExtensions.ToIdentifier("oxygenlow");

	private static readonly Identifier HypercapniaId = IdentifierExtensions.ToIdentifier("ba_hypercapnia");

	private static readonly Identifier SmokeId = IdentifierExtensions.ToIdentifier("ba_smoke_inhalation");

	public const float OxygenCureFactor = 4f;

	public static void ReduceAfflictionOnAllLimbs_Postfix(CharacterHealth __instance, Identifier afflictionIdOrType, float amount, ActionType? treatmentAction)
	{
		if (treatmentAction.HasValue && !(amount <= 0f) && !(afflictionIdOrType != OxygenLowId))
		{
			float num = amount * 4f;
			__instance.ReduceAfflictionOnAllLimbs(HypercapniaId, num, treatmentAction, (Character)null);
			__instance.ReduceAfflictionOnAllLimbs(SmokeId, num, treatmentAction, (Character)null);
		}
	}
}

