using Barotrauma;

namespace BadAir;

public static class Patch_SaveUtil
{
	public static void SaveGame_Postfix(CampaignDataPath filePath, bool isSavingOnLoading)
	{
		if (!isSavingOnLoading)
		{
			AtmospherePersistence.Save(filePath.SavePath);
		}
	}

	public static void LoadGame_Postfix(CampaignDataPath path)
	{
		AtmospherePersistence.NotifySaveLoaded(path.LoadPath);
	}
}

