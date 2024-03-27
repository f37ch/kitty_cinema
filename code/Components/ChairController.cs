public struct ChairPlace
{
	public Guid user { get; set; }
	public Vector3 eyepos { get; set; }
	public Vector3 sitpos { get; set; }
}
public sealed class ChairController : Component
{
	[Property] public string  ChairInfo { get; set; }
	[Property] public string  ChairChatInfo { get; set; }
	[HostSync] public NetList<ChairPlace> UsingList {get;set;}=new();
	private Chat Chat {get; set;}
	public InfoPopup Popup {get;set;}
	
	protected override void OnUpdate()
	{
		base.OnUpdate();
	
	}
	[Broadcast]
	public void Sit(Guid Userid)
	{
		foreach (var Place in UsingList){
			if (Place.user==default){
				if (Scene.Directory.FindByGuid(Userid).Components.TryGet<TheaterPlayer>(out var User)){
					UsingList.Add(new (){user=User.GameObject.Id,eyepos=Place.eyepos,sitpos=Place.sitpos});
					User.GameObject.SetParent(GameObject,true);
					
					if (Connection.Local==User.Network.OwnerConnection){
						Chat.AddLocalText(ChairChatInfo,"info");
					}
					UsingList.Remove(Place);
				}
				break;
			}
		}
	}
	[Broadcast]
	public void Leave(Guid Userid)
	{
		foreach (var Place in UsingList){
			if (Place.user==Userid){
				if (Scene.Directory.FindByGuid(Userid).Components.TryGet<TheaterPlayer>(out var User)){
					UsingList.Add(new (){user=default,eyepos=Place.eyepos,sitpos=Place.sitpos});
					UsingList.Remove(Place);
					User.GameObject.SetParent(null,true);
					User.Transform.Position=Transform.Local.PointToWorld(Place.sitpos.WithX(80));
					User.EyeAngles=User.EyeAngles.WithYaw(Transform.LocalRotation.Yaw());
					
				}
				break;
			}
		}
	}
	protected override void OnStart()
	{
		
		Flags=ComponentFlags.None;
		var Pos=new Vector3(2,44,49);
		UsingList.Add(new (){user=default,eyepos=Pos,sitpos=new Vector3(8,44,4)});
		Pos=new Vector3(2,-44,49);
		UsingList.Add(new (){user=default,eyepos=Pos,sitpos=new Vector3(8,-44,4)});
		Pos=new Vector3(2,0,49);
		UsingList.Add(new (){user=default,eyepos=Pos,sitpos=new Vector3(8,0,4)});
		Popup=Scene.Components.GetInDescendantsOrSelf<InfoPopup>();
		Chat=Scene.Components.GetInDescendantsOrSelf<Chat>();
	}
}