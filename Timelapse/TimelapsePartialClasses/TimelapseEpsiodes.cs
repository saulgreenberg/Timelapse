﻿using System;
using System.Windows;
using System.Windows.Media;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        #region Private Methods - Episodes - Display Episode Information
        /// <summary>
        /// Get and display the episode text if various conditions are met
        /// </summary>
        private void DisplayEpisodeTextInImageIfWarranted(int fileIndex)
        {
            if (Episodes.Episodes.ShowEpisodes && this.IsDisplayingSingleImage())
            {
                if (Episodes.Episodes.EpisodesDictionary.ContainsKey(fileIndex) == false)
                {
                    Episodes.Episodes.EpisodeGetEpisodesInRange(this.DataHandler.FileDatabase.FileTable, this.DataHandler.ImageCache.CurrentRow);
                }
                Tuple<int, int> episode = Episodes.Episodes.EpisodesDictionary[fileIndex];
                if (episode.Item1 == int.MaxValue)
                {
                    this.EpisodeText.Text = "Episode \u221E";
                }
                else
                {
                    this.EpisodeText.Text = (episode.Item2 == 1) ? "Single" : $"Episode {episode.Item1}/{episode.Item2}";
                }
                this.EpisodeText.Foreground = (episode.Item1 == 1) ? Brushes.Red : Brushes.Black;
                this.EpisodeText.Visibility = Visibility.Visible;
            }
            else
            {
                this.EpisodeText.Visibility = Visibility.Hidden;
            }
        }
        #endregion
    }
}
