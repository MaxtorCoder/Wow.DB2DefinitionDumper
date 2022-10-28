using DBDefsLib;

namespace Wow.DB2DefinitionDumper.DBD;

public struct DbdColumn
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Comment { get; set; }

    public Structs.Definition Field { get; set; }
    public Structs.ColumnDefinition Column { get; set; }
}