using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wow.DB2DefinitionDumper.DBD;
using Wow.DB2DefinitionDumper.Providers;

namespace Wow.DB2DefinitionDumper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            ToolBar.MouseLeftButtonDown += (e, o) => DragMove();
            CloseBtn.Click += (e, o) => Close();
        }

        private async void DumpButton_Click(object sender, RoutedEventArgs e)
        {
            var tableName = TableName.Text;
            if (string.IsNullOrEmpty(tableName))
            {
                ResultBox.Text = "Invalid table name";
                return;
            }

            var version = VersionBox.Text;
            if (string.IsNullOrEmpty(version))
            {
                ResultBox.Text = "Please provide a valid build. Like 10.0.2.45969";
                return;
            }

            string[] array = version.Split(new char[1] { '.' });
            if (array.Count() < 4)
            {
                ResultBox.Text = "Please provide a valid build. Like 10.0.2.45969";
                return;
            }

            try
            {

                var dbdProvider = new GithubDbdProvider();
                var dbdStream = await dbdProvider.StreamForTableName(tableName);

                var dbdInfo = DbdBuilder.Build(dbdStream, tableName, version);
                ResultBox.Text = dbdInfo.GetColumns() + Environment.NewLine + dbdInfo.DumpMeta();
            } catch (Exception ex)
            {
                ResultBox.Text = $"Couldn't retrieve data for {tableName}!";
            }
        }
    }
}
