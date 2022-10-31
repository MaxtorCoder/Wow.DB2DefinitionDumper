using System.IO;
using System.Text.Json;
using System.Windows;

using MahApps.Metro.Controls;
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
        private readonly ListfileReader _listfileReader = new();

        private List<DB2DisplayNameRecord> _db2DisplayNames = new();

        private List<(string, string)> _metadatas = new();
        private List<(string, string)> _structures = new();

        public MainWindow()
        {
            InitializeComponent();

            LoadDisplayNames();

            ToolBar.MouseLeftButtonDown += (e, o) => DragMove();
            CloseBtn.Click += (e, o) => Close();
            MinimizeBtn.Click += (e, o) => WindowState = WindowState.Minimized;
        }

        private async void DumpButton_Click(object sender, RoutedEventArgs e)
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
                        .Replace(".db2", "");

                    var normalizedName = _db2DisplayNames.FirstOrDefault(x => x.name == normalizedDb2Name);
                    if (normalizedName == null)
                        continue;

                    DumpLog.Content = $"Dumping {normalizedName.displayName} for build {VersionBox.Text} ...";
                    await DumpDB2MetaData(normalizedName.displayName, version, true, fileDataId);
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
                using var metadataWriter = new StreamWriter("DB2Metadata.h");
                using var structureWriter = new StreamWriter("DB2Structure.h");

                foreach (var (_, data) in _metadatas)
                    await metadataWriter.WriteLineAsync(data + "\n");

                foreach (var (_, data) in _structures)
                    await structureWriter.WriteLineAsync(data);

                _ = MessageBox.Show("Finished dumping all structures and metadata!");
            }

            DumpButton.IsEnabled = true;
            VersionBox.IsEnabled = true;
            TableName.IsEnabled = true;
            TableDropdown.IsEnabled = true;
        }

        private async Task DumpDB2MetaData(string? tableName, string? versionText, bool dumpToFile, uint fileDataId = 0)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                _ = MessageBox.Show("Invalid table name", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(versionText))
            {
                _ = MessageBox.Show("Please provide a valid build. Like 10.0.2.45969", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var array = versionText.Split('.');
            if (array.Length < 4)
            {
                _ = MessageBox.Show("Please provide a valid build. Like 10.0.2.45969", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    _structures.Add((tableName, dbdInfo.GetColumns()));
                    _metadatas.Add((tableName, dbdInfo.DumpMeta()));
                }
                else
                {
                    DB2StructResult.Text = dbdInfo.GetColumns();
                    DB2MetaResult.Text = dbdInfo.DumpMeta();
                }
            }
            catch (Exception ex)
            {
                // _ = MessageBox.Show($"Couldn't retrieve data for {tableName}!\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // throw;
            }
        }

        private void OpenPath_Click(object sender, RoutedEventArgs e)
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

                        var db2s = _listfileReader.GetAvailableDB2s();
                        foreach (var db2 in db2s)
                        {
                            var tableName = db2.Value.Replace("dbfilesclient/", "").Replace(".db2", "");

                            var normalizedName = _db2DisplayNames.FirstOrDefault(x => x.name == tableName);
                            if (normalizedName != null)
                                TableDropdown.Items.Add(normalizedName.displayName);
                        }

                        DumpButton.IsEnabled = true;
                    });

                    _ = MessageBox.Show("Finished loading listfile!");
                });
            }
        }

        private void LoadDisplayNames()
        {
            var fileStream = File.OpenRead("db2_display_names.json");

            var content = JsonSerializer.Deserialize<List<DB2DisplayNameRecord>>(fileStream);
            if (content == null)
            {
                _ = MessageBox.Show("Failed to load DB2 Display Names", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _db2DisplayNames = content;
        }
    }

    public record DB2DisplayNameRecord(string name, string displayName);
}
