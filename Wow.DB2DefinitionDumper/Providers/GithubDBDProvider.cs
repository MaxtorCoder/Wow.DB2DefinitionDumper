using System.IO;
using System.Net.Http;

namespace Wow.DB2DefinitionDumper.Providers;

public class GithubDbdProvider
{
    static readonly Uri BaseUri = new("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/");
    readonly HttpClient _client = new();

    public GithubDbdProvider()
    {
        if (!Directory.Exists("cache"))
            Directory.CreateDirectory("cache");

        _client.BaseAddress = BaseUri;
    }

    public async Task<Stream> StreamForTableName(string tableName)
    {
        var query = $"{tableName}.dbd";

        byte[] bytes;
        if (!File.Exists($"cache/{query}"))
        {
            bytes = await _client.GetByteArrayAsync(query);
            await File.WriteAllBytesAsync($"cache/{query}", bytes);
        }
        else
            bytes = await File.ReadAllBytesAsync($"cache/{query}");

        return new MemoryStream(bytes);
    }
}