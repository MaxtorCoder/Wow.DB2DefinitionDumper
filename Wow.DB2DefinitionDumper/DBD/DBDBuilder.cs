using DBDefsLib;
using System.IO;

namespace Wow.DB2DefinitionDumper.DBD;

public class DbdBuilder
{
    private static readonly DBDReader _reader = new();

    /// <summary>
    /// Builds a <see cref="DbdInfo"/> instance based on the provided <see cref="dbd"/> argument.
    /// </summary>
    /// <param name="dbd">The dbd file stream</param>
    /// <param name="name">The dbd name</param>
    /// <param name="build">The requested build</param>
    /// <returns></returns>
    public static DbdInfo? Build(Stream dbd, string name, string build, uint fileDataId)
    {
        var databaseDefinitions = _reader.Read(dbd);

        Structs.VersionDefinitions? versionToUse = null;
        if (!string.IsNullOrEmpty(build))
        {
            var dbBuild = new Build(build);
            Utils.GetVersionDefinitionByBuild(databaseDefinitions, dbBuild, out versionToUse);
        }

        versionToUse ??= databaseDefinitions.versionDefinitions.LastOrDefault();
        if (versionToUse == null || versionToUse.Value.layoutHashes == null || versionToUse.Value.layoutHashes.Length == 0)
        {
            return null;
        }

        var dbdInfo = new DbdInfo
        {
            FileName = name,
            LayoutHash = versionToUse.Value.layoutHashes[0],
            FileDataId = fileDataId
        };

        foreach (var fieldDefinition in versionToUse.Value.definitions)
        {
            var columnDefinition = databaseDefinitions.columnDefinitions[fieldDefinition.name];

            var dbdColumn = new DbdColumn
            {
                Name = fieldDefinition.name
            };

            if (columnDefinition.type == "locstring")
                dbdColumn.Name = dbdColumn.Name.Replace("_lang", "");

            // Remove all underscores from names and uppercase the next character
            while (dbdColumn.Name.Contains("_"))
            {
                var indexOf = dbdColumn.Name.IndexOf("_");
                dbdColumn.Name = dbdColumn.Name.Replace("_", "");
                dbdColumn.Name = dbdColumn.Name.ReplaceAt(indexOf, char.ToUpper(dbdColumn.Name[indexOf]));
            }

            if (fieldDefinition.isRelation && fieldDefinition.isNonInline)
                dbdColumn.Type = fieldDefinition.arrLength == 0 ? "int32" : $"int32[{fieldDefinition.arrLength}]";
            else
                dbdColumn.Type = FieldDefinitionToType(fieldDefinition, columnDefinition, true);

            dbdColumn.Field = fieldDefinition;
            dbdColumn.Column = columnDefinition;

            dbdInfo.Columns.Add(dbdColumn);
        }

        return dbdInfo;
    }

    private static string FieldDefinitionToType(Structs.Definition field, Structs.ColumnDefinition column, bool localiseStrings)
    {
        var isArray = field.arrLength != 0;

        switch (column.type)
        {
            case "int":
            {
                var typeString = field.size switch
                {
                    8 => field.isSigned ? "int8" : "uint8",
                    16 => field.isSigned ? "int16" : "uint16",
                    32 => field.isSigned ? "int32" : "uint32",
                    64 => field.isSigned ? "int64" : "uint64",
                    _ => new("Invalid field size")
                };

                return isArray ? $"std::array<{typeString}, {field.arrLength}>" : typeString;
            }
            case "string":
                return isArray ? $"std::array<char const*, {field.arrLength}>" : "char const*";
            case "locstring":
            {
                if (isArray)
                    throw new NotSupportedException("Localised string arrays are not supported");

                return !localiseStrings || isArray ? $"string[{field.arrLength}]" : "LocalizedString";
            }
            case "float":
                return isArray ? $"std::array<float, {field.arrLength}>" : "float";
            default:
                throw new ArgumentException("Unable to construct C++ type from " + column.type);
        }
    }
}