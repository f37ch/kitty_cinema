using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.Audio;
using Sandbox.UI;
/// <summary>
/// Local player settings. The saved file is the source of truth: values are kept here and applied to the
/// local camera and the audio mixers, never read back from the scene (another camera or a reloaded
/// scene would overwrite them).
/// </summary>
public class Settings
{
	[JsonIgnore] private const string Name="settings.json";
	[JsonIgnore] private static Settings data;
	[JsonIgnore] private static bool dirty;
	[JsonIgnore] private static RealTimeSince sinceChange;

	public float PlayerFov {get;set;}=85;
    public float FootStepVolume {get;set;}=1;
	public float OverAllVolume {get;set;}=1;
	public float SFXVolume {get;set;}=1;
	public float VoiceVolume {get;set;}=1;
	public float InterfaceVolume {get;set;}=1;

	/// <summary>The settings in use, loaded from disk the first time.</summary>
	public static Settings Data=>data??=Read();

	private static Settings Read()
	{
		try{
			if (FileSystem.Data.FileExists(Name)){
				var loaded=FileSystem.Data.ReadJson<Settings>(Name);
				if (loaded!=null) return loaded.Clamped();
			}
		}catch(Exception e){
			Log.Warning($"Settings file is broken, using defaults: {e.Message}");
		}
		return new Settings();
	}
	private Settings Clamped()
	{
		PlayerFov=Math.Clamp(PlayerFov,50,120);
		FootStepVolume=Math.Clamp(FootStepVolume,0,1);
		OverAllVolume=Math.Clamp(OverAllVolume,0,1);
		SFXVolume=Math.Clamp(SFXVolume,0,1);
		VoiceVolume=Math.Clamp(VoiceVolume,0,1);
		InterfaceVolume=Math.Clamp(InterfaceVolume,0,1);
		return this;
	}

	/// <summary>Applies the settings to the current scene: call after a scene loads.</summary>
	public static void Load()
	{
		Apply();
        Log.Info("Settings are loaded.");
	}
	/// <summary>Pushes the values to the local camera, footsteps and mixers.</summary>
	public static void Apply()
	{
		var s=Data;
		// The scene's main camera is the local player's view; other cameras (editor, other players) are left alone.
		var camera=Game.ActiveScene?.Camera;
		if (camera.IsValid()) camera.FieldOfView=s.PlayerFov;
        Footsteps.FootStepVolume=s.FootStepVolume;

		Mixer.Master.Volume=s.OverAllVolume;
		// By name: the order of the mixer's channels isn't fixed.
		SetMixer(Mixer.Default,s.SFXVolume);
		SetMixer(Mixer.Voice,s.VoiceVolume);
		SetMixer(FindChannel("ui"),s.InterfaceVolume);
	}
	private static void SetMixer(Mixer mixer,float volume)
	{
		if (mixer!=null) mixer.Volume=volume;
	}
	private static Mixer FindChannel(string name)
	{
		return Mixer.Master.GetChildren().FirstOrDefault(x=>string.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>Changes a setting, applies it and schedules a save.</summary>
	public static void Change(Action<Settings> change)
	{
		change(Data);
		Data.Clamped();
		Apply();
		dirty=true;
		sinceChange=0;
	}
	/// <summary>Saves a moment after the last change, so dragging a slider doesn't write the file every frame.</summary>
	public static void Tick()
	{
		if (dirty&&sinceChange>0.5f) Save();
	}
	/// <summary>Saves now if a change is still waiting for Tick.</summary>
	public static void Flush()
	{
		if (dirty) Save();
	}
	public static void Save()
	{
		dirty=false;
		FileSystem.Data.WriteJson(Name,Data);
		Log.Info("Settings saved.");
	}
}
