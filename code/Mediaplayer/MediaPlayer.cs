using System.Linq;
using Sandbox.UI;
using System.Threading.Tasks;
public struct Video
{
	public string videoid { get; set; }
	public float duration { get; set; }
	public string title { get; set; }
	public string thumb { get; set; }
	public string service { get; set; }
	public string requester { get; set; }
	public string servicename { get; set; }
}
public struct Skips
{
	public ulong steamid { get; set; }
}
public class MediaPlayer : Component
{
	[Property]private ModelRenderer ScreenModel { get; set; }
	[Property]public GameObject PanelComponent { get; set; }
	[Property]private SpotLight Projector { get; set; }
	private Texture ProjectionTexture {get;set;}
	private WorldPanel WorldUI { get; set; }
    public WebPanel WebPanel {get;set;}
	private ScreenUI ScreenUI {get;set;}
	private Pointer Pointer {get;set;}
	private ScreenInfo ScreenInfo {get;set;}
	public Sandbox.UI.WorldInput WorldInput {get;set;}
	[HostSync] public float RequestCD {get;set;}=0;
	[HostSync] public float Duration{get;set;}
	[HostSync] public float Curtime{get;set;}
	[HostSync] public string Service{get;set;}
	[HostSync] public string ContentID{get;set;}
	[HostSync] public bool Paused {get;set;}
	[HostSync] public bool IsLive {get;set;}
	[HostSync] public string Title {get;set;}
	[HostSync] public string Location{get;set;}
	[HostSync] public string Thumbnail{get;set;}
	[HostSync] public NetList<Video> VideoList {get;set;}=new();
	[HostSync] public NetList<Skips> SkipList {get;set;}=new();
	public float Volume{get;set;}=40;
	private bool LocalInside {get;set;}
	private bool MouseToggle {get;set;}
	private RealTimeSince NextThink;
	private static float boundy,boundz;
	/// <summary>
	///  Automate WorldPanel Bounds and size!
	///  Fixes shit with with disappearance.
	///  Calculates ray to screen.
	/// </summary>
	private void WorldPanelLogicUpdate(){

		var rot=ScreenModel.Transform.Rotation;
		if (WorldUI==null) return;
		WorldUI.Transform=Transform.Local.WithScale(10).WithPosition(ScreenModel.Transform.Position+rot.Backward*0.7F+rot.Up*ScreenModel.Transform.Scale.z*boundz/2).WithRotation(Rotation.LookAt(rot.Backward,rot.Up));
		boundy=ScreenModel.Model.Bounds.Size.y-16;
		boundz=ScreenModel.Model.Bounds.Size.z-8;
		WorldUI.PanelBounds=new Rect(-Transform.Scale.y*boundy,-Transform.Scale.z*boundz,Transform.Scale.y*boundy*2,Transform.Scale.z*(boundz*2-16));
		if (Scene.Camera is null) {return;}
		var ray=Scene.Camera.ScreenPixelToRay(Screen.Size/2);
		WorldInput.Ray=ray;
		WorldInput.MouseLeftPressed=Input.Down("use");
		if (WorldInput.Hovered!=null)
    	{
			if (Pointer.HasClass("Hide")){
				Pointer.RemoveClass("Hide");
			}
			Pointer.Style.Top=(WorldUI.PanelBounds.Top/2.2f)+WorldUI.MousePosActual.y/2;
      		Pointer.Style.Left=WorldUI.MousePosActual.x/2;
			
    	}else{
			if (!Pointer.HasClass("Hide")){
				Pointer.AddClass("Hide");
			}
		}
		var me=Scene.GetAllComponents<TheaterPlayer>().FirstOrDefault(x=>x?.GameObject?.Network.OwnerConnection==Connection.Local);
		if (me!=null&&me.Location==Location){
			if (ScreenUI!=null&&ScreenUI.HasClass("Notinside")){
				ScreenUI.RemoveClass("Notinside");
			}
			if(Service==null){
				if (ScreenUI==null){
					HomeScreen();
				}
			}else{
				if (ScreenInfo==null){
					PlaybackScreen();
				}
			}
			//if(Trigger>Time.Now){
			MouseToggle=!MouseToggle;
			WebPanel?.Surface?.TellMouseButton(MouseButtons.Left,MouseToggle);
			//WebPanel?.Surface?.TellMouseButton(MouseButtons.Left,false);// it needs for autoplay and sound working on load
				//WebPanel.Surface.TellMouseButton(MouseButtons.Left,false);
			//}
			HandlePause();
			LocalInside=true;
		}else{
			if (ScreenUI==null){
				HomeScreen();
			}else{
				if (!ScreenUI.HasClass("Notinside")){
					ScreenUI.AddClass("Notinside");
				}
			}
			LocalInside=false;
		}
		HandleVote();
		_=HandleQue(LocalInside);
	}
	protected override void OnUpdate(){
		if (!Paused&&Curtime>0&&NextThink>1&&!IsLive){
        	Curtime=+Math.Clamp(Curtime+1,0,Duration);
        	NextThink=0;
        }
		WorldPanelLogicUpdate();
    }
	public async Task HandleQue(bool isinside){
		if (Service==null||(Duration==Curtime&&!IsLive)){
			StopPlayBack();
			HomeScreen();
			if (VideoList.Count>0&&WebPanel.Surface.Url==$"{MediaController.WebHandlersURL}player.php?tp=dl"){
				var vid=VideoList[0];
				Service=vid.service;Duration=vid.duration;ContentID=vid.videoid;Title=vid.title;
				IsLive=Duration<=0;
				Curtime=Duration>0?1:0;
				Thumbnail=vid.thumb;
				VideoList.Remove(vid);
				if (isinside){
					if(PanelComponent.Components.TryGet<Queue>(out var queue)){
						queue.StateHasChanged();
					}
					if (Scene.Components.TryGet<Chat>(out var Chat,FindMode.EnabledInSelfAndDescendants)){
						Chat.AddLocalText($"[QUE] {vid.requester} has requested video: [{vid.servicename}] {vid.title}","video_settings");
					}
					await Task.DelayRealtimeSeconds(.8f);
					PlaybackScreen(true);
				}
			}
		}
	}
	private void StopPlayBack(){
		IsLive=default;
		Service=null;
		Thumbnail=null;
		Title=null;
		Paused=default;
		Duration=0;
		Curtime=0;
		SkipList.Clear();
	}
	private void HandleVote(){
		if((SkipList.Count>0)&&SkipList.Count>=MediaController.CountSkips(GameObject.Id)){
			StopPlayBack();
			MediaController.ChatMsg(this,$"Video was skipped by vote.");
		}
	}
	private void HomeScreen(){
		if (ScreenUI!=null){return;}
		ScreenInfo?.Delete();
		ScreenInfo=null;
		ScreenUI=WebPanel.AddChild<ScreenUI>();
		WebPanel.Surface.Url=$"{MediaController.WebHandlersURL}player.php?tp=dl";
		ScreenUI.MediaPlayer=this;
		ScreenUI.AcceptsFocus=false;
		Pointer=ScreenUI.AddChild<Pointer>();
	}
	private void PlaybackScreen(bool forseplay=false){
		
		if (ScreenInfo!=null){return;}
		ScreenUI.Delete();
		ScreenUI=null;
		ScreenInfo=WebPanel.AddChild<ScreenInfo>();
		if (!forseplay&&Paused){
			ScreenInfo.AddClass("Paused");
			WebPanel.Surface.Url=$"{MediaController.WebHandlersURL}player.php?tp=n";
		}else{
			ScreenInfo.RemoveClass("Paused");
			//Trigger=Time.Now+1;
			WebPanel.Surface.Url=$"{MediaController.WebHandlersURL}player.php?tp={Service}&dt={ContentID}&vol={Volume}&st={Curtime}";
		}
		ScreenInfo.MediaPlayer=this;
	}
	private void HandlePause(){
		if (ScreenInfo==null) return;
		if (Paused&&!ScreenInfo.HasClass("Paused")){
			ScreenInfo.AddClass("Paused");
			WebPanel.Surface.Url=$"{MediaController.WebHandlersURL}player.php?tp=n";
		}else if (!Paused&&ScreenInfo.HasClass("Paused")){
			ScreenInfo.RemoveClass("Paused");
			//Trigger=Time.Now+1;
			WebPanel.Surface.Url=$"{MediaController.WebHandlersURL}player.php?tp={Service}&dt={ContentID}&vol={Volume}&st={Curtime}";
		}
	}
	protected override void OnStart(){
		//Flags=ComponentFlags.None;
		WorldUI=new WorldPanel(Scene.SceneWorld)
		{
			MaxInteractionDistance=800
		};
		WorldInput=new Sandbox.UI.WorldInput(){
			Enabled=true
		};
		
		WebPanel=WorldUI.AddChild<WebPanel>();
		WebPanel.Style.Width=Length.Percent(100);
		WebPanel.Style.Height=Length.Percent(100);
		WebPanel.Style.PointerEvents=PointerEvents.All;
		WebPanel.AcceptsFocus=false;
		WebPanel.Surface.OnTexture+=UpdateProjection;
		
		ProjectionTexture=Texture.CreateRenderTarget("projector",ImageFormat.RGBA8888,Screen.Size,ProjectionTexture);
		Projector.Cookie=ProjectionTexture;
		Projector.LightColor=new Color(15,15,15);
		
		ScreenUI=WebPanel.AddChild<ScreenUI>();
		ScreenUI.MediaPlayer=this;
		ScreenUI.AcceptsFocus=false;
		Pointer=ScreenUI.AddChild<Pointer>();
    }
	private void UpdateProjection(ReadOnlySpan<byte> span, Vector2 size)
    {
		if (!LocalInside) return;//we're not inside theater, save fps.
		if (ProjectionTexture==null||ProjectionTexture.Size!=size)
      	{
            ProjectionTexture?.Dispose();
            ProjectionTexture=Texture.Create((int)size.x,(int)size.y,ImageFormat.BGRA8888).WithName("WebSurface_projected").WithDynamicUsage().Finish();
            WebPanel.Style.SetBackgroundImage(ProjectionTexture);
        }
	
        ProjectionTexture.Update(span,0,0,(int)size.x,(int)size.y);
		Projector.Cookie=ProjectionTexture;
	}
	protected override void OnFixedUpdate(){
		base.OnFixedUpdate();
	}
	protected override void OnEnabled()
	{
		base.OnEnabled();
	}
	protected override void OnDestroy(){
		base.OnDestroy();
		WorldUI?.Delete();
	}
	protected override void OnDisabled()
	{
		base.OnDisabled();
		WorldUI?.Delete();
	}
}