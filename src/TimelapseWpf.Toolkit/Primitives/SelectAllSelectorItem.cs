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


namespace TimelapseWpf.Toolkit.Primitives
{
  public class SelectAllSelectorItem : SelectorItem
  {
    #region Members

    private bool _ignoreSelectorChanges;

    #endregion

    #region Overrides

    // Do not raise an event when this item is Selected/UnSelected.
    protected override void OnIsSelectedChanged( bool? oldValue, bool? newValue )
    {
      if( _ignoreSelectorChanges )
        return;

      if( this.TemplatedParent is SelectAllSelector templatedParent )
      {
        if( newValue.HasValue )
        {
          // Select All
          if( newValue.Value )
          {
            templatedParent.SelectAll();
          }
          // UnSelect All
          else
          {
            templatedParent.UnSelectAll();
          }
        }
      }
    }

    #endregion

    #region Internal Methods

    internal void ModifyCurrentSelection( bool? newSelection )
    {
      _ignoreSelectorChanges = true;
      this.IsSelected = newSelection;
      _ignoreSelectorChanges = false;
    }

    #endregion
  }
}
