using System.Text.Json.Serialization;
public class Settings
{
	[JsonIgnore] private const string Name="settings.json";
	[JsonIgnore] private static Settings settings=null;
	public float PlayerFov {get;set;}
    public float FootStepVolume {get;set;}
	public bool PrivateLobby {get;set;}
	public string LobbyPassword {get;set;}
	public bool IsLobbyNeedPassword {get;set;}
	public static void Load()
	{
		if (FileSystem.Data.FileExists(Name))
		{
			settings=FileSystem.Data.ReadJson<Settings>(Name);
		}else{
			settings=Current();
		}
		Game.ActiveScene.Components.TryGet<CameraComponent>(out var Camera, FindMode.EnabledInSelfAndDescendants);
		Game.ActiveScene.Components.TryGet<GameMenu>(out var Menu, FindMode.EnabledInSelfAndDescendants);
		Camera.FieldOfView=settings.PlayerFov;
        Footsteps.FootStepVolume=settings.FootStepVolume;
		Menu.IsLobbyPrivate=settings.PrivateLobby;
		Menu.IsLobbyNeedPassword=settings.IsLobbyNeedPassword;
		Menu.LobbyPassword=settings.LobbyPassword;
        Log.Info("Settings are loaded.");
	}
	public static Settings Current()
	{
        Game.ActiveScene.Components.TryGet<CameraComponent>(out var Camera, FindMode.EnabledInSelfAndDescendants);
		Game.ActiveScene.Components.TryGet<GameMenu>(out var Menu, FindMode.EnabledInSelfAndDescendants);
		return new Settings
		{
			PlayerFov=Camera.FieldOfView,
            FootStepVolume=Footsteps.FootStepVolume,
			PrivateLobby=Menu.IsLobbyPrivate,
			IsLobbyNeedPassword=Menu.IsLobbyNeedPassword,
			LobbyPassword=Menu.LobbyPassword
		};
	}
	public static void Save()
	{
		FileSystem.Data.WriteJson(Name,Current());
	}
}