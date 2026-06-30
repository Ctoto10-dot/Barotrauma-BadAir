using System;
using System.Collections.Generic;
using System.Reflection;
using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Events;
using Barotrauma.Networking;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace BadAir;

public class Plugin : IAssemblyPlugin, IDisposable, IEventPluginPreInitialize, IEvent<IEventPluginPreInitialize>, IEvent, IEventPluginInitialize, IEvent<IEventPluginInitialize>, IEventPluginLoadCompleted, IEvent<IEventPluginLoadCompleted>
{
	private const string ServerThinkHook = "ba_server_think";

	private double lastAtmosBroadcast = -1.0;

	public const string ModName = "Bad Air";

	public const string ModVersion = "1.0.0";

	public const string HarmonyId = "ctoto.barotrauma.badair";

	internal readonly Harmony Harmony = new Harmony("ctoto.barotrauma.badair");

	public static Plugin? Instance { get; private set; }


	public void InitServer()
	{
		LuaCsSetup.Instance.Hook.Add("think", "ba_server_think", (LuaCsFunc)ServerThink, (object)this);
		Log("Server side initialized.");
	}

	public object ServerThink(object[] args)
	{
		if (GameMain.GameSession != null)
		{
			AtmosphereSim.Update();
			BroadcastAtmosphere();
		}
		return null;
	}

	private void BroadcastAtmosphere()
	{
		INetworkingService networking = LuaCsSetup.Instance.Networking;
		if (networking != null && networking.IsActive)
		{
			double totalTime = Timing.TotalTime;
			if (!(lastAtmosBroadcast >= 0.0) || !(totalTime - lastAtmosBroadcast < 0.5))
			{
				lastAtmosBroadcast = totalTime;
				IWriteMessage val = networking.Start("ba_atmos_sync");
				AtmosphereNetSync.WriteSnapshot(val);
				var sendMethod = networking.GetType().GetMethod("Send", new Type[] { typeof(IWriteMessage), typeof(NetworkConnection), typeof(DeliveryMethod) });
				if (sendMethod != null)
				{
					sendMethod.Invoke(networking, new object[] { val, null, Enum.ToObject(typeof(DeliveryMethod), 0) });
				}
			}
		}
	}

	public void DisposeServer()
	{
		LuaCsSetup.Instance.Hook.Remove("think", "ba_server_think");
	}

#if CLIENT
	public void InitClient()
	{
		INetworkingService networking = LuaCsSetup.Instance.Networking;
		if (networking != null && networking.IsActive)
		{
			networking.Receive("ba_atmos_sync", delegate(object[] args)
			{
				AtmosphereNetSync.ApplySnapshot((IReadMessage)args[0]);
			});
		}
		LuaCsSetup.Instance.Hook.Add("think", "ba_client_think", delegate(object[] args)
		{
			AtmosphereSim.UpdateClient();
			return null;
		});
		Log("Client side initialized.");
	}

	public void DisposeClient()
	{
		LuaCsSetup.Instance.Hook.Remove("think", "ba_client_think");
	}
#endif

	public void PreInitPatching()
	{
	}

	private static bool IsServer()
	{
		object networkMember = typeof(GameMain).GetProperty("NetworkMember", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
		if (networkMember == null) return false;
		return (bool)networkMember.GetType().GetProperty("IsServer").GetValue(networkMember);
	}

	private static bool IsClient()
	{
		object networkMember = typeof(GameMain).GetProperty("NetworkMember", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
		if (networkMember == null) return false;
		return (bool)networkMember.GetType().GetProperty("IsClient").GetValue(networkMember);
	}

	public void Initialize()
	{
		Instance = this;
		Log("Initializing v1.0.0 ...");
		bool isServer = IsServer();
		if (isServer || !GameMain.IsMultiplayer)
		{
			InitServer();
		}
#if CLIENT
		if (!isServer && GameMain.IsMultiplayer)
		{
			InitClient();
		}
#endif
		Harmony.Patch(
			original: AccessTools.Method(typeof(Hull), "get_OxygenPercentage"),
			postfix: new HarmonyMethod(AccessTools.Method(typeof(Patch_Hull), "OxygenPercentage_Postfix"))
		);

#if CLIENT
		Harmony.Patch(
			original: AccessTools.Method(typeof(MiniMap), "SetTooltip"),
			prefix: new HarmonyMethod(AccessTools.Method(typeof(Patch_MiniMap), "SetTooltip_Prefix"))
		);
		Harmony.Patch(
			original: AccessTools.Method(typeof(Submarine), "DrawFront"),
			prefix: new HarmonyMethod(AccessTools.Method(typeof(SmokeRenderer), "DrawFront_Prefix"))
		);
#endif
		Harmony.Patch((MethodBase)typeof(RepairTool).GetMethod("Use", new Type[2]
		{
			typeof(float),
			typeof(Character)
		}), (HarmonyMethod)null, new HarmonyMethod(typeof(Patch_RepairTool).GetMethod("Use_Postfix")), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Harmony.Patch((MethodBase)typeof(HumanAIController).GetMethod("CalculateHullSafety", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[8]
		{
			typeof(Hull),
			typeof(IEnumerable<Hull>),
			typeof(Character),
			typeof(bool),
			typeof(bool),
			typeof(bool),
			typeof(bool),
			typeof(bool)
		}, null), (HarmonyMethod)null, new HarmonyMethod(typeof(Patch_HumanAIController).GetMethod("CalculateHullSafety_Postfix")), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Harmony.Patch((MethodBase)typeof(SaveUtil).GetMethod("SaveGame", new Type[2]
		{
			typeof(CampaignDataPath),
			typeof(bool)
		}), (HarmonyMethod)null, new HarmonyMethod(typeof(Patch_SaveUtil).GetMethod("SaveGame_Postfix")), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Harmony.Patch((MethodBase)typeof(SaveUtil).GetMethod("LoadGame", new Type[1] { typeof(CampaignDataPath) }), (HarmonyMethod)null, new HarmonyMethod(typeof(Patch_SaveUtil).GetMethod("LoadGame_Postfix")), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Harmony.Patch((MethodBase)typeof(CharacterHealth).GetMethod("ReduceAfflictionOnAllLimbs", new Type[4]
		{
			typeof(Identifier),
			typeof(float),
			typeof(ActionType?),
			typeof(Character)
		}), (HarmonyMethod)null, new HarmonyMethod(typeof(Patch_CharacterHealth).GetMethod("ReduceAfflictionOnAllLimbs_Postfix")), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		FilterSlotInjector.Inject();
		Harmony.Patch((MethodBase)typeof(Item).GetMethod("OnMapLoaded", BindingFlags.Instance | BindingFlags.Public), (HarmonyMethod)null, new HarmonyMethod(typeof(FilterSlotInjector).GetMethod("Item_OnMapLoaded_Postfix")), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Log("Initialized.");
	}

	public void OnLoadCompleted()
	{
	}

	public void Dispose()
	{
		Log("Shutting down ...");
		bool isServer = IsServer();
		bool isClient = IsClient();
		
		if (isServer || (!isServer && !isClient))
		{
			DisposeServer();
		}
		if (isClient)
		{
#if CLIENT
			DisposeClient();
#endif
		}
		FilterSlotInjector.Remove();
		Harmony.UnpatchSelf();
		Instance = null;
		Log("Shut down.");
	}

	internal static void Log(string message)
	{
		LuaCsLogger.Log("[Bad Air] " + message);
	}
}

