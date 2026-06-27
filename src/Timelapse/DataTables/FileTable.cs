using System;
using System.Data;
using System.IO;
using Timelapse.Constant;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Util;

namespace Timelapse.DataTables
{
    public class FileTable(DataTable filesDataTable) : DataTableBackedList<ImageRow>(filesDataTable, CreateRow)
    {
        // Return a new image or video row
        private static ImageRow CreateRow(DataRow row)
        {
            // Return a image row or video row if its an image or video file respectively (as identified by its suffix)
            switch (FilesFolders.GetFileTypeByItsExtension(row.GetStringField(DatabaseColumn.File)))
            {
                case FileExtensionEnum.IsImage:
                    return new(row);
                case FileExtensionEnum.IsVideo:
                    return new VideoRow(row);
                case FileExtensionEnum.IsNotImageOrVideo:
                default:
                    // This should never be reached
                    throw new NotSupportedException(
                        $"Unhandled extension for file '{row.GetStringField(DatabaseColumn.File)}'.");
            }
        }

        public ImageRow NewRow(FileInfo file)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(file, nameof(file));

            DataRow row = DataTable.NewRow();
            row[DatabaseColumn.File] = file.Name;
            return CreateRow(row);
        }
    }
}
