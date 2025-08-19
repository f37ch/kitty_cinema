//Modified version of https://github.com/badandbest/sbox-sprays
//Credits for original version goes to badandbest~
using System.IO;
using System.Linq;
using System.Net.Http;
using Sandbox.Resources;
using Sandbox.Utility;
public static partial class Spray
{
    [ConVar("spraydebug",Help="Renders who placed a spray." ),Change(nameof(OnDirty))]
	internal static bool EnableDebug {get;set;}

	[ConVar("spraydisable",Help="Disables player sprays. Good for streamers.",Saved=true),Change(nameof(OnDirty))]
	internal static bool DisableRendering {get;set;}
	private static bool IsImage(this HttpContent content)=>content.Headers.GetValues("Content-Type").Any(type=>type.Contains("image"));
    public static GameObject LocalSpray;

	[ConCmd("spray",Help="URL of image. Must be in quotes.")]
	internal static async void SetImage(string imageUrl)
	{
		try
		{
			var uri=new Uri(imageUrl);
			var response=await Http.RequestAsync(uri.AbsoluteUri);
			
			if (!response.Content.IsImage())
			{
				throw new FileNotFoundException("Not an image type: Sites like Tenor require you to Right click > Copy image address");
			}
		}
		catch (Exception e)
		{
			Log.Warning(e);
			imageUrl="materials/spray/fallback.vtex";
		}
		finally
		{
			Game.Cookies.Set("spray.url",imageUrl);
		}
	}
    private static void OnDirty(object oldValue, object newValue)
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
			Name=$"Spray - {Steam.PersonaName}",
			Transform = new Transform(tr.HitPosition,Rotation.LookAt(tr.Normal)),
			PrefabVariables = new Dictionary<string, object>
			{
				{"Image",Game.Cookies.Get("spray.url","materials/decals/default.png")},
				{"Placer",Steam.PersonaName},
			}
		};

		LocalSpray?.Destroy();
		LocalSpray=GameObject.Clone("prefabs/spray.prefab",config);

		LocalSpray.NetworkSpawn();
		LocalSpray.SetPrefabSource("prefabs/spray.prefab");
		return true;
	}
}

internal class SprayRenderer : Renderer
{
	[Property] internal Decal _decal {get;set;}
	[Property] internal TextRenderer _text {get;set;}

	[Property, ImageAssetPath]
	public string Image {get;set;}

	[Button("Remove Spray",Icon="clear")]
	public void Remove()=>GameObject.Destroy();

	public virtual void UpdateObject()
	{
		_decal.Enabled=!Spray.DisableRendering;
		_text.Enabled=Spray.EnableDebug;
	}

	protected override async void OnAwake()
	{
	    UpdateObject();

	    if (string.IsNullOrWhiteSpace(Image))
	        return;
		try
		{
			var tex = await Texture.LoadFromFileSystemAsync( Image, FileSystem.Mounted, true );
			if ( tex == null )
			{
				Log.Warning( $"[SprayRenderer] Unable to load texture: {Image}" );
				return;
			}
			

			var def = new DecalDefinition
			{
				ColorTexture = tex,
				Tint = Color.White,
			};

			_decal.Decals.Add( def );
	    }
		catch ( Exception ex )
		{
			Log.Error( ex, $"[SprayRenderer] Error Loading spray: {Image}" );
		}
	}
}