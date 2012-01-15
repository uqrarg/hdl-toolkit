﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDLToolkit;
using HDLToolkit.Framework.Simulation;
using HDLToolkit.Xilinx.Simulation;
using System.Text.RegularExpressions;

namespace ISAGenericTestSuiteRunner
{
	public class Processor
	{
		public ISimSimulator Simulator { get; set; }
		
		//FIXME: localise this somewhow (C# syntax is teh suk with no non-static inner classes)
		public static int WordSize { get; set; }
		public static int NumGpRegisters { get; set; }
		public static int NumSpRegisters { get; set; }
		public static int NumIrqs { get; set; }
		private ProcessorState state = new ProcessorState();
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
		
		public abstract class Property
		{	
			public abstract int Evaluate(Processor proc);
			
			private static Regex regRegex = new Regex(@"^(?<type>.)(?<index>\d+)(\[(?<start>\d{1,2})(:(?<end>\d{1,2}))?\])?", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			public static bool TryParse(string operand, out Property ret)
			{
				ret = null;
				operand = AliasManager.Instance.AliasConvertOperand(operand);
				Logger.Instance.WriteDebug("\t\tOperand aliased to '{0}'", operand);
				Match m = regRegex.Match(operand);
			
				int index;
				if (m.Success && int.TryParse(m.Groups["index"].Value, out index))
				{
					string registerType = m.Groups["type"].Value;
	
					// Parse the start index
					int rangeStart = Processor.WordSize-1;
					int rangeEnd = 0;
					if (m.Groups["start"] != null && !string.IsNullOrEmpty(m.Groups["start"].Value)) {
						if (!int.TryParse(m.Groups["start"].Value, out rangeStart)) {
							//TODO: sensible error message
							return false;
						} else {
							rangeEnd = rangeStart;
						}
					}
					// Parse the end index
					if (m.Groups["end"] != null && !string.IsNullOrEmpty(m.Groups["end"].Value)) {
						if (!int.TryParse(m.Groups["end"].Value, out rangeEnd)) {
							//TODO: sensible error message
							return false;
						}
					}
					
					switch (registerType)
					{
						case "r":
							if (index < Processor.NumGpRegisters)
							{
								ret = new GPRegProperty(index, rangeStart, rangeEnd);
							} //TODO: else some error message
							break;
						case "s":
							if (index < Processor.NumGpRegisters)
							{
								ret = new SPRegProperty(index, rangeStart, rangeEnd);
							} //TODO: else some error message
							break;
						default :
							return false;
					}
					return true;
				}
				
				int cval;
				if (int.TryParse(operand, out cval)) {
					ret = new ConstantProperty(cval); return true;
				}
			
				if (operand.StartsWith("pc", StringComparison.InvariantCultureIgnoreCase)) {
					ret = PC; return true;
				}
				
				return false;
			}
		}
		
		private class ConstantProperty : Property
		{
			private int val;
			
			public ConstantProperty(int v) {
				 val = v;
			}
			
			public override int Evaluate(Processor proc) {
				return val;
			}
		}
		
		private abstract class RegProperty : Property
		{
			protected int index;
			protected int start;
			protected int end;
			
			public RegProperty(int index, int start, int end) {
				this.index = index;
				this.start = start;
				this.end = end;
			}
			
			protected int Evaluate(int regval) {
				return (regval >> end) & ((1 << (start - end + 1)) - 1);
			}
		}
		
		private class SPRegProperty : RegProperty
		{
			public SPRegProperty(int index, int start, int end) :
				base(index, start, end) {}
			
			public override int Evaluate(Processor proc) {
				return Evaluate(proc.GetCurrentState().SpRegisters[index]);
			}
		}
		
		private class GPRegProperty : RegProperty
		{
			public GPRegProperty(int index, int start, int end) :
				base(index, start, end) {}
			
			public override int Evaluate(Processor proc) {
				return Evaluate(proc.GetCurrentState().GpRegisters[index]);
			}
		}
		
		private class PCProperty : Property
		{
			public override int Evaluate(Processor proc) {
				return proc.GetCurrentState().PC;
			}
		}
		
		public static Property PC = new PCProperty();

	}
}
