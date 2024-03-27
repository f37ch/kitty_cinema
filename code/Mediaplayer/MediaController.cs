using System.Linq;
public class MediaController:Component
{
    public const string WebHandlersURL="https://etsv.pw/mediaplayer/";
	[Broadcast]
    public static void BroadcastVideo(ParsedData Data, string VideoID, string ServiceName, string ServiceType, Guid PlayerID){
		var MediaPlayer=GetPlayer(PlayerID);
		if (MediaPlayer.Service!=null){
			foreach (var video in MediaPlayer.VideoList){
			    if (video.videoid == VideoID){
                    ChatMsg(MediaPlayer,$"Failed to requested video: Current video already listed in queue.",true);
			        return;
                }
            }
            MediaPlayer.VideoList.Add(new (){
                videoid=VideoID,
                duration=Data.duration,
                title=Data.title!=null?Data.title:"No Title",
                thumb=Data.thumb!=null?Data.thumb:"/ui/thumb.png",
                service=ServiceType,
                requester=Rpc.Caller.DisplayName,
                servicename=ServiceName
            });
			if(MediaPlayer.PanelComponent.Components.TryGet<Queue>(out var queue)){
				queue.StateHasChanged();
			}
			ChatMsg(MediaPlayer,$"Your video was added to queue.",true);
			if (Rpc.Caller==Connection.Local){
				Sandbox.Services.Stats.Increment("videos",1);
			}
		}else{
	    	MediaPlayer.Service=ServiceType;MediaPlayer.Duration=Data.duration;MediaPlayer.ContentID=VideoID;MediaPlayer.Title=Data.title!=null?Data.title:"";
	    	MediaPlayer.IsLive=MediaPlayer.Duration<=0;
	    	MediaPlayer.Curtime=MediaPlayer.Duration>0?1:0;
			MediaPlayer.Thumbnail=Data.thumb;
			ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} has requested video: [{ServiceName}] {MediaPlayer.Title}");
			if (Rpc.Caller==Connection.Local){
				Sandbox.Services.Stats.Increment("videos",1);
			}
		}
    }
	public static MediaPlayer GetPlayer(Guid id){
		return Game.ActiveScene.Directory.FindByGuid(id).Components.Get<MediaPlayer>();
	}
	public static void ChatMsg(MediaPlayer MediaPlayer,string msg,bool islocal=false)
	{
		if (Game.ActiveScene.Components.TryGet<Chat>(out var Chat,FindMode.EnabledInSelfAndDescendants)){
			var da=Game.ActiveScene.GetAllComponents<TheaterPlayer>().FirstOrDefault(x=>x.GameObject.Network.OwnerConnection==Connection.Local);
			if (da==null) return;
			if (islocal){//local player Log.Info(Connection.Local.Id==Rpc.Caller.Id);
				Chat.AddLocalText(msg,"video_settings",true);
			}else{//only players in theater
				if (da.Location==MediaPlayer.Location){
					Chat.AddLocalText(msg,"video_settings");
				}
			}
		}
	}
    public static void SetVolume(Guid playerid,float volume)
	{
		var MediaPlayer=GetPlayer(playerid);
        MediaPlayer.Volume=volume;
		ChatMsg(MediaPlayer,$"Your volume set to {volume}%.",true);
		if (!MediaPlayer.Paused){
			MediaPlayer.WebPanel.Surface.Url=$"{WebHandlersURL}player.php?tp={MediaPlayer.Service}&dt={MediaPlayer.ContentID}&st={MediaPlayer.Curtime}&vol={MediaPlayer.Volume}";
			//MediaPlayer.Trigger=Time.Now+1;
		}
	}
	public static int CountSkips(Guid playerid){
		var MediaPlayer=GetPlayer(playerid);
		var numplayers_in_theater=Game.ActiveScene.GetAllComponents<TheaterPlayer>().Where(x=>x.Location==MediaPlayer.Location).Count();
		if (numplayers_in_theater==1||numplayers_in_theater==0){
			return 1;
		}else if(numplayers_in_theater>0 && numplayers_in_theater<3){
			return 2;
		}else{
			return (int)Math.Round(numplayers_in_theater*.66f);
		}
	}
	[Broadcast]
    public static void VoteSkip(Guid playerid)
	{
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer.Service==null){ChatMsg(MediaPlayer,$"Failed to vote skip video: No video is playing.",true);return;}
		foreach (var vote in MediaPlayer.SkipList){
		    if (vote.steamid == Rpc.Caller.SteamId){
                ChatMsg(MediaPlayer,$"Failed to vote skip video: You already voted.",true);
		        return;
            }
        }
        MediaPlayer.SkipList.Add(new (){
            steamid=Rpc.Caller.SteamId,
		});
		ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} has voted to skip this video.");
	}
    [Broadcast (NetPermission.HostOnly)]
    public static void Skip(Guid playerid)
	{
		if (!Rpc.Caller.IsHost) return;
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer.Service==null){return;}//||MediaPlayer.Paused?
        MediaPlayer.Curtime=0;
		MediaPlayer.Duration=0;
		MediaPlayer.Service=null;
		ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} skipped current video.");
	}
    [Broadcast (NetPermission.HostOnly)]
    public static void TogglePlay(Guid playerid)
	{
		if (!Rpc.Caller.IsHost) return;
			var MediaPlayer=GetPlayer(playerid);
		
		if (MediaPlayer.Service==null){return;}
        	
		if (MediaPlayer.Paused){
          	MediaPlayer.Paused=false;
		   	ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} resumed current video.");
        }else{
			MediaPlayer.Paused=true;
			ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} paused current video.");
        }
		
	}
    [Broadcast (NetPermission.HostOnly)]
    public static void Seek(Guid playerid,float time)
	{
		if (!Rpc.Caller.IsHost) return;
		var MediaPlayer=GetPlayer(playerid);
		if (MediaPlayer.Service==null||MediaPlayer.Paused||MediaPlayer.IsLive){return;}
        MediaPlayer.Curtime=time;
		MediaPlayer.WebPanel.Surface.Url=$"{WebHandlersURL}player.php?tp={MediaPlayer.Service}&st={MediaPlayer.Curtime}&dt={MediaPlayer.ContentID}&vol={MediaPlayer.Volume}";
		//MediaPlayer.Trigger=Time.Now+1;
		ChatMsg(MediaPlayer,$"{Rpc.Caller.DisplayName} seeked current video.");
	}
}