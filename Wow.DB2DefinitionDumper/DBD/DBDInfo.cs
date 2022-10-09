using System.Text;

namespace Wow.DB2DefinitionDumper.DBD;

public class DbdInfo
{
    public List<DbdColumn> Columns { get; } = new();

    /// <summary>
    /// Parses the <see cref="DbdColumn"/> instance to a C like structure string.
    /// </summary>
    /// <returns></returns>
    public string GetColumns()
    {
        var padding = GetMaxPadding();
        
        var builder = new StringBuilder();
        foreach (var column in Columns)
        {
            builder.Append($"{column.Type.PadRight(padding.Type)} {(column.Name + ";").PadRight(padding.Name)}");
            if (!string.IsNullOrEmpty(column.Comment))
                builder.Append($"// {column.Comment}");
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private (int Name, int Type) GetMaxPadding()
    {
        var typePadding = 6;
        var namePadding = 20;
        foreach (var column in Columns)
        {
            if (column.Type.Length > typePadding)
                typePadding = column.Type.Length + 1;
            if (column.Name.Length > namePadding)
                namePadding = column.Name.Length + 1;
        }

        return (namePadding, typePadding);
    }
}