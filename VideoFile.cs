using CommunityToolkit.Mvvm.ComponentModel;
using FFMpegCore;
using System;
using System.Collections.Generic;
using Windows.Storage;

namespace Magnesium;

public partial class VideoFile : ObservableObject
{
	[ObservableProperty]
	public partial string Name { get; set; }

	[ObservableProperty]
	public partial TimeSpan Duration { get; set; }

	[ObservableProperty]
	public partial string FullPath { get; set; }

	[ObservableProperty]
	public partial string ThumbnailPath { get; set; }

	[ObservableProperty]
	public partial string Extension { get; set; }

	[ObservableProperty]
	public partial string OriginalSize { get; set; }

	[ObservableProperty]
	public partial string NewSize { get; set; }

	[ObservableProperty]
	public partial string SavedPercentage { get; set; }

	[ObservableProperty]
	public partial double Fps { get; set; }

	[ObservableProperty]
	public partial int AudioTracks { get; set; }

	[ObservableProperty]
	public partial DateTime DateCreated { get; set; }

	[ObservableProperty]
	public partial DateTime DateModified { get; set; }

	[ObservableProperty]
	public partial string Status { get; set; } 

	[ObservableProperty]
	public partial bool IsConverting { get; set; }
}