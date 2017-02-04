using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
			watcher.ScanningMode = BluetoothLEScanningMode.Active;
			watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(20);
			watcher.Start();
		}

		Dictionary<string, BeaconInfo> Samples = new Dictionary<string, BeaconInfo>();
		private object _lockObject = new object();

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
					var id = guid.ToString() + minor;
					if (id.StartsWith("c"))
					{
						if(Samples.ContainsKey(id))
						{
							var beacon = Samples[id];
							if (beacon.Samples.Count < 10)
								beacon.Samples.Add(args.RawSignalStrengthInDBm);
							else
							{
								beacon.Samples.RemoveAt(0);
								beacon.Samples.Add(args.RawSignalStrengthInDBm);
								await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
								{
									var mean = beacon.Samples.Average(x => x);
									if(beacon.Minor == 101)
									{
										DistanceLabelA.Text = $"d({beacon.Minor}) = {ComputeDistance(mean, beacon.Power).ToString("0.00")}";
										ProximityA.Text = $"rssi({beacon.Minor}) = {mean.ToString("0.00")}";
									}
									else if (beacon.Minor == 102)
									{
										DistanceLabelB.Text = $"d({beacon.Minor}) = {ComputeDistance(mean, beacon.Power).ToString("0.00")}";
										ProximityB.Text = $"rssi({beacon.Minor}) = {mean.ToString("0.00")}";
									}
									else if (beacon.Minor == 103)
									{
										DistanceLabelC.Text = $"d({beacon.Minor}) = {ComputeDistance(mean, beacon.Power).ToString("0.00")}";
										ProximityC.Text = $"rssi({beacon.Minor}) = {mean.ToString("0.00")}";
									}
								});
							}
						}
						else
							lock(_lockObject)
								Samples.Add(id, new BeaconInfo { Guid = guid, Minor = minor, Power = power, Samples = new List<short> { args.RawSignalStrengthInDBm } } );
						
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
