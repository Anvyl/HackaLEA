using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace HackaLEA
{


    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
			this.Loaded += MainPage_Loaded;
        }

		private void MainPage_Loaded(object sender, RoutedEventArgs e)
		{
			BluetoothLEAdvertisementWatcher watcher = new BluetoothLEAdvertisementWatcher();
			watcher.Received += AdvertismentReceived;
			watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(0);
			watcher.ScanningMode = BluetoothLEScanningMode.Active;
			watcher.Start();
		}

		private async void AdvertismentReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
		{
			foreach (var adv in args.Advertisement.ManufacturerData)
			{
				var bytes = adv.Data.ToArray();
				if (bytes[0] == 0x02 && bytes[1] == 0x15 && bytes.Length == 23)
				{
					var guid = new Guid(
							BitConverter.ToInt32(bytes.Skip(2).Take(4).Reverse().ToArray(), 0),
							BitConverter.ToInt16(bytes.Skip(6).Take(2).Reverse().ToArray(), 0),
							BitConverter.ToInt16(bytes.Skip(8).Take(2).Reverse().ToArray(), 0),
							bytes.Skip(10).Take(8).ToArray());
					var major = BitConverter.ToUInt16(bytes.Skip(18).Take(2).Reverse().ToArray(), 0);
					var minor = BitConverter.ToUInt16(bytes.Skip(20).Take(2).Reverse().ToArray(), 0);
					var power = (short)(sbyte)bytes[22];
					if (guid.ToString().StartsWith("c336aa3"))
					{
						Debug.WriteLine(guid);
						Debug.WriteLine($"{ComputeDistance(args.RawSignalStrengthInDBm, power),2}");
						if(minor == 101)
						{
							await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
							{
								//DistanceLabelA.Text = $"A distance: {ComputeDistance(args.RawSignalStrengthInDBm, power),2}";
								ProximityA.Text = $"A proximity: {args.RawSignalStrengthInDBm}";
							});
						}
						else if (minor == 102)
						{
							await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
							{
								//DistanceLabelB.Text = $"B distance: {ComputeDistance(args.RawSignalStrengthInDBm, power),2}";
								ProximityB.Text = $"B proximity: {args.RawSignalStrengthInDBm}";
							});
						}
						else if (minor == 103)
						{
							await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
							{
								//DistanceLabelC.Text = $"C distance: {ComputeDistance(args.RawSignalStrengthInDBm, power),2}";
								ProximityC.Text = $"C proximity: {args.RawSignalStrengthInDBm}";
							});
						}
					}
				}
			}
		}

		private double ComputeDistance(double rssi, int txPower)
		{
			if (rssi == 0)
			{
				return -1.0; // if we cannot determine accuracy, return -1.
			}

			double ratio = rssi * 1.0 / txPower;
			if (ratio < 1.0)
			{
				return Math.Pow(ratio, 10);
			}
			else
			{
				double accuracy = (0.89976) * Math.Pow(ratio, 7.7095) + 0.111;
				return accuracy;
			}
		}
	}
}
