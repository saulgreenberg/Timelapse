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

using System;
using System.Windows;
using System.ComponentModel;
using System.Diagnostics;

namespace TimelapseWpf.Toolkit.Core
{
  public class VersionResourceDictionary : ResourceDictionary, ISupportInitialize
  {
    private int _initializingCount;
    private string _assemblyName;
    private string _sourcePath;


    public VersionResourceDictionary() { }

    public VersionResourceDictionary(string assemblyName, string sourcePath)
    {
      ( ( ISupportInitialize )this ).BeginInit();
      this.AssemblyName = assemblyName;
      this.SourcePath = sourcePath;
      ( ( ISupportInitialize )this ).EndInit();
    }

    public string AssemblyName
    {
      get => _assemblyName;
      set 
      {
        this.EnsureInitialization();
        _assemblyName = value; 
      }
    }

    public string SourcePath
    {
      get => _sourcePath;
      set 
      {
        this.EnsureInitialization();
        _sourcePath = value; 
      }
    }

    private void EnsureInitialization()
    {
      if( _initializingCount <= 0 )
        throw new InvalidOperationException( "VersionResourceDictionary properties can only be set while initializing." );
    }

    void ISupportInitialize.BeginInit()
    {
      base.BeginInit();
      _initializingCount++;
    }

    void ISupportInitialize.EndInit()
    {
      _initializingCount--;
      Debug.Assert( _initializingCount >= 0 );

      if( _initializingCount <= 0 )
      {
        if( this.Source != null )
          throw new InvalidOperationException( "Source property cannot be initialized on the VersionResourceDictionary" );

        if( string.IsNullOrEmpty( this.AssemblyName ) || string.IsNullOrEmpty( this.SourcePath ) )
          throw new InvalidOperationException( "AssemblyName and SourcePath must be set during initialization" );

        //Using an absolute path is necessary in VS2015 for themes different than Windows 8.
        string uriStr = $@"pack://application:,,,/{this.AssemblyName};component/{this.SourcePath}";
        this.Source = new( uriStr, UriKind.Absolute );
      }

      base.EndInit();
    }
  }
}
