using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace Magnesium;

public partial class VideoFile : ObservableObject
{
	[ObservableProperty]
	private string name;

	[ObservableProperty]
	private string fullPath;

	[ObservableProperty]
	private string extension;

	[ObservableProperty]
	private string originalSize;

	[ObservableProperty]
	private string newSize = "N/A";

	[ObservableProperty]
	private string savedPercentage = "N/A";

	[ObservableProperty]
	private double fps;

	[ObservableProperty]
	private int audioTracks;

	[ObservableProperty]
	private DateTime dateCreated;

	[ObservableProperty]
	private DateTime dateModified;

	[ObservableProperty]
	private string status = "Pending"; 

	[ObservableProperty]
	private bool isConverting;
}