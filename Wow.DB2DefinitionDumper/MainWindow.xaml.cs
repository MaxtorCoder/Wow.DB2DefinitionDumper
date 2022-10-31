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

        public MainWindow()
        {
            InitializeComponent();

            LoadDisplayNames();

            ToolBar.MouseLeftButtonDown += (e, o) => DragMove();
            CloseBtn.Click += (e, o) => Close();
        }

        private async void DumpButton_Click(object sender, RoutedEventArgs e)
        {
            var tableName = TableName.Text;
            if (_listfileReader.IsLoaded)
                tableName = TableDropdown.SelectedItem as string;

            var normalizedName = _db2DisplayNames.FirstOrDefault(x => x.displayName == tableName);
            if (normalizedName == null)
            {
                _ = MessageBox.Show($"Failed to find normalized name for {tableName}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(normalizedName.displayName))
            {
                _ = MessageBox.Show("Invalid table name", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var version = VersionBox.Text;
            if (string.IsNullOrEmpty(version))
            {
                _ = MessageBox.Show("Please provide a valid build. Like 10.0.2.45969", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var array = version.Split('.');
            if (array.Length < 4)
            {
                _ = MessageBox.Show("Please provide a valid build. Like 10.0.2.45969", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var dbdProvider = new GithubDbdProvider();
                var dbdStream = await dbdProvider.StreamForTableName(normalizedName.displayName);

                var fileDataId = 0u;
                if (_listfileReader.IsLoaded)
                    fileDataId = _listfileReader.GetFileDataIdByEntry($"dbfilesclient/{normalizedName.name}.db2");

                var dbdInfo = DbdBuilder.Build(dbdStream, normalizedName.displayName, version, fileDataId);

                DB2StructResult.Text = dbdInfo.GetColumns();
                DB2MetaResult.Text = dbdInfo.DumpMeta();
            }
            catch
            {
                _ = MessageBox.Show($"Couldn't retrieve data for {normalizedName.displayName}!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
