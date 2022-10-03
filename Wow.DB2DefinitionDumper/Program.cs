using Wow.DB2DefinitionDumper.DBD;
using Wow.DB2DefinitionDumper.Providers;

var tableName = string.Empty;
while (string.IsNullOrEmpty(tableName))
{
    Console.Write("Table name: ");
    tableName = Console.ReadLine();
    
    if (string.IsNullOrEmpty(tableName))
        Console.WriteLine("Please provide a valid table name.");
}

var build = string.Empty;
while (string.IsNullOrEmpty(build))
{
    Console.Write("Build: ");
    build = Console.ReadLine();
    
    if (string.IsNullOrEmpty(build))
        Console.WriteLine("Please provide a valid build.");
}

var dbdProvider = new GithubDbdProvider();
var dbdStream = await dbdProvider.StreamForTableName(tableName);

var dbdInfo = DbdBuilder.Build(dbdStream, tableName, build);
Console.WriteLine(dbdInfo.GetColumns());