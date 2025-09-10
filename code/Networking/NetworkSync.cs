using Sandbox.Network;
using System.Threading.Tasks;
using System.Linq;
using Sandbox.UI;
//using System.Data;
//using System.Threading.Channels;
[Title("Network Sync")]
[Category("Networking")]
[Icon("electrical_services")]
public sealed class NetworkSync : Component, Component.INetworkListener
{
	/// <summary>
	/// Create a server (if we're not joining one)
	/// </summary>
	[Property] public bool StartServer {get;set;}=true;

	/// <summary>
	/// The prefab to spawn for the player to control.
	/// </summary>
	[Property] public GameObject PlayerPrefab {get;set;}

	/// <summary>
	/// A list of points to choose from randomly to spawn the player in. If not set, we'll spawn at the
	/// location of the NetworkHelper object.
	/// </summary>
	[Property] public List<GameObject> SpawnPoints {get;set;}
	//[Sync] public NetDictionary<Guid,Guid> Players {get;set;}
	private static Chat Chat;
	private bool PrivateLobby {get;set;}
	//private bool LobbyNeedPassword {get;set;}
	//private string LobbyPassword {get;set;}
	
	protected override async Task OnLoad()
	{
		if (Scene.IsEditor)
			return;

		if (StartServer&&!Networking.IsActive)
		{
			LoadingScreen.Title="Creating Lobby";
			
			Settings.Load();
			var st=Settings.Current();
			PrivateLobby=st.PrivateLobby;
			Networking.CreateLobby(new LobbyConfig(){
				Privacy=PrivateLobby?LobbyPrivacy.FriendsOnly:LobbyPrivacy.Public,
			});
			//LobbyNeedPassword=st.IsLobbyNeedPassword;
			//LobbyPassword=st.LobbyPassword;
		}
		await Task.CompletedTask;
	}
    public static void Disconnect(string text, string icon)
	{
		Networking.Disconnect();
		var init_scene=ResourceLibrary.Get<SceneFile>("scenes/init.scene");
		Game.ActiveScene.Load(init_scene);
		var gmenu=Game.ActiveScene.GetAllComponents<GameMenu>().FirstOrDefault();
		gmenu.DisplayText=text;
		gmenu.DisplayIcon=icon;
        gmenu.selected="displaytext";
		gmenu.selectedB="";
		Log.Info(text);
	}
	[Rpc.Broadcast(NetFlags.HostOnly)]
	public static void Kick(Guid id)
	{
		if (!Rpc.Caller.IsHost) return;
		var player=Connection.All.First(x=>x.Id==id);
		var kicked=false;
		if (Rpc.Caller.Id==player.Id){
			Chat.AddLocalText("You can not kick yourself.","terminal",true);
			return;
		}else if(id==Connection.Local.Id){
			Disconnect("KICKED BY HOST","link_off");
			kicked=true;
		}
		if(kicked){
			Chat.AddLocalText(player.DisplayName+" was kicked by host.","terminal");
		}
		Game.ActiveScene.Directory.FindByGuid(id)?.Destroy();
	}
	public void OnDisconnected(Connection conn){
		var playerobj=Game.ActiveScene?.GetAllComponents<TheaterPlayer>()?.FirstOrDefault(x=>x.GameObject.Network.Owner.Id==conn.Id);
		if (playerobj!=null){
			playerobj?.Destroy();
		}
	}
	//public async Task Deleayed()
	//{
	//	await Task.DelayRealtimeSeconds(.4f);
	//	ReceivePassword(Game.ActiveScene.Components.Get<GameMenu>().LobbyPassword);
	//
	//}
	//[Broadcast (NetPermission.HostOnly)]
	//public static void HandlePassword(Guid id,string type=null)
	//{
	//	if (!Rpc.Caller.IsHost) return;
	//	if(id==Connection.Local.Id){
	//		if (type=="invalid"){
	//			Disconnect("WRONG PASSWORD","no_encryption");
	//		}else if (type=="ok"){
	//			var passwordEntry=Game.ActiveScene.Components.GetAll<ScreenPanel>().Where(x=>x.ZIndex==45).First();//meeh, im lazy
	//			passwordEntry.Destroy();
	//			return;
	//		}else if(type==null){
	//			var network=Game.ActiveScene.Components.Get<NetworkSync>(FindMode.EnabledInSelfAndDescendants);
	//			network.Deleayed();
	//		}
	//	}
	//}
	//[Rpc.Broadcast]
	//public void ReceivePassword(string password)
	//{
	//	if (IsProxy) return;
	//	Log.Info(LobbyPassword);
	//	if (Game.ActiveScene.GetAllComponents<TheaterPlayer>().FirstOrDefault(x=>x.GameObject.Network.OwnerConnection==Rpc.Caller)==default){
	//		
	//		if (LobbyPassword!=password){
	//			HandlePassword(Rpc.Caller.Id,"invalid");
	//			
	//			Chat.AddSystemText($"{Rpc.Caller.Id} failed to join your lobby: worng password ({password}).","terminal");
	//			return;
	//		}else{
	//		
	//			InitPlayer(Rpc.Caller);
	//			HandlePassword(Rpc.Caller.Id,"ok");
	//		}
	//	}
	//}
	/// <summary>
	/// A client is fully connected to the server. This is called on the host.
	/// </summary>
	public void InitPlayer(Connection channel){
		Log.Info( $"Player '{channel.DisplayName}' has joined the game" );
		if (PlayerPrefab is null)
			return;
		//
		// Find a spawn location for this player
		//
		var startLocation=FindSpawnLocation().WithScale(1);

		// Spawn this object and make the client the owner
		var player=PlayerPrefab.Clone(startLocation,name:$"Player - {channel.DisplayName}");
		
		foreach (var entry in Connection.All)
        {
			if (entry.SteamId==channel.SteamId){
				channel=entry;
			}
		}
		
		if (!player.Network.Active){
			player.NetworkSpawn(channel);
		}else{
			player.Network.AssignOwnership(channel);
		}

		//if (Players==null){Players=new();}
		//if (!Players.ContainsKey(channel.Id))
		//{
		//	Players.Add(channel.Id,player.Id);
		//}
	}
	public async void OnActive(Connection channel)
	{
		//if (LobbyNeedPassword){
		//	HandlePassword(channel.Id);
		//}else{
			InitPlayer(channel);
			await Task.DelayRealtimeSeconds(.4f);
			if (PrivateLobby){
				Chat.AddLocalText("#notify.friendonly","info",true);
				Chat.AddLocalText("#notify.friendonly2","info",true);
			}
		//}
	}

	/// <summary>
	/// Find the most appropriate place to respawn
	/// </summary>
	Transform FindSpawnLocation()
	{
		//
		// If they have spawn point set then use those
		//
		if ( SpawnPoints is not null && SpawnPoints.Count > 0 )
		{
			return Random.Shared.FromList( SpawnPoints, default ).Transform.World;
		}

		//
		// If we have any SpawnPoint components in the scene, then use those
		//
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();
		if ( spawnPoints.Length > 0 )
		{
			return Random.Shared.FromArray( spawnPoints ).Transform.World;
		}

		//
		// Failing that, spawn where we are
		//
		return Transform.World;
	}
	protected override void OnStart()
	{
		base.OnStart();
		Chat=Scene.Components.GetInDescendantsOrSelf<Chat>();
	}
}