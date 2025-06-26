using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Timelapse.DataStructures;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class TestSomeCodeDialog
    {
        public TestSomeCodeDialog(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        private void TestSomeCodeDialog_OnLoaded(object sender, RoutedEventArgs e)
        {

        }

        private void Go_OnClick(object sender, RoutedEventArgs e)
        {
           // ParseDescriptionsIntoTaxa(GlobalReferences.MainWindow.DataHandler.FileDatabase.classificationDescriptionsDictionary);
        }

        //private void ParseDescriptionsIntoTaxa(Dictionary<string,string> descriptions)
        //{
        //    // Each description has the form "429257d4-3ef2-47fb-b849-66ee6c107346;mammalia;cetartiodactyla;cervidae;alces;alces;moose", 
        //    // 1st field: GUID
        //    // 2nd-6th fields: Taxa as: Class;Order;Family;Genus;Species
        //    // 7th field: common name

        //    // Split the descriptions
        //    foreach (KeyValuePair<string, string> kvp in descriptions)
        //    {
        //        if (string.IsNullOrEmpty(kvp.Value))
        //        {
        //            ShowList.Items.Add("oops Empty description");
        //            return;
        //        }

        //        string[] parts = kvp.Value.Split(';');
        //        if (parts.Length != 7)
        //        {
        //            ShowList.Items.Add("oops not enought parts" + parts.Length);
        //            return;
        //        }

        //        if (parts[0].Length != 36)
        //        {
        //            ShowList.Items.Add("oops GUID malformed" + parts[0].Length);
        //            return;
        //        }

        //        // Parse the description
        //        List<string> taxa = new List<string>();
        //        string commonName = string.Empty;
        //        for (int i = 0; i < parts.Length; i++)
        //        {
        //            if (i == 0)
        //            {
        //                // ignore the guid
        //                continue;
        //            }

        //            if (i == parts.Length - 1)
        //            {
        //                // The common name is the last element
        //                commonName = parts[i];
        //                continue;
        //            }

        //            taxa.Add(parts[i]);
        //        }

        //        // Display the results
        //        string indent = "";
        //        ShowList.Items.Add($"Common name: {commonName}");
        //        foreach (string part in taxa)
        //        {
        //            if (string.IsNullOrEmpty(part))
        //            {
        //                indent += ".";
        //                continue;
        //            }
        //            ShowList.Items.Add($"{indent}{part}");
        //            indent += ".";
        //        }
        //    }
        //}
    }
}
