using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class ClassificationByTaxonomyDialog
    {
        #region Constructor and Initialization

        private readonly FileDatabase FileDatabase;
        private readonly UIElement _anchor;
        private readonly string InitialSelectedTaxon;

        public ClassificationByTaxonomyDialog(Window owner, FileDatabase fileDatabase, string selectedTaxonNode, UIElement anchor = null)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FileDatabase = fileDatabase;
            this._anchor = anchor;
            this.InitialSelectedTaxon = selectedTaxonNode;
            FormattedDialogHelper.SetupStaticReferenceResolver(DialogMessage);
        }

        private void ClassificationByTaxonomyDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dictionary<string, string> classificationDescriptions = null;
            Dictionary<string, string> classificationCategories = null;
            if (FileDatabase != null)
            {
                this.FileDatabase.CreateClassificationDescriptionsDictionaryIfNeeded();
                this.FileDatabase.CreateClassificationCategoriesDictionaryIfNeeded();
                classificationDescriptions = this.FileDatabase.classificationDescriptionsDictionary;
                classificationCategories = this.FileDatabase.classificationCategoriesDictionary;
            }

            if (classificationDescriptions != null && classificationCategories != null)
            {
                this.TreeViewSpeciesTaxonomy.Populate(classificationCategories, classificationDescriptions);
                this.TreeViewSpeciesTaxonomy.SelectionChanged += TreeViewSpeciesTaxonomySelectionChanged;
                this.TreeViewSpeciesTaxonomy.TrySelectAndRevealNode(this.InitialSelectedTaxon);
            }
            else
            {
                // Fall back to hardcoded test data (guid becomes the key, last segment the common name).
                this.TreeViewSpeciesTaxonomy.Populate([
                    "xxxx2069-a39c-4a84-a949-60044271c0c1;No taxonomy available;;;;;;"
                ]);
            }
            if (_anchor != null)
                PositionRelativeToAnchor();
            else
                Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.DialogMessage.BuildContentFromProperties();
        }

        private void PositionRelativeToAnchor()
        {
            // PointToScreen returns physical pixels; convert to WPF logical units via the DPI transform
            var source = PresentationSource.FromVisual(_anchor);
            var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;

            Point physicalOrigin = _anchor.PointToScreen(new Point(0, 0));
            Point logical = fromDevice.Transform(physicalOrigin);

            double anchorLeft   = logical.X;
            double anchorTop    = logical.Y;
            double anchorWidth  = (_anchor is FrameworkElement fe) ? fe.ActualWidth  : 0;
            double anchorBottom = logical.Y + ((_anchor is FrameworkElement fe2) ? fe2.ActualHeight : 0);

            Rect screen = SystemParameters.WorkArea;
            const double offset = 4;

            // Case 1: dialog's lower-left corner just above and to the left of the button's upper-left corner.
            double left = anchorLeft - offset;
            double top  = anchorTop - ActualHeight - offset;

            if (left + ActualWidth <= screen.Right)
            {
                Left = Math.Max(screen.Left, left);
                Top  = Math.Max(screen.Top,  top);
                return;
            }

            // Case 2: insufficient room to the right — center over button, top just below button bottom.
            // Shift left if needed until it fits horizontally.
            left = anchorLeft + anchorWidth / 2 - ActualWidth / 2;
            top  = anchorBottom;

            if (left + ActualWidth > screen.Right)
                left = screen.Right - ActualWidth;

            if (left >= screen.Left && left + ActualWidth <= screen.Right && top + ActualHeight <= screen.Bottom)
            {
                Left = left;
                Top  = top;
                return;
            }

            // Case 3: center within the owner (CustomSelection) dialog.
            Left = Owner != null
                ? Owner.Left + (Owner.ActualWidth  - ActualWidth)  / 2
                : screen.Left + (screen.Width - ActualWidth) / 2;
            Top  = Owner != null
                ? Owner.Top  + (Owner.ActualHeight - ActualHeight) / 2
                : screen.Top  + (screen.Height - ActualHeight) / 2;
        }

        private void TreeViewSpeciesTaxonomySelectionChanged(object sender, EventArgs e)
        {
            this.TreeViewSpeciesTaxonomy.DebugTextBox.Text = string.Join(Environment.NewLine, this.TreeViewSpeciesTaxonomy.SelectedClassifications);
        }

        private void BtnOkay_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion
    }
}
