using System;
using System.Collections.Generic;
using System.Globalization;
using Barotrauma;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;

namespace BadAir;

#if CLIENT
public static class Patch_MiniMap
{
    private static readonly Color SafeColor = new Color(110, 210, 110);
    private static readonly Color WarnColor = new Color(235, 170, 60);
    private static readonly Color DangerColor = new Color(225, 85, 70);

    public static void SetTooltip_Prefix(MiniMap __instance, ref LocalizedString line2, ref Color? line2Color)
    {
        if (line2 != null && !string.IsNullOrEmpty(line2.Value) && line2.Value != TextManager.Get("minimapairqualityunavailable").Value && __instance.hullStatusComponents != null)
        {
            Hull? hull = FindHoveredHull(__instance);
            if (hull != null)
            {
                // Заменяем ванильные надписи на короткий O2
                string modifiedLine2 = line2.Value.Replace("Качество воздуха", "O2").Replace("Air quality", "O2");
                line2 = modifiedLine2;

                float co2 = HullAtmosphere.TryGet(hull, out var atmosphere) ? atmosphere.CO2 : 400f;
                float percent = AtmosphereSim.AirToxicityPercent(co2);
                string text = ((int)Math.Round(percent)).ToString(CultureInfo.InvariantCulture);
                
                line2 += "\n";
                line2 += TextManager.GetWithVariable("badair.hud.co2", "[value]", text, FormatCapitals.Yes);
                line2Color = (percent < 25f) ? SafeColor : ((percent < 60f) ? WarnColor : DangerColor);

                float smokePercent = AtmosphereSim.StrengthFromSmoke(atmosphere?.Smoke ?? 0f);
                if (smokePercent > 0f)
                {
                    string smokeText = ((int)Math.Round(smokePercent)).ToString(CultureInfo.InvariantCulture);
                    line2 += "\n";
                    line2 += TextManager.GetWithVariable("badair.hud.smoke", "[value]", smokeText, FormatCapitals.Yes);
                }
            }
        }
    }

    private static Hull? FindHoveredHull(MiniMap miniMap)
    {
        GUIComponent mouseOn = GUI.MouseOn;
        if (mouseOn == null) return null;
        
        foreach (var hullStatusComponent in miniMap.hullStatusComponents)
        {
            MapEntity key = hullStatusComponent.Key;
            Hull? hull = key as Hull;
            if (hull == null) continue;
            
            var value = hullStatusComponent.Value;
            if (value.RectComponent == mouseOn)
            {
                return hull;
            }
        }
        return null;
    }
}
#endif
