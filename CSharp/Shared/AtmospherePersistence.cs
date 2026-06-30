using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Barotrauma;
using Microsoft.Xna.Framework;

namespace BadAir;

public static class AtmospherePersistence
{
	private const string SidecarSuffix = ".badair.xml";

	private static string? pendingRestorePath;

	public static void NotifySaveLoaded(string loadPath)
	{
		pendingRestorePath = loadPath;
	}

	private static string SidecarPathFor(string savePath)
	{
		return savePath + ".badair.xml";
	}

	public static void Save(string savePath)
	{
		if (string.IsNullOrEmpty(savePath))
		{
			return;
		}
		Submarine mainSub = Submarine.MainSub;
		if (mainSub == null)
		{
			return;
		}
		XElement xElement = new XElement("BadAirCO2");
		List<Hull> hullList = Hull.HullList;
		for (int i = 0; i < hullList.Count; i++)
		{
			Hull val = hullList[i];
			if (((Entity)val).Submarine == mainSub && HullAtmosphere.TryGet(val, out HullAtmosphere atmosphere) && !(atmosphere.CO2 <= 401f))
			{
				Rectangle rect = ((MapEntity)val).rect;
				xElement.Add(new XElement("h", new XAttribute("x", rect.X), new XAttribute("y", rect.Y), new XAttribute("w", rect.Width), new XAttribute("hh", rect.Height), new XAttribute("co2", ((int)Math.Round(atmosphere.CO2)).ToString(CultureInfo.InvariantCulture))));
			}
		}
		try
		{
			xElement.Save(SidecarPathFor(savePath));
		}
		catch (Exception ex)
		{
			Plugin.Log("Could not write CO2 save data: " + ex.Message);
		}
	}

	public static void RestoreIfPending()
	{
		if (pendingRestorePath == null)
		{
			return;
		}
		Submarine mainSub = Submarine.MainSub;
		if (mainSub == null)
		{
			return;
		}
		string text = SidecarPathFor(pendingRestorePath);
		pendingRestorePath = null;
		if (!File.Exists(text))
		{
			return;
		}
		XDocument xDocument;
		try
		{
			xDocument = XDocument.Load(text);
		}
		catch (Exception ex)
		{
			Plugin.Log("Could not read CO2 save data: " + ex.Message);
			return;
		}
		if (xDocument.Root == null)
		{
			return;
		}
		foreach (XElement item in xDocument.Root.Elements("h"))
		{
			int valueOrDefault = ((int?)item.Attribute("x")).GetValueOrDefault();
			int valueOrDefault2 = ((int?)item.Attribute("y")).GetValueOrDefault();
			int valueOrDefault3 = ((int?)item.Attribute("w")).GetValueOrDefault();
			int valueOrDefault4 = ((int?)item.Attribute("hh")).GetValueOrDefault();
			float cO = ((float?)item.Attribute("co2")) ?? 400f;
			Hull val = FindHull(mainSub, valueOrDefault, valueOrDefault2, valueOrDefault3, valueOrDefault4);
			if (val != null)
			{
				HullAtmosphere.Get(val).CO2 = cO;
			}
		}
	}

	private static Hull? FindHull(Submarine sub, int x, int y, int w, int h)
	{
		List<Hull> hullList = Hull.HullList;
		for (int i = 0; i < hullList.Count; i++)
		{
			Hull val = hullList[i];
			if (((Entity)val).Submarine == sub)
			{
				Rectangle rect = ((MapEntity)val).rect;
				if (rect.X == x && rect.Y == y && rect.Width == w && rect.Height == h)
				{
					return val;
				}
			}
		}
		return null;
	}
}

