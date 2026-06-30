using System.Collections.Generic;
using Barotrauma;
using Barotrauma.Networking;

namespace BadAir;

public static class AtmosphereNetSync
{
	public const string NetId = "ba_atmos_sync";

	public const float BroadcastInterval = 0.5f;

	private const float SyncThreshold = 401f;

	private const float SmokeSyncThreshold = 1f;

	private static bool ShouldSync(HullAtmosphere atm)
	{
		if (!(atm.CO2 > 401f))
		{
			return atm.Smoke > 1f;
		}
		return true;
	}

	public static void WriteSnapshot(IWriteMessage msg)
	{
		List<Hull> hullList = Hull.HullList;
		uint num = 0u;
		for (int i = 0; i < hullList.Count; i++)
		{
			if (HullAtmosphere.TryGet(hullList[i], out HullAtmosphere atmosphere) && ShouldSync(atmosphere))
			{
				num++;
			}
		}
		msg.WriteVariableUInt32(num);
		for (int j = 0; j < hullList.Count; j++)
		{
			Hull val = hullList[j];
			if (HullAtmosphere.TryGet(val, out HullAtmosphere atmosphere2) && ShouldSync(atmosphere2))
			{
				msg.WriteUInt16(((Entity)val).ID);
				msg.WriteSingle(atmosphere2.CO2);
				msg.WriteSingle(atmosphere2.Smoke);
			}
		}
	}

	public static void ApplySnapshot(IReadMessage msg)
	{
		List<Hull> hullList = Hull.HullList;
		for (int i = 0; i < hullList.Count; i++)
		{
			if (HullAtmosphere.TryGet(hullList[i], out HullAtmosphere atmosphere))
			{
				atmosphere.CO2 = 400f;
				atmosphere.Smoke = 0f;
			}
		}
		uint num = msg.ReadVariableUInt32();
		for (uint num2 = 0u; num2 < num; num2++)
		{
			ushort num3 = msg.ReadUInt16();
			float cO = msg.ReadSingle();
			float smoke = msg.ReadSingle();
			Entity obj = Entity.FindEntityByID(num3);
			Hull val = (Hull)(object)((obj is Hull) ? obj : null);
			if (val != null)
			{
				HullAtmosphere hullAtmosphere = HullAtmosphere.Get(val);
				hullAtmosphere.CO2 = cO;
				hullAtmosphere.Smoke = smoke;
			}
		}
	}
}

