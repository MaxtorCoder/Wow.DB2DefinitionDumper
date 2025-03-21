using System.Text;
using System.Text.RegularExpressions;

namespace Wow.DB2DefinitionDumper.DBD;

public class DbdInfo
{
    public string FileName { get; set; }
    public List<DbdColumn> Columns { get; } = new();
    public string? LayoutHash { get; set; }
    public uint FileDataId { get; set; }

    /// <summary>
    /// Parses the <see cref="DbdColumn"/> instance to a C like structure string.
    /// </summary>
    /// <returns></returns>
    public string DumpStructures()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"struct {FileName}Entry");
        builder.AppendLine("{");

        foreach (var column in Columns)
            builder.AppendLine($"    {column.Type} {column.Name};");

        builder.AppendLine("};");
        return builder.ToString();
    }

    /// <summary>
    /// Parses the <see cref="DbdColumn"/> instance to MetaData.
    /// </summary>
    /// <returns></returns>
    public string DumpMeta()
    {
        var indexField = -1;
        var parentIndexField = -1;
        var fieldCount = Columns.Count;
        var fileFieldCount = Columns.Count;

        const string padding = "    ";

        var columnsBuild = new StringBuilder();

        for (var i = 0; i < Columns.Count; ++i)
        {
            var column = Columns[i];

            if (column.Field.isID)
            {
                indexField = column.Field.isNonInline ? -1 : i;

                if (column.Field.isNonInline)
                {
                    fieldCount--;
                    fileFieldCount--;
                    continue;
                }
            }

            if (column.Field.isRelation)
            {
                parentIndexField = i + (indexField == -1 ? -1 : 0);

                if (column.Field.isNonInline)
                    fileFieldCount--;
            }

            columnsBuild.AppendLine($"        {ConvertColumnToMetaField(column)}");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"struct {FileName}Meta");
        builder.AppendLine("{");
        builder.AppendLine($"    static constexpr DB2MetaField Fields[{fieldCount}] =");
        builder.AppendLine("    {");
        builder.Append($"{columnsBuild}");
        builder.AppendLine("    };");
        builder.AppendLine();
        builder.AppendLine($"    static constexpr DB2Meta Instance{{ {FileDataId}, {indexField}, {fieldCount}, {fileFieldCount}, 0x{LayoutHash}, Fields, {parentIndexField} }};");
        builder.AppendLine("};");

        return builder.ToString();
    }

    /// <summary>
    /// Parses the <see cref="DbdColumn"/> instance to LoadInfo.
    /// </summary>
    /// <returns></returns>
    public string DumpLoadInfo()
    {
        var fieldCount = 0;

        var columnBuilder = new StringBuilder();
        foreach (var column in Columns)
        {
            var typeString = column.Field.size switch
            {
                8 => "FT_BYTE",
                16 => "FT_SHORT",
                32 => "FT_INT",
                64 => "FT_LONG",
                _ => new("Invalid field size")
            };

            if (column.Column.type is "string" or "locstring")
                typeString = "FT_STRING";

            var isSigned = column.Field.isSigned ? "true" : "false";
            if (column.Field.isID)
                isSigned = "false";

            if (column.Field.arrLength > 0)
            {
                for (var j = 0; j < column.Field.arrLength; ++j)
                {
                    columnBuilder.AppendLine($"        {{ {isSigned}, {typeString}, \"{column.Name}{j + 1}\" }},");
                    fieldCount++;
                }
            }
            else
            {
                columnBuilder.AppendLine($"        {{ {isSigned}, {typeString}, \"{column.Name}\" }},");
                fieldCount++;
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine($"struct {FileName}LoadInfo");
        builder.AppendLine("{");

        builder.AppendLine($"    static constexpr DB2FieldMeta Fields[{fieldCount}] =");
        builder.AppendLine("    {");
        builder.Append($"{columnBuilder}");
        builder.AppendLine("    };");

        builder.AppendLine();

        var sqlName = ConvertToScreamingSnakeCase(FileName);
        builder.AppendLine($"    static constexpr DB2LoadInfo Instance {{ Fields, {fieldCount}, &{FileName}Meta::Instance, HOTFIX_SEL_{sqlName} }};");

        builder.AppendLine("};");
        return builder.ToString();
    }

    static string ConvertColumnToMetaField(DbdColumn column)
    {
        var builder = new StringBuilder();

        builder.Append("{ ");

        var field = column.Field;
        var isArray = field.arrLength != 0;

        switch (column.Column.type)
        {
            case "int":
            {
                var typeString = field.size switch
                {
                    8 => "FT_BYTE",
                    16 => "FT_SHORT",
                    32 => "FT_INT",
                    64 => "FT_LONG",
                    _ => new("Invalid field size")
                };
                builder.Append(typeString);
                break;
            }
            case "string":
            {
                builder.Append("FT_STRING_NOT_LOCALIZED");
                break;
            }
            case "locstring":
            {
                builder.Append("FT_STRING");
                break;

            }
            case "float":
            {
                builder.Append("FT_FLOAT");
                break;
            }
            default:
                throw new ArgumentException("Unable to construct C++ type from " + column.Type);
        }

        builder.Append(", ");
        builder.Append(isArray ? field.arrLength : 1);
        builder.Append(", ");

        if (!field.isID && (field.isSigned || column.Column.type == "float" || column.Column.type == "locstring" || column.Column.type == "string"))
            builder.Append("true");
        else
            builder.Append("false");
        builder.Append(" },");
        return builder.ToString();
    }

    static string ConvertToScreamingSnakeCase(string input) => Regex.Replace(input, "([a-z])([A-Z])", "$1_$2").ToUpper();
}
