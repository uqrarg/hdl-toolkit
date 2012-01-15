using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDLToolkit.Framework.Simulation;
using HDLToolkit.Xilinx.Simulation;

namespace ISAGenericTestSuiteRunner
{
	public class Processor
	{
		public ISimSimulator Simulator { get; set; }

		private int WordSize;
		private int NumGpRegisters;
		private int NumSpRegisters;
		private int NumIrqs;
		private ProcessorState state;
		private bool stateDirty = true;

		public Processor(ISimSimulator sim)
		{
			WordSize = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_WORD_SIZE"));
			NumGpRegisters = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_NUM_GPR"));
			NumSpRegisters = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_NUM_SPR"));
			NumIrqs = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_NUM_IRQS"));
			Simulator = sim;
		}
		
		public ProcessorState GetCurrentState()
		{
			if (stateDirty)
				RefreshCurrentState();
			return state;
		}
			
		private void RefreshCurrentState()
		{
			state.PC = (int)(Simulator.GetSignalState("UUT/pcs(1)").Flip().ToLong()); // current PC

			state.GpRegisters = new int[NumGpRegisters];
			state.SpRegisters = new int[NumSpRegisters];
			for (int i = 0; i < NumGpRegisters; i++)
				state.GpRegisters[i] = (int)(Simulator.GetSignalState(
					//FIXME: macroify or global variableify UUT somwehere
					"UUT/gprf/ISO_REG_FILE_INST/ram(" + i + ")(" + (WordSize - 1) + ":0)"
				).Flip().ToLong());
			for (int i = 0; i < NumSpRegisters; i++) {
				state.SpRegisters[i] = (int)(Simulator.GetSignalState(
					"UUT/state_1.rs(" + i + ")(" + (WordSize - 1) + ":0)"
				).Flip().ToLong());
			}
			stateDirty = false;
		}

		public void RunCycle()
		{
			stateDirty = true;
			Simulator.RunFor(10);
		}

		public void RunToNextValidInstruction()
		{
			while (true)
			{
				if (((Simulator.GetSignalState("UUT/instr_valid(1)").ToLong()) > 0))
					return;
				RunCycle();
			}
		}
		
		public void SetIrqs(int mask, int val)
		{
			string path = "irq";
			int x = (int)(Simulator.GetSignalState(path).Flip().ToLong());
			x &= ~mask;
			x |= mask & val;
			Simulator.SetSignalState(path, new StdLogicVector(NumIrqs, (uint)x));
		}
	}
}
