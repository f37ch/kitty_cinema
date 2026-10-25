using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
/// <summary>
/// Twemoji (CC-BY 4.0, see ui/emoji/NOTICE.txt) from one atlas: the picker's categories and the images that
/// replace emoji in chat messages. Messages stay plain Unicode text, only the drawing changes.
/// Data: ui/emoji/emoji.json, generated from UCinema's emoji catalog and atlas.
/// </summary>
public static class EmojiCatalog
{
	public const string AtlasPath="/ui/emoji/twemoji.png";

	public class Category
	{
		[JsonPropertyName("icon")] public string Icon {get;set;}
		[JsonPropertyName("name")] public string Name {get;set;}
	}
	public class Emoji
	{
		/// <summary>The text the emoji stands for.</summary>
		[JsonPropertyName("u")] public string Unicode {get;set;}
		[JsonPropertyName("n")] public string Name {get;set;}
		[JsonPropertyName("c")] public int Category {get;set;}
		/// <summary>Top-left corner of the 64 px image in the atlas.</summary>
		[JsonPropertyName("x")] public int X {get;set;}
		[JsonPropertyName("y")] public int Y {get;set;}
	}
	private class Data
	{
		[JsonPropertyName("atlas")] public int Atlas {get;set;}
		[JsonPropertyName("size")] public int Size {get;set;}
		[JsonPropertyName("categories")] public List<Category> Categories {get;set;}
		[JsonPropertyName("items")] public List<Emoji> Items {get;set;}
	}

	private static Data _data;
	private static Dictionary<string,Emoji> _byText;
	private static int _longest;

	private static Data Loaded
	{
		get
		{
			if (_data!=null) return _data;
			_data=FileSystem.Mounted.ReadJson<Data>("ui/emoji/emoji.json")??new Data{Categories=new(),Items=new()};
			_byText=new Dictionary<string,Emoji>();
			foreach (var e in _data.Items){
				var key=Normalize(e.Unicode);
				if (!_byText.ContainsKey(key)) _byText[key]=e;
				_longest=Math.Max(_longest,key.Length);
			}
			return _data;
		}
	}

	public static IReadOnlyList<Category> Categories=>Loaded.Categories;
	public static IReadOnlyList<Emoji> All=>Loaded.Items;
	public static IEnumerable<Emoji> InCategory(int category)=>Loaded.Items.Where(x=>x.Category==category);

	public static Emoji Find(string text)
	{
		_=Loaded;
		return text!=null&&_byText.TryGetValue(Normalize(text),out var e)?e:null;
	}

	public static IEnumerable<Emoji> Search(string query)
	{
		query=query.Trim().ToLowerInvariant().Replace(' ','_');
		return Loaded.Items.Where(x=>x.Name.Contains(query));
	}

	private static Bitmap _atlas;
	private static readonly Dictionary<Emoji,Texture> _textures=new();

	/// <summary>
	/// The emoji's own small texture, cut from the atlas the first time it's shown. UI background offsets into
	/// a large atlas don't line up in the engine, so every emoji is drawn from its own texture.
	/// </summary>
	public static Texture TextureOf(Emoji emoji)
	{
		if (emoji==null) return null;
		if (_textures.TryGetValue(emoji,out var texture)) return texture;
		_atlas??=Bitmap.CreateFromBytes(FileSystem.Mounted.ReadAllBytes(AtlasPath.TrimStart('/')).ToArray());
		var size=Loaded.Size;
		texture=_atlas.Crop(new Rect(emoji.X,emoji.Y,size,size)).ToTexture(true);
		_textures[emoji]=texture;
		return texture;
	}

	/// <summary>The emoji's texture if it's been cut already, without cutting it now.</summary>
	public static Texture CachedTextureOf(Emoji emoji)=>emoji!=null&&_textures.TryGetValue(emoji,out var texture)?texture:null;

	/// <summary>A piece of a message: plain text, or an emoji drawn from the atlas.</summary>
	public readonly record struct Token(string Text,Emoji Emoji);

	/// <summary>Splits a message into words and emoji, so the chat can draw emoji as images and still wrap lines.</summary>
	public static List<Token> Tokenize(string message)
	{
		_=Loaded;
		var tokens=new List<Token>();
		if (string.IsNullOrEmpty(message)) return tokens;
		var text=Normalize(message);
		var word=new StringBuilder();
		void FlushWord(){ if (word.Length>0){ tokens.Add(new Token(word.ToString(),null)); word.Clear(); } }
		int i=0;
		while (i<text.Length){
			Emoji found=null; int length=0;
			for (int len=Math.Min(_longest,text.Length-i);len>0;len--){
				if (_byText.TryGetValue(text.Substring(i,len),out var e)){ found=e; length=len; break; }
			}
			if (found!=null){
				FlushWord();
				tokens.Add(new Token(null,found));
				i+=length;
				continue;
			}
			var c=text[i];
			word.Append(c);
			i++;
			// Words end after a space, so lines can wrap between them.
			if (c==' ') FlushWord();
		}
		FlushWord();
		return tokens;
	}

	/// <summary>Emoji are matched without the emoji presentation selector, which keyboards add or leave out.</summary>
	private static string Normalize(string text)=>text.Replace("️","");
}
