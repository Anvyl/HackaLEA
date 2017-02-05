using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackaLEA
{
	public class Kalman
	{
		private double _r;
		private double _q;
		private double _a;
		private double _b;
		private double _c;

		public Kalman(double r=1, double q =1, double a =1, double b =0, double c =1)
		{
			_r = r;
			_q = q;
			_a = a;
			_b = b;
			_c = c;
		}

		public (double x, double cov) Filter(double x, double cov, double measure, double control = 0)
		{
			if (double.IsNaN(x))
			{
				x = (1 / _c) * measure;
				cov = (1 / _c) * _q * (1 / _c);
			}
			else
			{
				var predX = _a * x + _b * control;
				var predCov = _a * _a * cov + _r;

				var k = predCov * _c * (1 / ((_c * predCov * _c) + _q));

				x = predX + k * (measure - (_c * predX));
				cov = predCov - (k * _c * predCov);
			}
			return (x, cov);
		}

	
		
	}

}
