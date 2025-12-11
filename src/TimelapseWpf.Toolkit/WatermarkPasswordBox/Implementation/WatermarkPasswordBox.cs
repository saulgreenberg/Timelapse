/*************************************************************************************
   
   Toolkit for WPF
   Copyright (C) 2007-2019 Xceed Software Inc.
   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/license/ms-pl-html

   Fork origin: https://github.com/dotnetprojects/WpfExtendedToolkit
   - based on: https://github.com/xceedsoftware/wpftoolkit, Version 3
   This fork: modified for use in Timelapse project
    by Saul Greenberg, 2025 onwards

  ***********************************************************************************/

using System.Security;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TimelapseWpf.Toolkit
{
  public class WatermarkPasswordBox : WatermarkTextBox
  {
    #region Members

    private int _newCaretIndex = -1;

    #endregion

    #region Properties

    #region Password

    public string Password
    {
      [SecuritySafeCritical]
      get
      {
        string passwordString;
        var valuePtr = Marshal.SecureStringToBSTR( this.SecurePassword );
        try
        {
          passwordString = Marshal.PtrToStringUni( valuePtr );
        }
        finally
        {
          Marshal.ZeroFreeBSTR( valuePtr );
        }
        return passwordString;
      }
      set
      {
        value ??= string.Empty;
        this.SecurePassword = new();
        foreach (var t in value)
        {
          this.SecurePassword.AppendChar( t );
        }

        // Internal changes to Password property will have a _newCaretIndex > 0.
        this.SyncTextPassword( _newCaretIndex );

        this.RaiseEvent( new( PasswordChangedEvent, this ) );
      }
    }

    #endregion

    #region PasswordChar

    public static readonly DependencyProperty PasswordCharProperty = DependencyProperty.Register( nameof(PasswordChar), typeof( char ), typeof( WatermarkPasswordBox )
      , new UIPropertyMetadata( '\u25CF', OnPasswordCharChanged ) ); //default is black bullet

    public char PasswordChar
    {
      get => (char)GetValue( PasswordCharProperty );

      set => SetValue( PasswordCharProperty, value );
    }

    private static void OnPasswordCharChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is WatermarkPasswordBox watermarkPasswordBox )
      {
        watermarkPasswordBox.OnPasswordCharChanged( (char)e.OldValue, (char)e.NewValue );
      }
    }

    protected virtual void OnPasswordCharChanged( char oldValue, char newValue )
    {
      this.SyncTextPassword( this.CaretIndex );
    }

    #endregion

    #region SecurePassword

    public SecureString SecurePassword
    {
      get;
      private set;
    }

    #endregion SecurePassword

    #endregion //Properties

    #region Constructors

    public WatermarkPasswordBox()
    {
      this.Password = string.Empty;
      this.IsUndoEnabled = false;
      this.UndoLimit = 0;

      CommandManager.AddPreviewCanExecuteHandler( this, OnPreviewCanExecuteCommand );
      DataObject.AddPastingHandler( this, OnPaste );
    }

    #endregion //Constructors

    #region Base Class Overrides

    [SecuritySafeCritical]
    protected override void OnPreviewTextInput( TextCompositionEventArgs e )
    {
      this.PasswordInsert( e.Text, this.CaretIndex );

      e.Handled = true; //Handle to prevent TextChanged when OnPreviewTextInput exist

      base.OnPreviewTextInput( e );
    }

    [SecuritySafeCritical]
    protected override void OnPreviewKeyDown( KeyEventArgs e )
    {
      // Keys not detected by OnPreviewTextInput
      switch( e.Key )
      {
        case Key.Space:
          this.PasswordInsert( " ", this.CaretIndex );
          e.Handled = true;  //Handle to prevent TextChanged when OnPreviewKeyDown exist
          break;
        case Key.Back:
          // With a selection, delete from CaretIndex. Without a selection delete the character before the CaretIndex.
          this.PasswordRemove( (this.SelectedText.Length > 0) ? this.CaretIndex : this.CaretIndex - 1 );
          e.Handled = true;  //Handle to prevent TextChanged when OnPreviewKeyDown exists
          break;
        case Key.Delete:
          this.PasswordRemove( this.CaretIndex );
          e.Handled = true;  //Handle to prevent TextChanged when OnPreviewKeyDown exist
          break;
        case Key.V:
          if( (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control )
          {
            if( Clipboard.ContainsText() )
            {
              this.PasswordInsert( Clipboard.GetText(), this.CaretIndex );
              e.Handled = true; //Handle to prevent TextChanged when OnPreviewKeyDown exist
            }
          }
          break;
        case Key.Enter:
          if( this.AcceptsReturn )
          {
            // Add input because it's not added by default.
            this.PasswordInsert( "\r", this.CaretIndex );
          }
          // Do not add input when AcceptReturn is False.
          e.Handled = true;  //Handle to prevent TextChanged when OnPreviewKeyDown exist
          break;
        case Key.Escape:
          e.Handled = true;  //Handle to prevent TextChanged when OnPreviewKeyDown exist
          break;
      }

      base.OnPreviewKeyDown( e );
    }

    protected override void OnTextChanged( TextChangedEventArgs e )
    {
      base.OnTextChanged( e );

      if( this.Text.Length != this.Password.Length )
      {
        // When Clear() or Cut() methods are called, we need to update the Password property to empty.
        if( this.Text == "" )
        {
          this.SetPassword( "", 0 );
        }
        // When AppendText() method is called, we need to reset the Text property to prevent adding text.
        else
        {
          this.SyncTextPassword( this.Password.Length );
        }
      }
    }




    #endregion //Base Class Overrides

    #region Event

    public static readonly RoutedEvent PasswordChangedEvent = EventManager.RegisterRoutedEvent( "PasswordChanged", RoutingStrategy.Bubble, typeof( RoutedEventHandler )
      , typeof( WatermarkPasswordBox ) );
    public event RoutedEventHandler PasswordChanged
    {
      add => AddHandler( PasswordChangedEvent, value );
      remove => RemoveHandler( PasswordChangedEvent, value );
    }

    #endregion

    #region Event Handlers

    [ SecuritySafeCritical]
    private void OnPaste( object sender, DataObjectPastingEventArgs e )
    {
      //Pasting something that is not text
      if( !e.SourceDataObject.GetDataPresent( DataFormats.UnicodeText, true ) )
        return;

      if( e.SourceDataObject.GetData( DataFormats.UnicodeText ) is string text )
      {
        this.PasswordInsert( text, this.CaretIndex );
      }
      e.CancelCommand(); //Cancel to prevent TextChanged
    }

    private void OnPreviewCanExecuteCommand( object sender, CanExecuteRoutedEventArgs e )
    {
      //Will not execute these actions
      if( e.Command == ApplicationCommands.Copy ||
          e.Command == ApplicationCommands.Cut ||
          e.Command == ApplicationCommands.Undo )
      {
        e.CanExecute = false;
        e.Handled = true;  //Handle to prevent actions
      }
    }

    #endregion

    #region Private Methods

    [SecurityCritical]
    private void PasswordInsert( string text, int index )
    {
      if( text == null )
        return;
      if( (index < 0) || (index > this.Password.Length) )
        return;

      //If there is a selection, remove it first
      if( this.SelectedText.Length > 0 )
      {
        this.PasswordRemove( index );
      }

      var newPassword = this.Password;
      foreach (var t in text)
      {
        // MaxLength == 0 is no limit
        if( (this.MaxLength == 0) || (newPassword.Length < this.MaxLength) )
        {
          newPassword = newPassword.Insert( index++, t.ToString() );
        }
      }
      this.SetPassword( newPassword, index );
    }

    [SecurityCritical]
    private void PasswordRemove( int index )
    {
      if( (index < 0) || (index >= this.Password.Length) )
        return;

      if( this.SelectedText.Length > 0 )
      {
        var newPassword = this.Password;
        for( int i = 0; i < this.SelectedText.Length; ++i )
        {
          newPassword = newPassword.Remove( index, 1 );          
        }
        this.SetPassword( newPassword, index );
      }
      else
      {
        var newPassword = this.Password.Remove( index, 1 );
        this.SetPassword( newPassword, index );
      }
    }

    private void SetPassword( string password, int caretIndex )
    {
      _newCaretIndex = caretIndex;
      this.Password = password;
      _newCaretIndex = -1;
    }

    private void SyncTextPassword( int nextCarretIndex )
    {
      var sb = new StringBuilder();
      this.Text = sb.Append( Enumerable.Repeat(this.PasswordChar, this.Password.Length).ToArray() ).ToString();
      //set CaretIndex after Text is changed
      this.CaretIndex = Math.Max( nextCarretIndex, 0 );
    }

    #endregion
  }
}
