using System.IO;
using System.Text.Json;
using System.Windows;

using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;

using Wow.DB2DefinitionDumper.DBD;
using Wow.DB2DefinitionDumper.Providers;

namespace Wow.DB2DefinitionDumper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        readonly ListfileReader _listfileReader = new();
        readonly List<(string, string)> _metadatas = new();
        readonly List<(string, string)> _structures = new();
        readonly List<(string, string)> _loadInfos = new();

        List<DB2DisplayNameRecord> _db2DisplayNames = new();

        public MainWindow()
        {
            InitializeComponent();

            LoadDisplayNames();

            ToolBar.MouseLeftButtonDown += (_, _) => DragMove();
            CloseBtn.Click += (_, _) => Close();
            MinimizeBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        }

        async void DumpButton_Click(object sender, RoutedEventArgs e)
        {
            DumpButton.IsEnabled = false;
            VersionBox.IsEnabled = false;
            TableName.IsEnabled = false;
            TableDropdown.IsEnabled = false;

            // So we do not get issues with thread blocking
            var version = VersionBox.Text;

            // Dump all DB2 metadata
            if (DumpAllDB2.IsChecked ?? false)
            {
                foreach (var (fileDataId, db2Name) in _listfileReader.GetAvailableDB2s())
                {
                    if (db2Name.Contains("internal"))
                        continue;

                    var normalizedDb2Name = db2Name.Replace("dbfilesclient/", "")
                        .Replace("DBFilesClient/", "")
                        .Replace(".db2", "");

                    var normalizedName = _db2DisplayNames.FirstOrDefault(x => x.Name == normalizedDb2Name);
                    if (normalizedName == null)
                    {
                        DumpLog.Content = $"Dumping {normalizedDb2Name} for build {VersionBox.Text} ...";
                        await DumpDB2MetaData(normalizedDb2Name, version, true, fileDataId);
                    }
                    else
                    {
                        DumpLog.Content = $"Dumping {normalizedName.DisplayName} for build {VersionBox.Text} ...";
                        await DumpDB2MetaData(normalizedName.DisplayName, version, true, fileDataId);
                    }
                }
            }
            else
            {
                var tableName = TableName.Text;
                if (_listfileReader.IsLoaded)
                    tableName = TableDropdown.SelectedItem as string;

                await DumpDB2MetaData(tableName, version, DumpToFile.IsChecked ?? false);
            }

            if ((DumpAllDB2.IsChecked ?? false) || (DumpToFile.IsChecked ?? false))
            {
                await using var metadataWriter = new StreamWriter("DB2Metadata.h");
                await using var structureWriter = new StreamWriter("DB2Structure.h");
                await using var loadInfoWriter = new StreamWriter("DB2LoadInfo.h");

                foreach (var (_, data) in _metadatas)
                    await metadataWriter.WriteLineAsync(data + "\n");

                foreach (var (_, data) in _structures)
                    await structureWriter.WriteLineAsync(data + "\n");

                foreach (var (_, data) in _loadInfos)
                    await loadInfoWriter.WriteLineAsync(data + "\n");

                await this.ShowMessageAsync("Dumper", "Finished dumping all structures and metadata!");
            }

            DumpButton.IsEnabled = true;
            VersionBox.IsEnabled = true;
            TableName.IsEnabled = true;
            TableDropdown.IsEnabled = true;
        }

        async Task DumpDB2MetaData(string? tableName, string? versionText, bool dumpToFile, uint fileDataId = 0)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                await this.ShowMessageAsync("Error", "Invalid table name");
                return;
            }

            if (string.IsNullOrEmpty(versionText))
            {
                await this.ShowMessageAsync("Error", "Please provide a valid build. Like 10.0.2.45969");
                return;
            }

            var array = versionText.Split('.');
            if (array.Length < 4)
            {
                await this.ShowMessageAsync("Error", "Please provide a valid build. Like 10.0.2.45969");
                return;
            }

            try
            {
                var dbdProvider = new GithubDbdProvider();
                var dbdStream = await dbdProvider.StreamForTableName(tableName);

                if (fileDataId == 0 && _listfileReader.IsLoaded)
                    fileDataId = _listfileReader.GetFileDataIdByEntry($"dbfilesclient/{tableName.ToLower()}.db2");

                var dbdInfo = DbdBuilder.Build(dbdStream, tableName, versionText, fileDataId);
                if (dbdInfo == null)
                    return;

                if (dumpToFile)
                {
                    _structures.Add((tableName, dbdInfo.DumpStructures()));
                    _metadatas.Add((tableName, dbdInfo.DumpMeta()));
                    _loadInfos.Add((tableName, dbdInfo.DumpLoadInfo()));
                }
                else
                {
                    DB2StructResult.Text = dbdInfo.DumpStructures();
                    DB2MetaResult.Text = dbdInfo.DumpMeta();
                    DB2LoadInfoResult.Text = dbdInfo.DumpLoadInfo();
                }
            }
            catch (Exception)
            {
                // _ = MessageBox.Show($"Couldn't retrieve data for {tableName}!\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // throw;
            }
        }

        void OpenPath_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            var dialog = new OpenFileDialog
            {
                DefaultExt = ".csv",
                Filter = "CSV Files (*.csv)|*.csv"
            };

            var result = dialog.ShowDialog() ?? false;
            if (result)
            {
                Task.Run(async () =>
                {
                    await Dispatcher.BeginInvoke(() =>
                    {
                        ListfilePath.Text = dialog.FileName;
                        DumpButton.IsEnabled = false;
                    });

                    // Load the listfile csv
                    await _listfileReader.ReadFileAsync(dialog.FileName);

                    // Populate the dropdown with db2 names
                    await Dispatcher.BeginInvoke(() =>
                    {
                        TableNameInputGrid.Visibility = Visibility.Hidden;
                        TableNameDropdownGrid.Visibility = Visibility.Visible;

                        var availableDatabases = _listfileReader.GetAvailableDB2s();
                        foreach (var db2 in availableDatabases)
                        {
                            if (!db2.Value.StartsWith("DBFilesClient/", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var tableName = db2.Value.Replace("dbfilesclient/", "")
                                .Replace("DBFilesClient/", "")
                                .Replace(".db2", "");

                            var normalizedName = _db2DisplayNames.FirstOrDefault(x => x.Name == tableName);
                            TableDropdown.Items.Add(normalizedName != null ? normalizedName.DisplayName : tableName);
                        }

                        DumpButton.IsEnabled = true;
                    });

                    await this.ShowMessageAsync("Listfile", "Finished loading listfile!");
                });
            }
        }

        async void LoadDisplayNames()
        {
            var fileStream = File.OpenRead("db2_display_names.json");

            var content = JsonSerializer.Deserialize<List<DB2DisplayNameRecord>>(fileStream);
            if (content == null)
            {
                await this.ShowMessageAsync("Error", "Failed to load DB2 Display Names");
                return;
            }

            _db2DisplayNames = content;
        }
    }

    public record DB2DisplayNameRecord(string Name, string DisplayName);
}
