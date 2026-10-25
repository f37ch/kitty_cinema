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
	/// <summary>Unique per queue entry: the same video may come back later.</summary>
	public string uid { get; set; }
	public ulong requesterid { get; set; }
}
public struct Skips
{
	public ulong steamid { get; set; }
}
public struct QueueLike
{
	public string uid { get; set; }
	public ulong steamid { get; set; }
}
public class MediaPlayer : Component
{
	[Property]private ModelRenderer ScreenModel { get; set; }
	[Property]public GameObject PanelComponent { get; set; }
	[Property]private SpotLight Projector { get; set; }
	[Property]private Sandbox.WorldPanel WorldPanel { get; set; }

	/// <summary>Cone half-angle. Wider spreads the light more to the sides, like a real screen.</summary>
	[Property,Group("Screen Light"),Range(30,85)] public float LightAngle {get;set;}=75;
	/// <summary>Brightness for a 12 x 6.75 m screen showing a white frame; scales with the screen's area.</summary>
	[Property,Group("Screen Light")] public float LightIntensity {get;set;}=4f;
	/// <summary>Range per unit of the screen's size (square root of its area).</summary>
	[Property,Group("Screen Light")] public float LightRangeScale {get;set;}=2f;
	/// <summary>How fast the light follows the frame. Lower is softer, higher reacts sharper to scene cuts.</summary>
	[Property,Group("Screen Light")] public float LightSmoothing {get;set;}=5f;
	[Property,Group("Screen Light"),Range(0,2)] public float LightSaturation {get;set;}=1.25f;
	/// <summary>The light sits behind the screen: the screen itself would shadow it, so it doesn't cast shadows by default.</summary>
	[Property,Group("Screen Light")] public bool LightShadows {get;set;}=false;
	[Property,Group("Screen Light")] public bool MirrorCookie {get;set;}=false;
	/// <summary>Glow of a paused or not loaded screen.</summary>
	[Property,Group("Screen Light")] public Color IdleColor {get;set;}=new Color(0.35f,0.42f,0.65f);
	[Property,Group("Screen Light"),Range(0,1)] public float IdleLevel {get;set;}=0.07f;
	/// <summary>Optional: tinted with the screen's average color while the local player is inside.</summary>
	[Property,Group("Screen Light")] public AmbientLight Ambient {get;set;}
	/// <summary>Ambient tint from a 12 x 6.75 m screen; smaller screens tint proportionally less.</summary>
	[Property,Group("Screen Light")] public float AmbientStrength {get;set;}=0.14f;

	/// <summary>Screen the defaults are tuned for: 12 x 6.75 m, in square inches.</summary>
	private const float ReferenceArea=472f*266f;
	/// <summary>Cookie resolution: columns and rows of color across the room.</summary>
	private const int CookieSize=16;
	/// <summary>The video rarely changes faster than the eye follows a room's light.</summary>
	private const float CookieRate=30f;
	private const float AmbientSampleInterval=0.1f;
	private Texture WebTexture {get;set;}
	private Texture LightCookie {get;set;}
	private ComputeShader CookieShader {get;set;}=new ComputeShader("shaders/ScreenLightCookie");
	private float LightScale {get;set;}
	private float AmbientWeight {get;set;}
	private Color AmbientBase {get;set;}
	private Color AmbientTarget {get;set;}
	private Color AmbientCurrent {get;set;}
	private bool AmbientPending {get;set;}
	private bool CookieInitialized {get;set;}
	private RealTimeSince SinceCookie {get;set;}
	private RealTimeSince SinceAmbientSample {get;set;}
	private RealTimeSince SinceIdle {get;set;}
	private Sandbox.UI.WorldPanel WorldUI { get; set; }
    public WebPanel WebPanel {get;set;}
	private ScreenUI ScreenUI {get;set;}
	private Pointer Pointer {get;set;}
	private ScreenInfo ScreenInfo {get;set;}
	public Sandbox.WorldInput WorldInput {get;set;}

	// Playback state. Only the host changes it (see MediaController); clients just follow.
	[Sync(SyncFlags.FromHost)] public float Duration{get;set;}
	/// <summary>Seconds into the video, advanced by the host every frame.</summary>
	[Sync(SyncFlags.FromHost)] public float Curtime{get;set;}
	[Sync(SyncFlags.FromHost)] public string Service{get;set;}
	[Sync(SyncFlags.FromHost)] public string ContentID{get;set;}
	[Sync(SyncFlags.FromHost)] public bool Paused {get;set;}
	[Sync(SyncFlags.FromHost)] public bool IsLive {get;set;}
	[Sync(SyncFlags.FromHost)] public string Title {get;set;}
	[Sync(SyncFlags.FromHost)] public string Thumbnail{get;set;}
	[Sync(SyncFlags.FromHost)] public string Requester{get;set;}
	/// <summary>Bumped on every new video and seek: clients reload the player when it changes.</summary>
	[Sync(SyncFlags.FromHost)] public int PlaybackId{get;set;}
	[Sync(SyncFlags.FromHost)] public NetList<Video> VideoList {get;set;}=new();
	[Sync(SyncFlags.FromHost)] public NetList<Skips> SkipList {get;set;}=new();
	[Sync(SyncFlags.FromHost)] public NetList<QueueLike> LikeList {get;set;}=new();
	[Sync] public string Location{get;set;}
	/// <summary>Local request cooldown (Time.Now).</summary>
	public float RequestCD {get;set;}=0;
	public float Volume{get;set;}=40;
	public bool IsPlaying=>Service!=null;
	public static bool IsAuthority=>!Networking.IsActive||Networking.IsHost;
	private bool LocalInside {get;set;}
	private TheaterPlayer LocalPlayer {get;set;}
	/// <summary>What the web surface shows now; the page is reloaded only when this changes.</summary>
	private string LoadedPage {get;set;}
	private bool MouseToggle {get;set;}
	/// <summary>The player pages need a click for autoplay with sound: click for a while after every load.</summary>
	private RealTimeSince SincePageLoad {get;set;}
	private const float AutoplayClickTime=8f;
	private string HomeUrl=>$"{MediaController.WebHandlersURL}player.php?tp=dl";

	protected override void OnUpdate(){
		if (IsAuthority) HostThink();
		UpdateLocalInside();
		UpdatePage();
		EnsureWorldInput();
		UpdatePointer();
		UpdateScreenLight();
    }

	/// <summary>Host: advances time, ends videos, runs the queue and skip votes.</summary>
	private void HostThink(){
		if (!IsPlaying){
			if (VideoList.Count>0) PlayNext();
			return;
		}
		if (SkipList.Count>=MediaController.CountSkips(GameObject.Id)){
			MediaController.ChatMsg(this,"Video was skipped by vote.");
			PlayNext();
			return;
		}
		if (Paused||IsLive) return;
		Curtime=MathF.Min(Curtime+Time.Delta,Duration);
		if (Curtime>=Duration) PlayNext();
	}
	public static string QueueKey(Video vid)=>vid.uid??vid.videoid;
	public int LikesOf(Video vid){
		var key=QueueKey(vid);
		return LikeList.Count(x=>x.uid==key);
	}
	/// <summary>The queue as it plays: most liked first, then in request order.</summary>
	public IEnumerable<Video> OrderedQueue=>VideoList.Select((v,i)=>(v,i)).OrderByDescending(x=>LikesOf(x.v)).ThenBy(x=>x.i).Select(x=>x.v);
	/// <summary>Host: takes an entry out of the queue with its likes.</summary>
	public bool RemoveFromQueue(string key){
		for (int i=0;i<VideoList.Count;i++){
			if (QueueKey(VideoList[i])!=key) continue;
			VideoList.RemoveAt(i);
			for (int j=LikeList.Count-1;j>=0;j--){
				if (LikeList[j].uid==key) LikeList.RemoveAt(j);
			}
			return true;
		}
		return false;
	}
	/// <summary>Host: starts the most liked video from the queue, or stops playback when it's empty.</summary>
	public void PlayNext(){
		if (VideoList.Count==0){
			StopPlayBack();
			return;
		}
		var vid=OrderedQueue.First();
		RemoveFromQueue(QueueKey(vid));
		StartVideo(vid);
		MediaController.ChatMsg(this,$"[QUE] {vid.requester} has requested video: [{vid.servicename}] {vid.title}");
	}
	/// <summary>Host: starts playing a video right away.</summary>
	public void StartVideo(Video vid){
		Service=vid.service;
		ContentID=vid.videoid;
		Title=vid.title;
		Thumbnail=vid.thumb;
		Requester=vid.requester;
		Duration=vid.duration;
		IsLive=vid.duration<=0;
		Curtime=0;
		Paused=false;
		SkipList.Clear();
		PlaybackId++;
	}
	/// <summary>Host: jumps to a time; clients reload the player there.</summary>
	public void SeekTo(float time){
		// Not to the very end: that would end the video instead of rewinding it.
		Curtime=Math.Clamp(time,0,MathF.Max(Duration-1,0));
		PlaybackId++;
	}
	public void StopPlayBack(){
		IsLive=default;
		Service=null;
		ContentID=null;
		Thumbnail=null;
		Title=null;
		Requester=null;
		Paused=default;
		Duration=0;
		Curtime=0;
		SkipList.Clear();
		PlaybackId++;
	}

	private void UpdateLocalInside(){
		if (!LocalPlayer.IsValid()){
			LocalPlayer=Scene.GetAllComponents<TheaterPlayer>().FirstOrDefault(x=>x.IsLocalPlayer);
		}
		LocalInside=LocalPlayer.IsValid()&&Location!=null&&LocalPlayer.Location==Location;
	}

	/// <summary>
	/// Shows what the shared state asks for: the home screen, the paused screen or the video at the current time.
	/// The page is reloaded only when the wanted page changes, so every client starts at the host's time.
	/// </summary>
	private void UpdatePage(){
		if (WebPanel==null) return;
		string page;
		if (!LocalInside||!IsPlaying){
			page="home";
			ShowHomeScreen();
			ScreenUI.SetClass("Notinside",!LocalInside);
		}else{
			ShowPlaybackScreen();
			ScreenInfo.SetClass("Paused",Paused);
			page=Paused?"paused":$"play:{PlaybackId}:{Volume}";
		}
		if (page!=LoadedPage){
			LoadedPage=page;
			SincePageLoad=0;
			WebPanel.Surface.Url=page switch{
				"home"=>HomeUrl,
				"paused"=>$"{MediaController.WebHandlersURL}player.php?tp=n",
				_=>$"{MediaController.WebHandlersURL}player.php?tp={Service}&dt={Uri.EscapeDataString(ContentID??"")}&vol={Volume}&st={(int)Curtime}"
			};
		}
		if (LocalInside&&page!="home"&&SincePageLoad<AutoplayClickTime){
			MouseToggle=!MouseToggle;
			WebPanel.Surface.TellMouseButton(MouseButtons.Left,MouseToggle);
		}
	}
	private void ShowHomeScreen(){
		if (ScreenUI!=null) return;
		ScreenInfo?.Delete();
		ScreenInfo=null;
		ScreenUI=WebPanel.AddChild<ScreenUI>();
		ScreenUI.MediaPlayer=this;
		ScreenUI.AcceptsFocus=false;
		Pointer=ScreenUI.AddChild<Pointer>();
		Pointer.AddClass("Hide");
	}
	private void ShowPlaybackScreen(){
		if (ScreenInfo!=null) return;
		ScreenUI?.Delete();
		ScreenUI=null;
		Pointer=null;
		ScreenInfo=WebPanel.AddChild<ScreenInfo>();
		ScreenInfo.MediaPlayer=this;
	}
	/// <summary>The home screen's service buttons are clicked with the crosshair.</summary>
	private void UpdatePointer(){
		if (ScreenUI==null||Pointer==null||WorldUI==null||Scene.Camera==null) return;
		var ray=Scene.Camera.ScreenPixelToRay(Screen.Size/2);
		bool aiming=LocalInside&&!Input.Down("queue")&&ray.Position.Distance(WorldUI.Position)<WorldUI.MaxInteractionDistance;
		ScreenUI.Style.PointerEvents=aiming?PointerEvents.All:PointerEvents.None;
		Pointer.SetClass("Hide",!aiming);
		if (aiming){
			Pointer.Style.Top=WorldUI.PanelBounds.Top + WorldUI.MousePosition.y/2;
			Pointer.Style.Left=(WorldUI.MousePosition.x-WorldUI.PanelBounds.Width/2)/2;
		}
	}

	protected override void OnStart(){
		
		WorldPanel.InteractionRange=800;
		WorldPanel.PanelSize= new Vector2(
		  7050*ScreenModel.WorldScale.x,
		  1700*ScreenModel.WorldScale.y
		);
		WorldPanel.RenderScale=6;

    	WorldUI=WorldPanel.GetPanel() as Sandbox.UI.WorldPanel;

		EnsureWorldInput();

		WebPanel=WorldUI.AddChild<WebPanel>();
		WebPanel.Style.Width=Length.Percent(100);
		WebPanel.Style.Height=Length.Percent(100);
		WebPanel.Style.PointerEvents=PointerEvents.None;
		WebPanel.AcceptsFocus=false;
		WebPanel.Surface.OnTexture+=UpdateProjection;

		FitLightToScreen();
    }
	/// <summary>The scene camera can show up after the snapshot loads (joining a lobby): hook it once it exists.</summary>
	private void EnsureWorldInput(){
		if (WorldInput.IsValid()) return;
		var cam=Scene.Camera;
		if (cam==null) return;
		WorldInput=cam.Components.GetOrCreate<Sandbox.WorldInput>();
		WorldInput.LeftMouseAction="use";
	}
	private void UpdateProjection(ReadOnlySpan<byte> span, Vector2 size)
    {
		if (!LocalInside) return;//we're not inside theater, save fps.
		if (WebTexture == null || WebTexture.Size != size)
		{
			WebTexture?.Dispose();
			WebTexture = Texture.Create((int)size.x, (int)size.y, ImageFormat.BGRA8888).WithUAVBinding().WithName("WebSurface").WithDynamicUsage().Finish();
			WebPanel.Style.SetBackgroundImage(WebTexture);
		}
    	WebTexture.Update(span,0,0,(int)size.x,(int)size.y);
	}
	/// <summary>
	/// Light from the screen: a spotlight behind the screen plane shines through it into the hall, its cookie is
	/// the frame shrunk to a few columns of color. The cone passes through the screen's edges and the brightness
	/// scales with the screen's area: a small screen lights a corner, a big one the whole hall.
	/// </summary>
	private void FitLightToScreen(){
		var sizes=ScreenModel.Model.Bounds.Size*ScreenModel.WorldScale;
		var axes=new[]{MathF.Abs(sizes.x),MathF.Abs(sizes.y),MathF.Abs(sizes.z)}.OrderByDescending(x=>x).ToArray();
		float width=axes[0],area=axes[0]*axes[1];
		var forward=WorldPanel.WorldRotation.Forward;

		// Behind the screen plane at the distance where the cone passes through its edges.
		float distance=width*0.5f/MathF.Tan(LightAngle.DegreeToRadian());
		Projector.WorldPosition=WorldPanel.WorldPosition-forward*distance;
		Projector.WorldRotation=Rotation.LookAt(forward,Vector3.Up);
		Projector.ConeOuter=LightAngle;
		Projector.ConeInner=LightAngle*0.4f;
		Projector.Radius=distance+MathF.Sqrt(area)*LightRangeScale;
		Projector.Shadows=LightShadows;
		Projector.Enabled=false;
		LightScale=LightIntensity*area/ReferenceArea;
		AmbientWeight=AmbientStrength*MathF.Min(1f,area/ReferenceArea);

		LightCookie=Texture.Create(CookieSize,CookieSize,ImageFormat.RGBA8888).WithUAVBinding().WithName("ScreenLightCookie").WithDynamicUsage().Finish();
		CookieShader.Attributes.Set("OutputTexture",LightCookie);
		CookieShader.Attributes.Set("CookieSize",(float)CookieSize);
	}
	private void UpdateScreenLight(){
		if (LightCookie==null) return;
		if (!LocalInside){
			// Outside the theater the screen costs nothing and doesn't tint the room.
			if (Projector.Enabled) Projector.Enabled=false;
			RestoreAmbient();
			CookieInitialized=false;
			return;
		}

		bool hasPicture=WebTexture!=null&&!(Service!=null&&Paused);
		if (hasPicture) SinceIdle=0;
		// Once the idle glow has settled there is nothing left to blend.
		bool settledIdle=!hasPicture&&CookieInitialized&&SinceIdle>4f/LightSmoothing;
		if (!settledIdle&&SinceCookie>=1f/CookieRate){
			// Elapsed time is capped: after a settled idle pause the light still eases into the first frame.
			float elapsed=MathF.Min(SinceCookie,2f/CookieRate);
			float blend=CookieInitialized?1f-MathF.Exp(-LightSmoothing*elapsed):1f;
			SinceCookie=0;
			CookieInitialized=true;

			var idle=IdleColor*IdleLevel;
			CookieShader.Attributes.Set("InputTexture",hasPicture?WebTexture:LightCookie);
			CookieShader.Attributes.Set("InputSize",hasPicture?WebTexture.Size:new Vector2(CookieSize));
			CookieShader.Attributes.Set("IdleColor",new Vector3(idle.r,idle.g,idle.b));
			CookieShader.Attributes.Set("HasPicture",hasPicture?1f:0f);
			CookieShader.Attributes.Set("Mirror",MirrorCookie?1f:0f);
			CookieShader.Attributes.Set("Saturation",LightSaturation);
			CookieShader.Attributes.Set("Blend",blend);
			CookieShader.Dispatch(CookieSize,CookieSize,1);

			Projector.Cookie=LightCookie;
			Projector.LightColor=new Color(LightScale,LightScale,LightScale);
			if (!Projector.Enabled) Projector.Enabled=true;
		}
		UpdateAmbient();
	}
	private void UpdateAmbient(){
		if (!Ambient.IsValid()) return;
		if (AmbientWeight<=0) return;
		if (!AmbientPending&&SinceAmbientSample>=AmbientSampleInterval){
			if (AmbientBase==default) AmbientBase=Ambient.Color;
			SinceAmbientSample=0;
			AmbientPending=true;
			LightCookie.GetPixelsAsync(OnAmbientReadback,0);
		}
		// Readbacks arrive in steps; ease between them.
		AmbientCurrent=Color.Lerp(AmbientCurrent,AmbientTarget,1f-MathF.Exp(-LightSmoothing*RealTime.Delta));
		var sum=AmbientBase+AmbientCurrent;
		Ambient.Color=sum.WithAlpha(AmbientBase.a);
	}
	private void OnAmbientReadback(ReadOnlySpan<Color32> pixels){
		AmbientPending=false;
		if (pixels.Length==0) return;
		float r=0,g=0,b=0;
		foreach (var p in pixels){r+=p.r;g+=p.g;b+=p.b;}
		float scale=AmbientWeight/(255f*pixels.Length);
		AmbientTarget=new Color(r*scale,g*scale,b*scale,0);
	}
	private void RestoreAmbient(){
		if (AmbientBase==default) return;
		if (Ambient.IsValid()) Ambient.Color=AmbientBase;
		AmbientBase=default;
		AmbientCurrent=AmbientTarget=default;
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
		RestoreAmbient();
		WorldPanel?.Destroy();
		WebTexture?.Dispose();
		LightCookie?.Dispose();
	}
	protected override void OnDisabled()
	{
		base.OnDisabled();
		RestoreAmbient();
		WorldPanel?.Destroy();
	}
}