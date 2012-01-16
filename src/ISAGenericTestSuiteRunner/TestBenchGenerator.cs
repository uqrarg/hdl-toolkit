using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HDLToolkit;

namespace ISAGenericTestSuiteRunner
{
	public static class TestBenchGenerator
	{
		public static string GenerateTestBench(TestBench test, string workingDirectory, string templateFile)
		{
			string assemblyFile = PathHelper.Combine(workingDirectory, "test.s");
			Logger.Instance.WriteVerbose("Generating Assembly file");
			File.WriteAllText(assemblyFile, test.GenerateAssembly());

			MemoryStream code = GenerateMachineCode(workingDirectory, assemblyFile);

			return TestBenchTemplate(code, templateFile);
		}

		//FIXME: this is not specific to Test Bench Generation, move to somewhere more generic
		public static string PreProcessTestBench(string fileContents, string args, List<string> includeDirectories)
		{
			Logger.Instance.WriteVerbose("Preprocessing testbench with gcc");

			List<string> arguments = new List<string>();
			arguments.Add(" -E ");
			if (args != null)
				arguments.Add(args);
			// Include directories
			foreach (string include in includeDirectories)
			{
				arguments.Add("-I" + include);
			}

			// TODO: use standard in/out or pipes
			string workingDirectory = SystemHelper.GetTemporaryDirectory();
			string input = PathHelper.Combine(workingDirectory, "isag-tb-" + Guid.NewGuid().ToString() + ".S");
			string output = input + ".e";

			arguments.Add("-x assembler-with-cpp " + input); // file input flags
			arguments.Add("-o " + output);

			File.WriteAllText(input, fileContents);

			ProcessHelper.ProcessExecutionResult result = ProcessHelper.ExecuteProcess(
				Environment.CurrentDirectory, Environment.GetEnvironmentVariable("CROSS_COMPILE") + "gcc", arguments);

			File.Delete(input);

			if (!File.Exists(output))
			{
				Logger.Instance.WriteInfo(result.StandardError.Replace("\n", "\n\t"));
				throw new Exception("GCC was unable to pre-process the test bench.");
			}
			string preprocessedContents = File.ReadAllText(output);

			Directory.Delete(workingDirectory, true);

			return preprocessedContents;
		}

		public static MemoryStream GenerateMachineCode(string workingDirectory, string asmFile)
		{
			Logger.Instance.WriteVerbose("Generating Machine code from assembly file using gcc");
			ProcessHelper.ProcessExecutionResult result = ProcessHelper.ExecuteProcess(workingDirectory,
				Environment.GetEnvironmentVariable("CROSS_COMPILE") + "gcc",
				"-x assembler-with-cpp \"" + Path.GetFullPath(asmFile) + "\" -nostartfiles -nodefaultlibs");

			if (!File.Exists(PathHelper.Combine(workingDirectory, "a.out")))
			{
				Logger.Instance.WriteError(result.StandardError);
			}

			Logger.Instance.WriteVerbose("Generating binary output from elf");
			ProcessHelper.ExecuteProcess(workingDirectory,
				Environment.GetEnvironmentVariable("CROSS_COMPILE") + "objcopy",
				"-O binary a.out a.bin");

			Logger.Instance.WriteVerbose("Reading in binary machine code");
			MemoryStream stream = new MemoryStream();
			using (FileStream reader = new FileStream(PathHelper.Combine(workingDirectory, "a.bin"), FileMode.Open, FileAccess.Read))
			{
				int b = 0;
				while ((b = reader.ReadByte()) != -1)
				{
					stream.WriteByte((byte)b);
				}
			}

			return stream;
		}

		public static string TestBenchTemplate(MemoryStream code, string templateFile)
		{
			StringBuilder data = new StringBuilder();
			string template = File.ReadAllText(templateFile);
			int currentAddress = 0;

			Logger.Instance.WriteVerbose("Generating VHDL Testbench");

			//FIXME: these should be command line arguments and not environment variables

			code.Seek(0, SeekOrigin.Begin);

			int currentBlockIndex = 0;
			byte[] currentBlock = new byte[Processor.InstrSizeBytes];
			int currentData = 0;
			while ((currentData = code.ReadByte()) != -1)
			{
				// Fill the array from the right to the left (right side = LSB)
				currentBlock[currentBlockIndex++] = (byte)currentData;
				// Block is complete
				if (currentBlockIndex == Processor.InstrSizeBytes)
				{
					currentBlockIndex = 0;

					// Swap the order for little endian systems
					if (!Processor.BigEndian)
					{
						Array.Reverse(currentBlock);
					}
					data.AppendLine(string.Format("\t\t\tipif_addr_data_pair_format(x\"{0:X8}\", x\"{1}\"),",
						currentAddress, StringHelpers.BytesToString(currentBlock, 0, Processor.InstrSizeBytes)));
					currentAddress += Processor.InstrSizeBytes;
				}
			}

			// FIXME: this is a hack because of VHDLs inability to support single element arrays?
			data.AppendLine(string.Format("\t\t\tipif_addr_data_pair_format(x\"{0:X8}\", x\"{1}\")",
				-1, StringHelpers.BytesToString(new byte[Processor.InstrSizeBytes], 0, Processor.InstrSizeBytes)));

			return template.Replace("##DATAARRAY", data.ToString());
		}
	}
}
