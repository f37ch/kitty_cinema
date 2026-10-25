using System.Linq;
using Sandbox.UI;
/// <summary>
/// Requests and controls from players. Every call runs on the host, which alone changes the player's
/// synced state; clients follow it (see MediaPlayer).
/// </summary>
public class MediaController:Component
{
    public const string WebHandlersURL="https://etsv.pw/mediaplayer/";
	[Rpc.Host]
    public static void BroadcastVideo(ParsedData Data, string VideoID, string ServiceName, string ServiceType, Guid PlayerID){
		var MediaPlayer=GetPlayer(PlayerID);
		if (MediaPlayer==null) return;
		var caller=Rpc.Caller;
		var vid=new Video(){
			videoid=VideoID,
			duration=Data.duration,
			title=string.IsNullOrEmpty(Data.title)?"No Title":Data.title,
			thumb=string.IsNullOrEmpty(Data.thumb)?"/ui/thumb.png":Data.thumb,
			service=ServiceType,
			requester=caller.DisplayName,
			servicename=ServiceName,
			uid=Guid.NewGuid().ToString("N"),
			requesterid=caller.SteamId
		};
		if (MediaPlayer.IsPlaying){
			// The playing video may be queued again, only the same video twice in the queue is refused.
			if (MediaPlayer.VideoList.Any(x=>x.videoid==VideoID)){
				Notify(caller,"Failed to request video: this video is already in the queue.");
				return;
			}
			MediaPlayer.VideoList.Add(vid);
			Notify(caller,"Your video was added to the queue.",true);
		}else{
			MediaPlayer.StartVideo(vid);
			ChatMsg(MediaPlayer,$"{caller.DisplayName} has requested video: [{ServiceName}] {vid.title}");
			Notify(caller,null,true);
		}
    }
	public static MediaPlayer GetPlayer(Guid id){
		return Game.ActiveScene?.Directory.FindByGuid(id)?.Components.Get<MediaPlayer>();
	}
	/// <summary>Host: sends a chat line to one player.</summary>
	private static void Notify(Connection target,string msg,bool requested=false){
		using (Rpc.FilterInclude(target)){
			NotifyLocal(msg,requested);
		}
	}
	[Rpc.Broadcast(NetFlags.HostOnly)]
	private static void NotifyLocal(string msg,bool requested){
		if (msg!=null) ChatMsgLocal(null,msg);
		if (requested) Sandbox.Services.Stats.Increment("videos",1);
	}
	public static void ChatMsgLocal(MediaPlayer MediaPlayer,string msg)//local msg
	{
		GameChat.Info(msg,"video_settings");
	}
	/// <summary>Host: a theater log line for everyone in that theater.</summary>
	public static void ChatMsg(MediaPlayer MediaPlayer,string msg)
	{
		if (!MediaPlayer.IsValid()) return;
		GameChat.TheaterBroadcast(MediaPlayer.Location,msg);
	}
	/// <summary>Local: the volume is per player, the page reloads at the current time with it.</summary>
    public static void SetVolume(Guid playerid,float volume)
	{
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer==null) return;
		volume=MathF.Round(Math.Clamp(volume,0,100));
		if (MediaPlayer.Volume==volume) return;
        MediaPlayer.Volume=volume;
	}
	public static int CountSkips(Guid playerid){
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer==null) return 1;
		var numplayers_in_theater=Game.ActiveScene.GetAllComponents<TheaterPlayer>().Count(x=>x.Location==MediaPlayer.Location);
		if (numplayers_in_theater<=1){
			return 1;
		}else if(numplayers_in_theater<3){
			return 2;
		}else{
			return (int)Math.Round(numplayers_in_theater*.66f);
		}
	}
	[Rpc.Host]
    public static void VoteSkip(Guid playerid)
	{
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer==null) return;
		var caller=Rpc.Caller;
		if (!MediaPlayer.IsPlaying){Notify(caller,"Failed to vote skip video: No video is playing.");return;}
		if (MediaPlayer.SkipList.Any(x=>x.steamid==caller.SteamId)){
			Notify(caller,"Failed to vote skip video: You already voted.");
		    return;
        }
        MediaPlayer.SkipList.Add(new (){
            steamid=caller.SteamId,
		});
		ChatMsg(MediaPlayer,$"{caller.DisplayName} has voted to skip this video.");
	}
	/// <summary>Like or unlike a queued video: the most liked plays next.</summary>
	[Rpc.Host]
	public static void ToggleLike(Guid playerid,string key)
	{
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer==null||!MediaPlayer.VideoList.Any(x=>MediaPlayer.QueueKey(x)==key)) return;
		var caller=Rpc.Caller;
		for (int i=0;i<MediaPlayer.LikeList.Count;i++){
			if (MediaPlayer.LikeList[i].uid==key&&MediaPlayer.LikeList[i].steamid==caller.SteamId){
				MediaPlayer.LikeList.RemoveAt(i);
				return;
			}
		}
		MediaPlayer.LikeList.Add(new QueueLike{uid=key,steamid=caller.SteamId});
	}
	/// <summary>The host, or whoever requested it, takes a video out of the queue.</summary>
	[Rpc.Host]
	public static void RemoveQueued(Guid playerid,string key)
	{
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer==null) return;
		var caller=Rpc.Caller;
		var vid=MediaPlayer.VideoList.FirstOrDefault(x=>MediaPlayer.QueueKey(x)==key);
		if (vid.videoid==null) return;
		if (!caller.IsHost&&vid.requesterid!=caller.SteamId){
			Notify(caller,"Only the host or the requester can remove a video from the queue.");
			return;
		}
		if (!MediaPlayer.RemoveFromQueue(key)) return;
		ChatMsg(MediaPlayer,$"{caller.DisplayName} removed \"{vid.title}\" from the queue.");
	}
	/// <summary>Host only: pause, skip and seek.</summary>
	private static bool CallerMayControl(){
		if (Rpc.Caller.IsHost) return true;
		Notify(Rpc.Caller,"Only the host can control playback.");
		return false;
	}
    [Rpc.Host]
    public static void Skip(Guid playerid)
	{
		if (!CallerMayControl()) return;
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer==null||!MediaPlayer.IsPlaying) return;
		ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} skipped current video.");
		MediaPlayer.PlayNext();
	}
    [Rpc.Host]
    public static void TogglePlay(Guid playerid)
	{
		if (!CallerMayControl()) return;
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer==null||!MediaPlayer.IsPlaying) return;
		MediaPlayer.Paused=!MediaPlayer.Paused;
		ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} {(MediaPlayer.Paused?"paused":"resumed")} current video.");
	}
    [Rpc.Host]
    public static void Seek(Guid playerid,float time)
	{
		if (!CallerMayControl()) return;
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer==null||!MediaPlayer.IsPlaying||MediaPlayer.IsLive) return;
		MediaPlayer.SeekTo(time);
		var t=TimeSpan.FromSeconds(MediaPlayer.Curtime);
		ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} rewound the video to {(t.Hours>0?$"{t.Hours}:":"")}{t.Minutes:D2}:{t.Seconds:D2}.");
	}
}
