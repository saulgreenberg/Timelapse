using System.Collections.Generic;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class TestSomeCodeDialog
    {
        #region Constructor and Initialization

        private readonly FileDatabase FileDatabase;
        public TestSomeCodeDialog(TimelapseWindow owner)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FileDatabase =  owner?.DataHandler?.FileDatabase;
            FormattedDialogHelper.SetupStaticReferenceResolver(TestMessage);
        }

        private void TestSomeCodeDialog_OnLoaded(object sender, RoutedEventArgs e)
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
                // Use real data: common names come from classificationCategories,
                // taxonomy paths come from classificationDescriptions (linked by the same key).
                this.SpecieNetTree.Populate(classificationCategories, classificationDescriptions);
            }
            else
            {
                // Fall back to hardcoded test data (guid becomes the key, last segment the common name).
                this.SpecieNetTree.Populate(new[]
                {
                    "429257d4-3ef2-47fb-b849-66ee6c107346;mammalia;cetartiodactyla;cervidae;alces;alces;moose",
                    "b1352069-a39c-4a84-a949-60044271c0c1;aves;;;;;birds",
                    "ba76d46e-25de-45e2-90a8-bd279b650f7c;mammalia;carnivora;felidae;lynx;rufus;bobcat",
                    "1f689929-883d-4dae-958c-3d57ab5b6c16;;;;;;animal",
                    "c5ce946f-8f0d-4379-992b-cc0982381f5e;mammalia;cetartiodactyla;cervidae;cervus;canadensis;elk",
                    "f1856211-cfb7-4a5b-9158-c0f72fd09ee6;;;;;;blank",
                    "78761f5e-64a3-46eb-b4f1-c966cc1ce630;mammalia;carnivora;canidae;vulpes;;vulpes species",
                    "ac0e8ba7-7261-4d17-8645-11ed3d02165a;mammalia;carnivora;canidae;vulpes;vulpes;red fox",
                    "f2d233e3-80e3-433d-9687-e29ecc7a467a;mammalia;;;;;mammal",
                    "0f2e2c41-f1bb-4cdd-8e97-ba7cffba3e86;mammalia;lagomorpha;leporidae;lepus;americanus;snowshoe hare",
                    "aaf3b049-36e6-46dd-9a07-8a580e9618b7;mammalia;carnivora;canidae;canis;latrans;coyote",
                    "a21c3699-864e-432c-a2e1-cbb016d81158;mammalia;carnivora;felidae;lynx;;lynx species",
                    "aca65aaa-8c6d-4b69-94de-842b08b13bd6;mammalia;cetartiodactyla;bovidae;bos;taurus;domestic cattle",
                    "e2895ed5-780b-48f6-8a11-9e27cb594511;;;;;;vehicle",
                });
            }

            //throw (new NullReferenceException());
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.TestMessage.BuildContentFromProperties();
        }
        #endregion
    }
}


