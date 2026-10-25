
using Sandbox.UI;
public sealed class TheaterTrigger:Component,Component.ITriggerListener
{
	private MediaPlayer MediaPlayer { get; set; }
	[Property]private string LocationName { get; set; }
	int iTouching;
	/// <summary>Colliders of each player touching the trigger: a player has several, the log wants one line.</summary>
	private readonly Dictionary<Guid,int> PlayerTouches=new();

	/// <summary>Theater log: who comes in and goes out, for the others in this theater.</summary>
	private void LogPresence(TheaterPlayer player,int change)
	{
		var id=player.GameObject.Id;
		PlayerTouches.TryGetValue(id,out var count);
		var now=Math.Max(0,count+change);
		if (now==0) PlayerTouches.Remove(id); else PlayerTouches[id]=now;
		if (MediaPlayer==null||player.IsProxy==false) return;
		var name=player.GameObject.Network.Owner?.DisplayName??"Someone";
		if (count==0&&now>0) GameChat.Theater(LocationName,$"{name} came in.");
		else if (count>0&&now==0) GameChat.Theater(LocationName,$"{name} left.");
	}
	void ITriggerListener.OnTriggerEnter(Collider other)
	{
		iTouching++;
		if (other.GameObject.Root.Components.TryGet<TheaterPlayer>(out var Player)){
			LogPresence(Player,1);
			//if (Player.Location!=LocationName){
				Player.Location=LocationName;
				if (MediaPlayer!=null&&!IsProxy){
					if(!MediaPlayer.PanelComponent.Components.TryGet<Queue>(out var queue)){
						if (Player.GameObject.Network.Owner.Id==Connection.Local.Id){
							queue=MediaPlayer.PanelComponent.Components.Create<Queue>();
							queue.MediaPlayer=MediaPlayer;
						}
					}
					if(!MediaPlayer.PanelComponent.Components.TryGet<TheaterControls>(out var controls)){
						if (Player.GameObject.Network.Owner.Id==Connection.Local.Id){
							controls=MediaPlayer.PanelComponent.Components.Create<TheaterControls>();
							controls.MediaPlayer=MediaPlayer;
						}
					}
				}
			//}
		}
		
		if (other.GameObject.Root.Components.TryGet<MediaPlayer>(out var MP)){
			MediaPlayer=MP;
			MediaPlayer.Location=LocationName;
		}
		//NameTag.Name = $"{iTouching} touching\n{other.GameObject.Name} entered";
	}
	void ITriggerListener.OnTriggerExit(Collider other) 
	{
		iTouching--;
		if (other.GameObject.Root.Components.TryGet<TheaterPlayer>(out var Player)){
			LogPresence(Player,-1);
			if (Player.GameObject.Network.Owner.Id==Connection.Local.Id){
				if (MediaPlayer!=null&&!IsProxy){
					if(MediaPlayer.PanelComponent.Components.TryGet<Queue>(out var queue)){
						queue.Destroy();
					}
					if(MediaPlayer.PanelComponent.Components.TryGet<TheaterControls>(out var controls)){
						controls.Destroy();
					}
				}
			}
			//Player.Location="Unknown";
		}
		if (MediaPlayer==null) return;
		if (other.GameObject.Root.Name.Contains("MediaPlayer")){
			MediaPlayer.Location=null;
			MediaPlayer=null;
		}
		//NameTag.Name = $"{iTouching} touching\n{other.GameObject.Name} left";
	}
}