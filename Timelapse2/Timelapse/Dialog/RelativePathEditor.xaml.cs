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
using DialogUpgradeFiles.Database;
using ToastNotifications.Position;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RelativePathEditor.xaml
    /// </summary>
    public partial class RelativePathEditor : Window
    {
        private List<string> RelativePathList = new List<string>
        {
            @"Sales\Training",
            @"Offices\Pune\HR",
            @"Offices\Pune\PMO",
            @"Offices\London\DEV\JAVA",
            @"Offices\London\DEV\DOTNET",
            @"Offices\London\QA",
            @"Offices\Mumbai",
            @"Finances",
            @"HR",
            @"Sales",
            @"Foo",
        };

        public RelativePathEditor(Window owner, Timelapse.Database.FileDatabase fileDatabase)
        {
            InitializeComponent();
            this.Owner = owner;

            this.RelativePathControl.FileDatabase = fileDatabase;
            this.RelativePathControl.ParentDialogWindow = this;
            if (fileDatabase != null)
            {
                this.RelativePathList = fileDatabase.GetRelativePaths();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.RelativePathControl.Initialize(this.RelativePathList);
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = this.RelativePathControl.WereEditsMade;
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.RefreshAsSortedRelativePaths();
        }
    }
}
