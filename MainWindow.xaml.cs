// MainWindow.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using System.Collections.Generic;

namespace Magnesium;

public sealed partial class MainWindow : Window
{
	public ObservableCollection<VideoFile> VideoFiles { get; set; } = new();
	private string _outputFolder = null; // null означає "та сама тека"

	public MainWindow()
	{
		this.InitializeComponent();
		Title = "Magnesium Video Compressor";
		ExtendsContentIntoTitleBar = true;
		SetTitleBar(null);
	}

	// Дозволяємо перетягування файлів у вікно
	private void FileListView_DragEnter(object sender, DragEventArgs e)
	{
		e.AcceptedOperation = DataPackageOperation.Link;
	}

	// Обробка файлів, які перетягнули
	private async void FileListView_Drop(object sender, DragEventArgs e)
	{
		if (e.DataView.Contains(StandardDataFormats.StorageItems))
		{
			var items = await e.DataView.GetStorageItemsAsync();
			if (items.Count > 0)
			{
				await AddFilesToList(items);
			}
		}
	}

	// Рекурсивна функція для додавання файлів
	private async Task AddFilesToList(IReadOnlyList<IStorageItem> items)
	{
		foreach (var item in items)
		{
			if (item is StorageFile file)
			{
				if (!VideoFiles.Any(vf => vf.FullPath == file.Path))
				{
					var videoFile = await GetVideoMetadata(file);
					if (videoFile != null)
					{
						VideoFiles.Add(videoFile);
					}
				}
			}
			else if (item is StorageFolder folder)
			{
				await AddFilesToList(await folder.GetItemsAsync());
			}
		}
	}

	// Отримання метаданих за допомогою FFprobe
	private async Task<VideoFile> GetVideoMetadata(StorageFile file)
	{
		try
		{
			var mediaInfo = await FFProbe.AnalyseAsync(file.Path);
			var videoStream = mediaInfo.VideoStreams.FirstOrDefault();

			if (videoStream == null) return null; // Це не відеофайл

			var props = await file.GetBasicPropertiesAsync();

			return new VideoFile
			{
				Name = file.Name,
				FullPath = file.Path,
				Extension = file.FileType,
				OriginalSize = FormatBytes(props.Size),
				Fps = Math.Round(videoStream.AvgFrameRate, 2),
				AudioTracks = mediaInfo.AudioStreams.Count(),
				DateCreated = file.DateCreated.DateTime,
				DateModified = props.DateModified.DateTime
			};
		}
		catch (Exception ex)
		{
			// Тут можна додати логування помилок
			System.Diagnostics.Debug.WriteLine($"Error getting metadata for {file.Name}: {ex.Message}");
			return null;
		}
	}

	// Конвертер байтів у читабельний формат
	private static string FormatBytes(ulong bytes)
	{
		string[] suffix = { "B", "KB", "MB", "GB", "TB" };
		int i;
		double dblSByte = bytes;
		for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
		{
			dblSByte = bytes / 1024.0;
		}
		return $"{dblSByte:0.##} {suffix[i]}";
	}

	// Видалення файлу зі списку
	private void DeleteButton_Click(object sender, RoutedEventArgs e)
	{
		var button = sender as Button;
		var filePath = button.Tag as string;
		var fileToRemove = VideoFiles.FirstOrDefault(f => f.FullPath == filePath);
		if (fileToRemove != null)
		{
			VideoFiles.Remove(fileToRemove);
		}
	}

	// Вибір теки для збереження
	private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
	{
		var folderPicker = new FolderPicker();
		InitializeWithWindow.Initialize(folderPicker, GetWindowHandle());
		folderPicker.FileTypeFilter.Add("*");

		var folder = await folderPicker.PickSingleFolderAsync();
		if (folder != null)
		{
			_outputFolder = folder.Path;
			OutputFolderTextBox.Text = folder.Path;
		}
	}

	// Початок конвертації
	private async void StartButton_Click(object sender, RoutedEventArgs e)
	{
		StartButton.IsEnabled = false;
		TotalProgressBar.Value = 0;

		for (int i = 0; i < VideoFiles.Count; i++)
		{
			var file = VideoFiles[i];
			file.IsConverting = true;
			file.Status = "In progress...";

			try
			{
				var originalSize = new FileInfo(file.FullPath).Length;

				string outputExtension = Path.GetExtension(file.FullPath);
				string outputFileName = $"{Path.GetFileNameWithoutExtension(file.FullPath)}_magnesium{outputExtension}";
				string outputDir = _outputFolder ?? Path.GetDirectoryName(file.FullPath);
				string outputPath = Path.Combine(outputDir, outputFileName);

				// Створюємо процесор аргументів
				var ffmpegArgumentProcessor = FFMpegArguments
					.FromFileInput(file.FullPath)
					.OutputToFile(outputPath, true, options =>
					{
						// Codec setup
						switch (CodecComboBox.SelectedIndex)
						{
							case 0: options.WithVideoCodec(VideoCodec.LibX264); break;
							case 1: options.WithVideoCodec(VideoCodec.LibX265); break;
							case 2: options.WithVideoCodec(VideoCodec.LibaomAv1); break;
							case 3: options.WithVideoCodec("hevc_nvenc"); break;
						}

						// Copy audio without changes
						options.WithAudioCodec(AudioCodec.Copy);

						// Set quality
						options.WithConstantRateFactor((int)QualitySlider.Value);

						// Set FPS if needed
						if (FpsNumberBox.Value > 0)
						{
							options.WithFramerate(FpsNumberBox.Value);
						}
					});

				// TODO: move to videofile
				var mediaInfo = await FFProbe.AnalyseAsync(file.FullPath);

				ffmpegArgumentProcessor.NotifyOnProgress(percent =>
				{
					DispatcherQueue.TryEnqueue(() =>
					{
						CurrentFileProgressBar.Value = percent;
					});
				}, mediaInfo.Duration);

				await ffmpegArgumentProcessor.ProcessAsynchronously();

				var newSize = new FileInfo(outputPath).Length;

				file.NewSize = FormatBytes((ulong)newSize);
				file.SavedPercentage = $"{100 - (100.0 * newSize / originalSize):0.##}%";
				file.Status = "Done";
			}
			catch (Exception ex)
			{
				file.Status = "Error";
				System.Diagnostics.Debug.WriteLine($"Conversion failed for {file.Name}: {ex.Message}");
			}
			finally
			{
				file.IsConverting = false;
				CurrentFileProgressBar.Value = 0;
				TotalProgressBar.Value = ((double)(i + 1) / VideoFiles.Count) * 100;
			}
		}

		StartButton.IsEnabled = true;
	}

	private nint GetWindowHandle()
	{
		return WindowNative.GetWindowHandle(this);
	}
}