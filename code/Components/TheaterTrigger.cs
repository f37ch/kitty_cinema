
using Sandbox.UI;
public sealed class TheaterTrigger:Component,Component.ITriggerListener
{
	private MediaPlayer MediaPlayer { get; set; }
	[Property]private string LocationName { get; set; }
	int iTouching;
	void ITriggerListener.OnTriggerEnter(Collider other) 
	{
		iTouching++;
		if (other.GameObject.Root.Components.TryGet<TheaterPlayer>(out var Player)){
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