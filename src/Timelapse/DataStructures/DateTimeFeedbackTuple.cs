namespace Timelapse.DataStructures
{
    /// <summary>
    /// Used by various DateTime Dialogs to store a particular file name with a feedback message 
    /// </summary>
    public class DateTimeFeedbackTuple(string fileName, string message)
    {
        #region Public Properties
        public string FileName { get; set; } = fileName;
        public string Message { get; set; } = message;

        #endregion
    }
}
