namespace Wow.DB2DefinitionDumper.Providers;

public class GithubDbdProvider
{
    private static readonly Uri BaseUri = new("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/");
    private readonly HttpClient _client = new();

    public GithubDbdProvider()
    {
        _client.BaseAddress = BaseUri;
    }

    public async Task<Stream> StreamForTableName(string tableName)
    {
        var query = $"{tableName}.dbd";
        var bytes = await _client.GetByteArrayAsync(query);
        return new MemoryStream(bytes);
    }
}