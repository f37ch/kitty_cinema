using System.Linq;
using Sandbox.UI;
/// <summary>Game notices in the chat box, including the theater log.</summary>
public static class GameChat
{
	/// <summary>A local notice.</summary>
	public static void Info(string text,string icon="info")
	{
		if (string.IsNullOrWhiteSpace(text)) return;
		ChatBox()?.AddLocalText(text,icon);
	}

	/// <summary>Inside a broadcast RPC: shows the notice only to the player who made the call.</summary>
	public static void InfoForCaller(string text,string icon="info")
	{
		if (Rpc.Caller!=Connection.Local) return;
		Info(text,icon);
	}

	/// <summary>A theater log line, shown to whoever is in that theater.</summary>
	public static void Theater(string location,string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return;
		if (LocalLocation()!=location) return;
		Info(text,"video_settings");
	}

	/// <summary>Host: sends a theater log line to everyone, each client shows it if they are in that theater.</summary>
	[Rpc.Broadcast(NetFlags.HostOnly)]
	public static void TheaterBroadcast(string location,string text)
	{
		Theater(location,text);
	}

	private static Chat ChatBox()
	{
		var scene=Game.ActiveScene;
		if (scene==null) return null;
		return scene.Components.TryGet<Chat>(out var chat,FindMode.EnabledInSelfAndDescendants)?chat:null;
	}

	private static string LocalLocation()
	{
		return Game.ActiveScene?.GetAllComponents<TheaterPlayer>().FirstOrDefault(x=>!x.IsProxy)?.Location;
	}
}
