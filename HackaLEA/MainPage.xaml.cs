using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace HackaLEA
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		private Kalman _kalman = new Kalman(q: 3, r: 0.01);
		private Guid _sessionGuid = Guid.NewGuid();
		private MessageWebSocket _socket;
		private DataWriter _messageWriter;
		private string _userName;
		private List<BeaconInfo> _beacons = new List<BeaconInfo>();
		BluetoothLEAdvertisementWatcher watcher = new BluetoothLEAdvertisementWatcher();
		private object _lockObject = new object();

		public MainPage()
		{
			this.InitializeComponent();
			this.Loaded += MainPage_Loaded;
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			_userName = e.Parameter as string;
		}

		private async void MainPage_Loaded(object sender, RoutedEventArgs e)
		{
			watcher.Received += AdvertismentReceivedAsync;
			watcher.Stopped += WatchStops;
			watcher.ScanningMode = BluetoothLEScanningMode.Active;
			watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(5);
			watcher.Start();

			_socket = new MessageWebSocket();
			_messageWriter = new DataWriter(_socket.OutputStream);
			await _socket.ConnectAsync(new Uri("ws://10.244.52.254:8080"));
		}

		private void WatchStops(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
		{
			throw new NotImplementedException();
		}

		public Dictionary<int, double> SignalCoefficients = new Dictionary<int, double>
		{
			[101] = 1,
			[102] = 1.08,
			[103] = 1,
			[104] = 1,
			[105] = 0.96,
			[106] = 1.08
		};

		private async void AdvertismentReceivedAsync(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
		{
			if(sender.Status != BluetoothLEAdvertisementWatcherStatus.Started)
			{
				Debug.WriteLine("kakova hera");
			}
			try
			{

				foreach (var adv in args.Advertisement.ManufacturerData)
				{
					var bytes = adv.Data.ToArray();

					//accept only ibeacons
					if (bytes[0] != 0x02 || bytes[1] != 0x15 || bytes.Length != 23)
						return;

					//get data from bytes
					var guid = new Guid(
							BitConverter.ToInt32(bytes.Skip(2).Take(4).Reverse().ToArray(), 0),
							BitConverter.ToInt16(bytes.Skip(6).Take(2).Reverse().ToArray(), 0),
							BitConverter.ToInt16(bytes.Skip(8).Take(2).Reverse().ToArray(), 0),
							bytes.Skip(10).Take(8).ToArray());
					var major = BitConverter.ToUInt16(bytes.Skip(18).Take(2).Reverse().ToArray(), 0);
					var minor = BitConverter.ToUInt16(bytes.Skip(20).Take(2).Reverse().ToArray(), 0);
					var power = (short)(sbyte)bytes[22];

					var id = guid.ToString() + minor;

					//target phones start with c
					if (!id.StartsWith("c"))
						return;
					BeaconInfo beacon;
					if ((beacon = _beacons.SingleOrDefault(x => x.Id == id)) != null)
					{
						var pair = _kalman.Filter(beacon.SignalPower, beacon.Cov, args.RawSignalStrengthInDBm * SignalCoefficients[minor]);
						var mean = beacon.SignalPower = pair.x;
						beacon.Cov = pair.cov;
						beacon.RawSignalPower = args.RawSignalStrengthInDBm;


						var closestBeacon = _beacons.Aggregate((x, y) => x.SignalPower > y.SignalPower ? x : y);

						var data = new
						{
							user = _userName,
							userID = _sessionGuid,
							room = closestBeacon.SignalPower > -60 ? closestBeacon.Minor : 0,
							beacons = _beacons
									.OrderByDescending(x => x.SignalPower).Take(3)
									.Select(x => new { x.Minor, x.SignalPower, x.RawSignalPower })
									.ToList()
						};

						await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							RoomName.Text = data.room is 0 ? "You are outside" : $"You are in room {data.room}";
							GridContainer.Children.OfType<Border>().ToList().ForEach(x =>
								{
									if (x.Name == $"Room{data.room}")
										x.Background = new SolidColorBrush(Colors.Green);
									else
										x.Background = new SolidColorBrush(Colors.White);
								});
						});

						var json = JsonConvert.SerializeObject(data);
						await SendMessage(json);
					}
					else
						lock (_lockObject)
						{
							_beacons.Add(new BeaconInfo
							{
								Guid = guid,
								Minor = minor,
								TxPower = power,
								//Position = Positions[minor],
								Signals = new List<double>
								{
									args.RawSignalStrengthInDBm * SignalCoefficients[minor]
								}
							});
							_beacons = _beacons.OrderBy(x => x.Minor).ToList();
						}
				}
			}
			catch
			{

			}
		}

		//Send a message to the server.
		private async Task SendMessage(string message)
		{
			try
			{
				_messageWriter.WriteString(message);
				await _messageWriter.StoreAsync();
			}
			catch
			{

			}
		}

		private (double, double) Triangulate(BeaconInfo beacon1, BeaconInfo beacon2, BeaconInfo beacon3)
		{
			var r1 = ComputeDistance(beacon1.SignalPower, beacon1.TxPower);
			var r2 = ComputeDistance(beacon2.SignalPower, beacon2.TxPower);
			var r3 = ComputeDistance(beacon3.SignalPower, beacon3.TxPower);

			double x1 = beacon1.Position.x;
			double y1 = beacon1.Position.y;

			double x2 = beacon2.Position.x;
			double y2 = beacon2.Position.y;

			double x3 = beacon3.Position.x;
			double y3 = beacon3.Position.y;

			double x = Math.Abs(((y2 - y3) * ((Math.Pow(y2, 2) - Math.Pow(y1, 2)) + (Math.Pow(x2, 2) - Math.Pow(x1, 2)) + (Math.Pow(r1, 2) - Math.Pow(r2, 2)))
				- (y1 - y2) * ((Math.Pow(y3, 2) - Math.Pow(y2, 2)) + (Math.Pow(x3, 2) - Math.Pow(x2, 2)) + (Math.Pow(r2, 2) - Math.Pow(r3, 2))))
				 / (2 * ((x1 - x2) * (y2 - y3) - (x2 - x3) * (y1 - y2))));
			double y = Math.Abs(((x2 - x3) * ((Math.Pow(x2, 2) - Math.Pow(x1, 2)) + (Math.Pow(y2, 2) - Math.Pow(y1, 2)) + (Math.Pow(r1, 2) - Math.Pow(r2, 2)))
				- (x1 - x2) * ((Math.Pow(x3, 2) - Math.Pow(x2, 2)) + (Math.Pow(y3, 2) - Math.Pow(y2, 2)) + (Math.Pow(r2, 2) - Math.Pow(r3, 2))))
				/ (2 * ((y1 - y2) * (x2 - x3) - (y2 - y3) * (x1 - x2))));

			return (x, y);
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
