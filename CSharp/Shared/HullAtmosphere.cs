using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Barotrauma;

namespace BadAir;

public sealed class HullAtmosphere
{
	public float CO2 = 400f;

	public float Smoke;

	private static readonly ConditionalWeakTable<Hull, HullAtmosphere> Table = new ConditionalWeakTable<Hull, HullAtmosphere>();

	private static readonly ConditionalWeakTable<Hull, HullAtmosphere>.CreateValueCallback Factory = (Hull _) => new HullAtmosphere();

	public static HullAtmosphere Get(Hull hull)
	{
		return Table.GetValue(hull, Factory);
	}

	public static bool TryGet(Hull hull, [NotNullWhen(true)] out HullAtmosphere? atmosphere)
	{
		return Table.TryGetValue(hull, out atmosphere);
	}
}

