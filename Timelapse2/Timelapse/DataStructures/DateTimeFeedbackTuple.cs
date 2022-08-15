namespace Timelapse.Dialog
{
    /// <summary>
    /// Used by various DateTime Dialogs to store a particular file name with a feedback message 
    /// </summary>
    public class DateTimeFeedbackTuple
    {
        #region Public Properties
        public string FileName { get; set; }
        public string Message { get; set; }
        #endregion

        #region Constructor
        public DateTimeFeedbackTuple(string fileName, string message)
        {
            this.FileName = fileName;
            this.Message = message;
        }
        #endregion
    }
}
