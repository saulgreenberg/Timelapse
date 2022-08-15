using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace Timelapse.Controls
{
    /// <summary>
    /// Implements an autocomplete textbox that retains memory of what was entered before
    /// and shows autocomplete predictions based on initial text typed into it
    /// </summary>
    public class AutocompleteTextBox : TextBox
    {
        #region Public Properties
        // XamlWriter doesn't support generics so this property breaks anything triggering XamlWriter.Save(), such as clearing UI object collections
        // containing the text box since the clear triggers undo and undo relies on serialization.
        // If needed serialization support can be added via a TypeConverter.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        // Autocompletion is implemented as a dictionary rather than as a list because its an easier way
        // to ensure that no duplicate entries are added (as keys). 
        // Values are always null. It works better than lists, where we would have to check the list to see if each entry was in it before adding it.
        // Yes, its a hack but a reasonable one
        public Dictionary<string, string> Autocompletions { get; set; }
        #endregion

        #region Public Events
        /// <summary>
        /// Since auto-completion hooks the TextChanged event provide a follow on event to callers as event sequencing can be fragile.
        /// </summary>
        public event Action<object, TextChangedEventArgs> TextAutocompleted;
        #endregion

        #region Private variables
        private string mostRecentAutocompletion;
        #endregion

        #region Constructore
        public AutocompleteTextBox()
        {
            this.mostRecentAutocompletion = null;
            this.TextChanged += this.OnTextChanged;
        }
        #endregion

        #region Private (internal) methods including Event Callbacks
        private void OnTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            // Don't allow leading whitespace
            // Updating the text box moves the caret to the start position, which results in poor user experience when the text box initially contains only
            // whitespace and the user happens to move focus to the control in such a way that the first non-whitespace character entered follows some of the
            // whitespace---the result's the first character of the word ends up at the end rather than at the beginning.
            int cursorPosition = this.CaretIndex;
            string trimmedNote = this.Text.TrimStart();
            if (trimmedNote != this.Text)
            {
                cursorPosition -= this.Text.Length - trimmedNote.Length;
                if (cursorPosition < 0)
                {
                    cursorPosition = 0;
                }

                this.Text = trimmedNote;
                this.CaretIndex = cursorPosition;
            }

            // check if autocompletion is possible when text is added
            // Don't attempt autocompletion on pure removals, such as backspace or delete, but do try when both add and remove changes are present as this
            // usually indicates the user's typing over the autocomplete suggestion.
            // Also, if the caret is at the beginning(i.e., either editing an empty cell or just a cell update by navigating), then don't autocomplete 
            if ((String.IsNullOrEmpty(this.Text) == false) && (this.CaretIndex > 0) &&
                eventArgs.Changes.Any(change => change.AddedLength > 0))
            {
                int textLength = this.Text.Length;
                string autocompletion = null;
                if (this.UseCompletion(this.mostRecentAutocompletion))
                {
                    // prefer the most recently used completion over others
                    // This tends to alleviate users' data entry effort as usually the data entered for the last file is more likely appropriate than the first
                    // hit found in the completions collection.
                    autocompletion = this.mostRecentAutocompletion;
                }
                else if (this.Autocompletions != null)
                {
                    autocompletion = this.Autocompletions.Keys.FirstOrDefault(value => this.UseCompletion(value));
                }

                if (String.IsNullOrEmpty(autocompletion) == false)
                {
                    this.Text = autocompletion;
                    this.CaretIndex = textLength;
                    this.SelectionStart = textLength;
                    this.SelectionLength = autocompletion.Length - textLength;

                    this.mostRecentAutocompletion = autocompletion;
                }
            }

            // synchronize tooltip with content
            this.ToolTip = this.Text;

            // fire follow on event
            this.TextAutocompleted?.Invoke(this, eventArgs);
        }

        private bool UseCompletion(string completion)
        {
            int textLength = this.Text.Length;
            if (completion != null && completion.Length >= textLength && completion.Substring(0, textLength).Equals(this.Text, StringComparison.Ordinal))
            {
                return true;
            }
            return false;
        }
        #endregion
    }
}
