using System;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Items.Components;

namespace BadAir;

internal static class FilterSlotInjector
{
	private static readonly Identifier OxygenGeneratorTag = IdentifierExtensions.ToIdentifier("oxygengenerator");

	private const string FilterId = "ba_oxygenfilter";

	private const string FilterSlotXml = "<ItemContainer capacity=\"1\" maxstacksize=\"1\" canbeselected=\"true\" hideitems=\"false\" itempos=\"120,-150\" UILabel=\"ba.filterslot\" msg=\"ItemMsgInteractSelect\" containedspritedepth=\"0.81\"><GuiFrame relativesize=\"0.12,0.16\" minsize=\"190,180\" anchor=\"Center\" style=\"ItemUI\" /><Containable items=\"ba_oxygenfilter\" /></ItemContainer>";

	private static readonly Identifier AirFilterItemId = IdentifierExtensions.ToIdentifier("airfilter");

	private static ItemPrefab? filterPrefab;

	private static readonly Identifier FilterIdentifier = IdentifierExtensions.ToIdentifier("ba_oxygenfilter");

	public static void Inject()
	{
		if (ThirdPartyAirFilterPresent())
		{
			NeutralizeOwnFilterItem();
			Plugin.Log("Third-party oxygen filter ('airfilter', e.g. Immersive Repairs) detected — deferring: no slot injected, our filter disabled.");
			return;
		}
		foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
		{
			if (((MapEntityPrefab)prefab).Tags.Contains(OxygenGeneratorTag))
			{
				ContentXElement configElement = prefab.ConfigElement;
				ContentXElement val = null;
				if (!(configElement == val) && !HasFilterSlot(configElement))
				{
					XElement xElement = XElement.Parse("<ItemContainer capacity=\"1\" maxstacksize=\"1\" canbeselected=\"true\" hideitems=\"false\" itempos=\"120,-150\" UILabel=\"ba.filterslot\" msg=\"ItemMsgInteractSelect\" containedspritedepth=\"0.81\"><GuiFrame relativesize=\"0.12,0.16\" minsize=\"190,180\" anchor=\"Center\" style=\"ItemUI\" /><Containable items=\"ba_oxygenfilter\" /></ItemContainer>");
					configElement.Add(new ContentXElement(configElement.ContentPackage, xElement));
					Plugin.Log($"Added a filter slot to oxygen generator '{((Prefab)prefab).Identifier}'.");
				}
			}
		}
	}

	private static bool ThirdPartyAirFilterPresent()
	{
		foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
		{
			if (prefab.Identifier == AirFilterItemId)
			{
				return true;
			}
		}
		return false;
	}

	private static void NeutralizeOwnFilterItem()
	{
		foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
		{
			if (!(prefab.Identifier != FilterIdentifier))
			{
				prefab.FabricationRecipes = prefab.FabricationRecipes.Clear();
				prefab.PreferredContainers = prefab.PreferredContainers.Clear();
				break;
			}
		}
	}

	public static void Remove()
	{
		foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
		{
			if (!((MapEntityPrefab)prefab).Tags.Contains(OxygenGeneratorTag))
			{
				continue;
			}
			ContentXElement configElement = prefab.ConfigElement;
			ContentXElement val = null;
			if (configElement == val)
			{
				continue;
			}
			foreach (ContentXElement item in configElement.Elements())
			{
				if (IsFilterContainer(item))
				{
					item.Element.Remove();
					break;
				}
			}
		}
	}

	public static void Item_OnMapLoaded_Postfix(Item __instance)
	{
		if (((__instance != null) ? ((Entity)__instance).Submarine : null) == null)
		{
			return;
		}
		SubmarineInfo info = ((Entity)__instance).Submarine.Info;
		if ((info != null && info.IsPlayer) || __instance.GetComponent<OxygenGenerator>() == null)
		{
			return;
		}
		if (filterPrefab == null)
		{
			filterPrefab = FindFilterPrefab();
		}
		if (filterPrefab == null)
		{
			return;
		}
		foreach (ItemContainer component in __instance.GetComponents<ItemContainer>())
		{
			if (component.CanBeContained(filterPrefab))
			{
				((ItemComponent)component).CanBeSelected = false;
			}
		}
	}

	private static ItemPrefab? FindFilterPrefab()
	{
		foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
		{
			if (prefab.Identifier == FilterIdentifier)
			{
				return prefab;
			}
		}
		return null;
	}

	private static bool HasFilterSlot(ContentXElement config)
	{
		foreach (ContentXElement item in config.Elements())
		{
			if (IsFilterContainer(item))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsFilterContainer(ContentXElement element)
	{
		if (!element.Name.ToString().Equals("ItemContainer", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		foreach (ContentXElement item in element.Elements())
		{
			if (item.Name.ToString().Equals("Containable", StringComparison.OrdinalIgnoreCase) && (item.GetAttributeString("items", "") ?? "").Contains("ba_oxygenfilter"))
			{
				return true;
			}
		}
		return false;
	}
}

