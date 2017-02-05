using System;
using System.Collections.Generic;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace HackaLEA
{
	public class BeaconInfo
	{
		public Guid Guid { get; set; }
		public string Id => Guid.ToString() + Minor;
		public ushort Minor { get; set; }
		public short Power { get; set; }
		public List<double> Signals { get; set; }
		public double SignalPower { get; set; } = 0;
		public (double x, double y) Position { get; set; }
		public double Cov { get; set; } = double.NaN;
		public short RawSignalPower { get; internal set; }
	}
}
