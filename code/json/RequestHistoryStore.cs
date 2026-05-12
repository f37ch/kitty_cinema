using System.Linq;
using System.Text.Json.Serialization;

public class HistoryEntry
{
	[JsonPropertyName( "id" )]
	public string Id { get; set; } = Guid.NewGuid().ToString( "N" );

	[JsonPropertyName( "url" )]
	public string Url { get; set; } = "";

	[JsonPropertyName( "title" )]
	public string Title { get; set; } = "Unknown";

	[JsonPropertyName( "thumb" )]
	public string Thumb { get; set; } = "/ui/thumb.png";

	/// <summary>Длительность в секундах. 0 = live.</summary>
	[JsonPropertyName( "duration" )]
	public float Duration { get; set; }

	/// <summary>Сколько раз запрашивалось.</summary>
	[JsonPropertyName( "count" )]
	public int Count { get; set; } = 1;

	/// <summary>Unix-время последнего запроса.</summary>
	[JsonPropertyName( "lastRequest" )]
	public long LastRequest { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public static class RequestHistoryStore
{
	private const string FileName = "cinema_history.json";
	private const int MaxEntries = 300;

	private static List<HistoryEntry> _cache = null;

	// -----------------------------------------------------------------------
	// Public API
	// -----------------------------------------------------------------------

	public static List<HistoryEntry> GetAll()
	{
		_cache ??= Load();
		return _cache;
	}

	public static void Record( string url, string title, string thumb = "/ui/thumb.png", float duration = 0 )
	{
		var all = GetAll();
		var existing = all.FirstOrDefault( e => e.Url == url );

		if ( existing != null )
		{
			existing.Title = title;
			existing.Thumb = thumb;
			existing.Duration = duration;
			existing.Count++;
			existing.LastRequest = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		}
		else
		{
			all.Insert( 0, new HistoryEntry
			{
				Url = url,
				Title = title,
				Thumb = thumb,
				Duration = duration
			} );

			if ( all.Count > MaxEntries )
				all.RemoveRange( MaxEntries, all.Count - MaxEntries );
		}

		Save();
	}

	public static void RemoveById( string id )
	{
		GetAll().RemoveAll( e => e.Id == id );
		Save();
	}

	public static void ClearAll()
	{
		_cache = new List<HistoryEntry>();
		Save();
	}

	private static List<HistoryEntry> Load()
	{
		if ( FileSystem.Data.FileExists( FileName ) )
			return FileSystem.Data.ReadJson<List<HistoryEntry>>( FileName ) ?? new();

		return new List<HistoryEntry>();
	}

	private static void Save()
	{
		FileSystem.Data.WriteJson( FileName, _cache ?? new() );
	}
}