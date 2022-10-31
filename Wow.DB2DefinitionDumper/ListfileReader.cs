using System.IO;

namespace Wow.DB2DefinitionDumper
{
    public class ListfileReader
    {
        public bool IsLoaded { get; private set; } = false;

        private Dictionary<uint, string> _listfileEntries = new();

        /// <summary>
        /// Opens the listfile file and loads that into memory.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task ReadFileAsync(string path)
        {
            using var reader = new StreamReader(path);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                    continue;

                var split = line.Split(";");
                if (split.Length < 2)
                    continue;

                if (!uint.TryParse(split[0], out var fileDataId))
                    continue;

                _listfileEntries.Add(fileDataId, split[1]);
            }

            IsLoaded = true;
        }

        /// <summary>
        /// Retrieves all DB2 filenames
        /// </summary>
        /// <returns></returns>
        public Dictionary<uint, string> GetAvailableDB2s()
        {
            return _listfileEntries.Where(x => x.Value.EndsWith(".db2"))
                .OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Returns the FileDataID for a specific entry in storage.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public uint GetFileDataIdByEntry(string entry)
            => _listfileEntries.FirstOrDefault(x => x.Value == entry).Key;
    }
}
