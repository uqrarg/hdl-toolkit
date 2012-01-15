using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDLToolkit.Xilinx.Simulation;

namespace ISAGenericTestSuiteRunner
{
	public class ProcessorState
	{
		public int PC { get; set; }

		public int[] GpRegisters { get; set; }
		public int[] SpRegisters { get; set; }

		public ProcessorState()
		{
		}
	}
}
