using System;
using Barotrauma;
using Microsoft.Xna.Framework;

namespace BadAir;

internal static class MedicalCompat
{
	private static readonly Identifier RespiratoryArrestId = IdentifierExtensions.ToIdentifier("respiratoryarrest");
	private static readonly Identifier AcidosisId = IdentifierExtensions.ToIdentifier("acidosis");
	private static readonly Identifier LungDamageId = IdentifierExtensions.ToIdentifier("lungdamage");

	private static bool resolved;
	private static bool active;

	private static AfflictionPrefab? respiratoryArrest;
	private static AfflictionPrefab? acidosis;
	private static AfflictionPrefab? lungDamage;

	public const float SymptomCeiling = 85f;
	private const float AcidosisFromCo2Factor = 0.4f;
	private const float AcidosisRampPerSecond = 0.15f;
	private const float ArrestRampPerSecond = 0.6f;
	private const float LungDamageFromSmokePerSecond = 0.3f;
	private const float LungDamageCeiling = 100f; // HARDCORE mode for NT

	public static bool Active
	{
		get
		{
			Resolve();
			return active;
		}
	}

	public static bool HasAcidosis
	{
		get
		{
			Resolve();
			return acidosis != null;
		}
	}

	public static bool HasRespiratoryArrest
	{
		get
		{
			Resolve();
			return respiratoryArrest != null;
		}
	}

	public static void Reset()
	{
		resolved = false;
		active = false;
		respiratoryArrest = null;
		acidosis = null;
		lungDamage = null;
	}

	private static void Resolve()
	{
		if (resolved)
		{
			return;
		}
		resolved = true;
		foreach (AfflictionPrefab prefab in AfflictionPrefab.Prefabs)
		{
			if (prefab.Identifier == RespiratoryArrestId)
			{
				respiratoryArrest = prefab;
			}
			else if (prefab.Identifier == AcidosisId)
			{
				acidosis = prefab;
			}
			else if (prefab.Identifier == LungDamageId)
			{
				lungDamage = prefab;
			}
		}
		active = respiratoryArrest != null || acidosis != null || lungDamage != null;
		if (active)
		{
			Plugin.Log("Medical overhaul detected (NeuroTrauma / EMO) — integrating afflictions.");
		}
	}

	public static void ApplyAcidosis(Character character, float co2AfflictionStrength, float deltaTime)
	{
		if (acidosis != null)
		{
			RaiseToward(character, acidosis, AcidosisId, co2AfflictionStrength * AcidosisFromCo2Factor, AcidosisRampPerSecond, deltaTime);
		}
	}

	public static void ApplyRespiratoryArrest(Character character, float smokeAfflictionStrength, float deltaTime)
	{
		if (respiratoryArrest != null)
		{
			float num = MathHelper.Clamp((smokeAfflictionStrength - 60f) / 40f, 0f, 1f);
			float target = MathHelper.Lerp(1f, respiratoryArrest.MaxStrength, num);
			RaiseToward(character, respiratoryArrest, RespiratoryArrestId, target, ArrestRampPerSecond, deltaTime);
		}
	}

	public static void ApplyLungDamage(Character character, float smokeAfflictionStrength, float deltaTime)
	{
		if (lungDamage != null)
		{
			float num = MathHelper.Clamp((smokeAfflictionStrength - 60f) / 40f, 0f, 1f);
			if (!(num <= 0f))
			{
				RaiseToward(character, lungDamage, LungDamageId, LungDamageCeiling, LungDamageFromSmokePerSecond * num, deltaTime);
			}
		}
	}

	private static void RaiseToward(Character character, AfflictionPrefab prefab, Identifier id, float target, float rampPerSecond, float deltaTime)
	{
		CharacterHealth characterHealth = character.CharacterHealth;
		if (characterHealth == null)
		{
			return;
		}
		float afflictionStrengthByIdentifier = characterHealth.GetAfflictionStrengthByIdentifier(id, true);
		if (!(afflictionStrengthByIdentifier >= target))
		{
			float num = Math.Min(rampPerSecond * deltaTime, target - afflictionStrengthByIdentifier);
			Affliction affliction = characterHealth.GetAffliction(id, true);
			if (affliction != null)
			{
				affliction.Strength = Math.Min(affliction.Strength + num, prefab.MaxStrength);
			}
			else
			{
				characterHealth.ApplyAffliction((Limb)null, prefab.Instantiate(num, (Character)null), true, false, true);
			}
		}
	}
}
