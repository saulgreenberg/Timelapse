using System;
using System.Data;
using System.IO;
using DialogUpgradeFiles.Enums;
using DialogUpgradeFiles.Extensions;
using DialogUpgradeFiles.Util;

namespace DialogUpgradeFiles.DataTables
{
    public class FileTable : DataTableBackedList<ImageRow>
    {
        #region Constructors
        public FileTable(DataTable filesDataTable)
            : base(filesDataTable, FileTable.CreateRow)
        {
        }
        #endregion

        // Return a new image or video row
        private static ImageRow CreateRow(DataRow row)
        {
            // Return a image row or video row if its an image or video file respectively (as identified by its suffix)
            switch (FilesFolders.GetFileTypeByItsExtension(row.GetStringField(Constant.DatabaseColumn.File)))
            {
                case FileExtensionEnum.IsImage:
                    return new ImageRow(row);
                case FileExtensionEnum.IsVideo:
                    return new VideoRow(row);
                case FileExtensionEnum.IsNotImageOrVideo:
                default:
                    // This should never be reached
                    throw new NotSupportedException(
                        $"Unhandled extension for file '{row.GetStringField(Constant.DatabaseColumn.File)}'.");
            }
        }

        public ImageRow NewRow(FileInfo file)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(file, nameof(file));

            DataRow row = this.DataTable.NewRow();
            row[Constant.DatabaseColumn.File] = file.Name;
            return FileTable.CreateRow(row);
        }
    }
}
