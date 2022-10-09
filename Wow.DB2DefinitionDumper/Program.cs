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
bool buildIsValid = false;
while (!buildIsValid)
{
    Console.Write("Build: ");
    build = Console.ReadLine();
    
    if (string.IsNullOrEmpty(build))
        Console.WriteLine("Please provide a valid build. Like 10.0.2.45969");
    else
    {
        string[] array = build.Split(new char[1] { '.' });
        if (array.Count() < 4)
        {
            Console.WriteLine("Please provide a valid build. Like 10.0.2.45969");
        }
        else
            buildIsValid = true;
    }
}

var dbdProvider = new GithubDbdProvider();
var dbdStream = await dbdProvider.StreamForTableName(tableName);

var dbdInfo = DbdBuilder.Build(dbdStream, tableName, build);
Console.WriteLine(dbdInfo.GetColumns());