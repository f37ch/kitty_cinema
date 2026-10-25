//Modified version of https://github.com/badandbest/sbox-sprays
//Credits for original version goes to badandbest~
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Sandbox.Resources;
using Sandbox.Utility;
public static partial class Spray
{
    [ConVar("spraydebug",Help="Renders who placed a spray." ),Change(nameof(OnSDirty))]
	internal static bool EnableDebug {get;set;}

	[ConVar("spraydisable",Help="Disables player sprays. Good for streamers.",Saved=true),Change(nameof(OnSDirty))]
	internal static bool DisableRendering {get;set;}
	private static bool IsImage(this HttpContent content)=>content.Headers.GetValues("Content-Type").Any(type=>type.Contains("image"));
    public static GameObject LocalSpray;

	public const string DefaultImage="https://i.imgur.com/b0FoWrh.png";
	private const string HistoryCookie="spray.history";
	private const int HistorySize=30;

	/// <summary>The image the next spray will use.</summary>
	public static string CurrentImage=>Game.Cookies.Get("spray.url",DefaultImage);

	/// <summary>Previously used images, most recent first.</summary>
	public static List<string> History=>Game.Cookies.Get(HistoryCookie,new List<string>())??new List<string>();

	[ConCmd("spray",Help="URL of image. Must be in quotes.")]
	internal static async void SetImage(string imageUrl)
	{
		var error=await TrySetImage(imageUrl);
		if (error!=null) Log.Warning(error);
	}

	/// <summary>Checks the link points to an image and makes it the current spray.</summary>
	/// <returns>Null on success, otherwise why the link was refused.</returns>
	public static async Task<string> TrySetImage(string imageUrl)
	{
		try
		{
			var uri=new Uri(imageUrl.Trim());
			var response=await Http.RequestAsync(uri.AbsoluteUri);
			if (!response.Content.IsImage())
			{
				return "Not an image. Sites like Tenor need Right click > Copy image address.";
			}
			Game.Cookies.Set("spray.url",uri.AbsoluteUri);
			Remember(uri.AbsoluteUri);
			return null;
		}
		catch (UriFormatException)
		{
			return "This doesn't look like a link.";
		}
		catch (Exception e)
		{
			return $"Couldn't load the image ({e.Message}).";
		}
	}

	private static void Remember(string url)
	{
		var history=History;
		history.Remove(url);
		history.Insert(0,url);
		if (history.Count>HistorySize) history.RemoveRange(HistorySize,history.Count-HistorySize);
		Game.Cookies.Set(HistoryCookie,history);
	}

	public static void Forget(string url)
	{
		var history=History;
		if (history.Remove(url)) Game.Cookies.Set(HistoryCookie,history);
	}

	public static void ClearHistory()=>Game.Cookies.Set(HistoryCookie,new List<string>());
    private static void OnSDirty(bool oldValue, bool newValue)
	{
		
		foreach (var spray in Game.ActiveScene.GetAllComponents<SprayRenderer>())
		{
			spray.UpdateObject();
		}
	}
  
	public static bool Place(Vector3 eyeworld,Vector3 eye)
	{
		var trace=Game.SceneTrace.Ray(eyeworld,eyeworld+eye).WithoutTags("player");
		if (trace.Run() is not {Body.BodyType:PhysicsBodyType.Static} tr)
			return false;

		var config=new CloneConfig
		{
			Name = $"Spray - {Steam.PersonaName}",
			// On floors the image's top points away from the player so it reads from where it was sprayed;
			// on walls it stays upright.
			Transform = new Transform( tr.HitPosition, Rotation.LookAt( tr.Normal, MathF.Abs( tr.Normal.z ) > 0.7f ? eye.WithZ( 0 ).Normal : Vector3.Up ) ),
		};
		

		LocalSpray?.Destroy();
		LocalSpray=GameObject.Clone("prefabs/spray.prefab",config);

		LocalSpray.NetworkSpawn();

		var sprayfab=LocalSpray.GetComponentInChildren<SprayRenderer>();
		sprayfab.Image=CurrentImage;
		sprayfab._text.Text=Steam.PersonaName;
		
		return true;
	}
}


internal class SprayRenderer : Renderer
{
	[Property] internal Decal _decal {get;set;}
	[Property] internal TextRenderer _text {get;set;}

	[Property, ImageAssetPath]
	public string Image {get;set;}

	/// <summary>Side of the square the image fits into, in world units.</summary>
	private const float Size=80f;
	/// <summary>Lifts the flat image off the surface so it doesn't flicker into it.</summary>
	private const float SurfaceOffset=0.3f;
	/// <summary>How far the surface may stray from a plane and still count as flat.</summary>
	private const float FlatTolerance=1.5f;

	/// <summary>
	/// On a flat surface the image is a sprite lying on it: a decal blends with the surface's own normals and
	/// parallax, so carpet fibers and tile seams show through the paint. Corners, curved walls and edges
	/// keep the decal, which wraps around them where a flat sprite would hang in the air.
	/// </summary>
	private SpriteRenderer _sprite;
	private bool _useDecal;

	private static Texture _flatNormal;
	private static Texture _mattePaint;
	private static Texture FlatNormal=>_flatNormal??=SolidTexture(new Color32(128,128,255,255),"SprayFlatNormal");
	/// <summary>Roughness 0.85, no metal, no occlusion.</summary>
	private static Texture MattePaint=>_mattePaint??=SolidTexture(new Color32(217,0,255,255),"SprayMattePaint");

	private static Texture SolidTexture(Color32 color,string name)
	{
		var data=new byte[4*4*4];
		for (int i=0;i<data.Length;i+=4){
			data[i]=color.r;data[i+1]=color.g;data[i+2]=color.b;data[i+3]=color.a;
		}
		return Texture.Create(4,4,ImageFormat.RGBA8888).WithData(data).WithName(name).Finish();
	}

	[Button("Remove Spray",Icon="clear")]
	public void Remove()=>GameObject.Destroy();

	public virtual void UpdateObject()
	{
		_decal.Enabled=_useDecal&&!Spray.DisableRendering;
		if (_sprite.IsValid()) _sprite.Enabled=!_useDecal&&!Spray.DisableRendering;
		_text.Enabled=Spray.EnableDebug;
	}

	/// <summary>Traces a grid over the spray's area: every point must hit the same plane.</summary>
	private bool IsSurfaceFlat()
	{
		var normal=WorldRotation.Forward;
		var right=WorldRotation.Right;
		var up=WorldRotation.Up;
		var origin=WorldPosition;
		for (int x=-2;x<=2;x++){
			for (int y=-2;y<=2;y++){
				var point=origin+right*(x*Size*0.25f)+up*(y*Size*0.25f);
				var tr=Scene.Trace.Ray(point+normal*8,point-normal*8).WithoutTags("player").Run();
				if (!tr.Hit) return false;
				if (MathF.Abs(Vector3.Dot(tr.HitPosition-origin,normal))>FlatTolerance) return false;
				if (Vector3.Dot(tr.Normal,normal)<0.97f) return false;
			}
		}
		return true;
	}

	protected override async void OnAwake()
	{
		UpdateObject();

		await Task.DelayRealtimeSeconds( 0.2f );//cuz it needs time to get our link first

		if ( string.IsNullOrWhiteSpace( Image ) )
			return;
		try
		{
			var tex = await Texture.LoadFromFileSystemAsync( Image, FileSystem.Mounted, true );
			if ( tex == null )
			{
				Log.Warning( $"[SprayRenderer] Unable to load texture: {Image}" );
				return;
			}

			_useDecal=!IsSurfaceFlat();
			if (_useDecal){
				// Flat normal and matte RMO soften the surface's relief under the paint as much as a decal can.
				_decal.Decals.Add( new DecalDefinition
				{
					ColorTexture = tex,
					NormalTexture = FlatNormal,
					RoughMetalOcclusionTexture = MattePaint,
					Tint = Color.White,
				} );
			}else{
				// The spray faces along its forward (the surface normal); the child is local to every client.
				var go = new GameObject( GameObject, true, "Spray Image" );
				go.NetworkMode = NetworkMode.Never;
				go.LocalPosition = Vector3.Forward * SurfaceOffset;
				_sprite = go.Components.Create<SpriteRenderer>();
				_sprite.Sprite = Sprite.FromTexture( tex );
				_sprite.Billboard = SpriteRenderer.BillboardMode.None;
				_sprite.Lighting = true;
				_sprite.Size = new Vector2( Size, Size );
			}
			UpdateObject();
		}
		catch ( Exception ex )
		{
			Log.Error( ex, $"[SprayRenderer] Error Loading spray: {Image}" );
		}
	}
}
