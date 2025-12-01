using System.Text.Json.Serialization;
using Sandbox.Audio;
using Sandbox.UI;
public class Settings
{
	[JsonIgnore] private const string Name="settings.json";
	[JsonIgnore] private static Settings settings=null;
	public float PlayerFov {get;set;}=85;
    public float FootStepVolume {get;set;}=1;
	public float OverAllVolume {get;set;}=1;
	public float SFXVolume {get;set;}=1;
	public float VoiceVolume {get;set;}=1;
	public float InterfaceVolume {get;set;}=1;
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

		Mixer.Master.Volume=settings.OverAllVolume;
		var channel=Mixer.Master.GetChildren();
		channel[0].Volume=settings.SFXVolume;
		channel[2].Volume=settings.InterfaceVolume;
		channel[1].Volume=settings.VoiceVolume;
        Log.Info("Settings are loaded.");
	}
	public static Settings Current()
	{
        Game.ActiveScene.Components.TryGet<CameraComponent>(out var Camera, FindMode.EnabledInSelfAndDescendants);
		Game.ActiveScene.Components.TryGet<GameMenu>(out var Menu, FindMode.EnabledInSelfAndDescendants);
		var channel=Mixer.Master.GetChildren();
		return new Settings
		{
			PlayerFov=Camera.FieldOfView,
            FootStepVolume=Footsteps.FootStepVolume,
			PrivateLobby=Menu.IsLobbyPrivate,
			IsLobbyNeedPassword=Menu.IsLobbyNeedPassword,
			LobbyPassword=Menu.LobbyPassword,
			OverAllVolume=Mixer.Master.Volume,
			SFXVolume=channel[0].Volume,
			InterfaceVolume=channel[2].Volume,
			VoiceVolume=channel[1].Volume
		};
	}
	public static void Save()
	{
		FileSystem.Data.WriteJson(Name,Current());
		Log.Info("Settings saved.");
	}
}