using DBDefsLib;
using System.IO;
using System.Linq;

namespace Wow.DB2DefinitionDumper.DBD;

public class DbdBuilder
{
    /// <summary>
    /// Builds a <see cref="DbdInfo"/> instance based on the provided <see cref="dbd"/> argument.
    /// </summary>
    /// <param name="dbd">The dbd file stream</param>
    /// <param name="name">The dbd name</param>
    /// <param name="build">The requested build</param>
    /// <returns></returns>
    public static DbdInfo Build(Stream dbd, string name, string build)
    {
        var dbdReader = new DBDReader();

        var databaseDefinitions = dbdReader.Read(dbd);

        Structs.VersionDefinitions? versionToUse = null;
        if (!string.IsNullOrEmpty(build))
        {
            var dbBuild = new Build(build);
            Utils.GetVersionDefinitionByBuild(databaseDefinitions, dbBuild, out versionToUse);
        }

        if (versionToUse == null)
        {
            versionToUse = databaseDefinitions.versionDefinitions.Last();
         //   throw new($"No definition found for table: {name} with build: {build}");
        }

        var dbdInfo = new DbdInfo();

        dbdInfo.FileName = name;
        dbdInfo.LayoutHash = versionToUse.Value.layoutHashes[0];

        foreach (var fieldDefinition in versionToUse.Value.definitions)
        {
            var columnDefinition = databaseDefinitions.columnDefinitions[fieldDefinition.name];

            var dbdColumn = new DbdColumn
            {
                Name = fieldDefinition.name
            };

            if (fieldDefinition.isRelation && fieldDefinition.isNonInline)
                dbdColumn.Type = fieldDefinition.arrLength == 0 ? "int32" : $"int32[{fieldDefinition.arrLength}]";
            else
                dbdColumn.Type = FieldDefinitionToType(fieldDefinition, columnDefinition, true);

            if (fieldDefinition.isID)
                dbdColumn.Comment += "ID ";
            if (fieldDefinition.isRelation)
                dbdColumn.Comment += "Relation ";
            if (fieldDefinition.isNonInline)
                dbdColumn.Comment += "Non-inline ";

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
                return isArray ? $"string[{field.arrLength}]" : "string";
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