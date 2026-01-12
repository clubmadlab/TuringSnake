using System;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;

namespace TuringMachine
{
	public static class DevicePort
	{
		private static SerialPort serialPort = null;

		public const int BAUD_RATE = 19200;

		static DevicePort()
		{
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern UInt32 QueryDosDevice(String lpDeviceName, String lpTargetPath, UInt32 ucchMax);

		public static bool Open()
		{
			if (serialPort != null && serialPort.IsOpen) return true;

			if (serialPort == null) serialPort = new SerialPort();

			// serialPort.BaudRate = BAUD_RATE;
			serialPort.Parity = Parity.None;
			serialPort.DataBits = 8;
			serialPort.StopBits = StopBits.One;
			serialPort.Handshake = Handshake.None;
			serialPort.ReadTimeout = 500;
			serialPort.WriteTimeout = 500;

			// find the virtual serial port
			String[] portNames;
			try {portNames = SerialPort.GetPortNames();} catch (Exception) {return false;}

			String lpTargetPath = new String(' ', 256);

			for (int i = 0; i < portNames.Length; i++)
			{
				if (QueryDosDevice(portNames[i], lpTargetPath, 255) > 0 && lpTargetPath.Contains("USBSER"))
				{
					serialPort.PortName = portNames[i];

					const int RETRY = 5;
					for (int n = 0; n < RETRY; n++)
					{
						try
						{
							serialPort.Open();

							serialPort.DiscardInBuffer();
							serialPort.DiscardOutBuffer();

							return true;
						}
						catch (Exception)
						{
							if (serialPort.IsOpen) try {serialPort.Close();} catch (Exception) {}
						}

						Thread.Sleep(100);
					}
				}
			}

			return false;
		}

		public static bool IsOpen()
		{
			if (serialPort == null) return false;
			else return serialPort.IsOpen;
		}

		public static void Flush()
		{
			if (serialPort == null || !serialPort.IsOpen) return;

			while (BytesToWrite > 0) Thread.Sleep(100);
		}

		public static void Close()
		{
			if (serialPort == null || !serialPort.IsOpen) return;

			try
			{
				serialPort.DiscardInBuffer();
				serialPort.DiscardOutBuffer();
				serialPort.Close();
				serialPort.Dispose();
			}
			catch (Exception) {}

			serialPort = null;
		}

		public static int BytesToRead
		{
			get
			{
				if (serialPort == null || !serialPort.IsOpen) return 0;
				else return serialPort.BytesToRead;
			}
		}

		public static int BytesToWrite
		{
			get
			{
				if (serialPort == null || !serialPort.IsOpen) return 0;
				else return serialPort.BytesToWrite;
			}
		}

		public static int Read(bool timeout)
		{
			if (serialPort == null || !serialPort.IsOpen) return -1;

			int c = -1;
			if (timeout || serialPort.BytesToRead > 0)
			{
				try {c = serialPort.ReadByte();} catch (Exception) {c = -1;}
			}

			return c;
		}

		public static int Read(byte[] buffer, int offset, int count, bool timeout)
		{
			if (serialPort == null || !serialPort.IsOpen) return -1;

			if (offset + count > buffer.Length) return -1;

			int i = 0;
			if (timeout || serialPort.BytesToRead >= count)
			{
				while (true)
				{
					int n;
					try {n = serialPort.Read(buffer, offset + i, count - i);} catch (Exception) {n = 0;}
					i += n; if (n == 0 || i == count) break;
				}
			}

			if (i < count) return -1;
			return 0;
		}

		public static int Read(byte[] buffer, int count, bool timeout)
		{
			return Read(buffer, 0, count, timeout);
		}

		public static int Write(byte b)
		{
			if (serialPort == null || !serialPort.IsOpen) return -1;

			byte[] buffer = {b};
			try {serialPort.Write(buffer, 0, 1);} catch (Exception) {return -1;}

			return 0;
		}

		public static int Write(byte[] buffer, int count)
		{
			if (serialPort == null || !serialPort.IsOpen) return -1;

			try
			{
				int offset = 0;
				while (count > 0)
				{
					int cnt = Math.Min(count, 255);
					serialPort.Write(buffer, offset, cnt);
					offset += cnt;
					count -= cnt;
				}
			}
			catch (Exception) {return -1;}

			return 0;
		}
	}
}
