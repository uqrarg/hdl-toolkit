using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;
using HDLToolkit;

namespace ISAGenericTestSuiteRunner
{
	public class TestBench
	{
		private int Stride;

		private List<string> instructionsList;
		private int instructionsCount = -1;

		private List<TestCommand> commands;
		public AliasManager AliasManager { get; private set; }

		public int failedAssertions = 0;
		public int passedAssertions = 0;
		
		private class QueuedCommand
		{
			public TestCommand Command { get; set; }
			public int CycleToWait { get; set; }

			public QueuedCommand()
			{
			}
		}

		private List<QueuedCommand> queuedCommands = new List<QueuedCommand>();

		public TestBench()
		{
			Stride = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_STRIDE"));
			instructionsList = new List<string>();
			commands = new List<TestCommand>();
			AliasManager = new AliasManager();
		}

		public void Reset()
		{
			queuedCommands.Clear();

			failedAssertions = 0;
			passedAssertions = 0;
		}

		public void RunCommands(Processor proc)
		{
			ProcessorState state = proc.GetCurrentState();
			int nextPhysicalPC = state.PC / Stride;

			// find all commands to be made
			foreach (TestCommand c in commands)
			{
				if (c.Address == nextPhysicalPC)
				{
					// enqueuing the command
					Logger.Instance.WriteDebug("Enqueued command {0}::'{1}'", c.GetType().ToString(), c.Parameters);
					queuedCommands.Add(new QueuedCommand() { Command = c, CycleToWait = c.CyclesAfterEvent });
				}
			}

			// check all queued commands and execute any
			List<QueuedCommand> toRemove = new List<QueuedCommand>();
			foreach (QueuedCommand q in queuedCommands)
			{
				if (q.CycleToWait == 0)
				{
					Logger.Instance.WriteDebug("Executed command {0}::'{1}'", q.Command.GetType().ToString(), q.Command.Parameters);
					q.Command.Execute(proc);
					toRemove.Add(q);
				}
				else
				{
					q.CycleToWait--;
				}
			}
			foreach (QueuedCommand q in toRemove)
			{
				queuedCommands.Remove(q);
			}
		}

		public void IncrementAssertionResult(bool passed)
		{
			if (passed)
			{
				passedAssertions++;
			}
			else
			{
				failedAssertions++;
			}
		}

		public string GenerateAssembly()
		{
			StringBuilder builder = new StringBuilder();
			int instruction = 0;

			foreach (string i in instructionsList)
			{
				builder.AppendLine(i + "   // INSTRUCTION " + instruction++.ToString());
			}

			return builder.ToString();
		}

		#region Parsing
		private static Regex lineRegex = new Regex(@"(?<command>.*?)(\$|$)", RegexOptions.IgnoreCase);
		private static TestBench Load(string fileContents)
		{
			TestBench bench = new TestBench();

			using (StringReader reader = new StringReader(fileContents))
			{
				string line = null;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					for (Match m = lineRegex.Match(line); m.Success; m = m.NextMatch())
					{
						string command = m.Groups["command"].Value.Trim();
						if (!string.IsNullOrEmpty(command))
						{
							//print a warning for ##todo statements
							if (command.StartsWith("##todo", StringComparison.InvariantCultureIgnoreCase))
							{
								Logger.Instance.WriteWarning("TODO: {1}", command);
								continue;
							}
							//Ignore comments
							if (command.StartsWith("##"))
							{
								continue;
							}
							// Parse the command
							if (bench.ParseCommand(command))
							{
								continue;
							}
							bench.instructionsList.Add(command);
							bench.instructionsCount++;
						}
					}
				}
			}

			return bench;
		}

		public static string testBenchText;

		public static TestBench Load(string file, List<string> includeDirectories)
		{
			Logger.Instance.WriteDebug("Loading test bench...");

			string testBenchContents = File.ReadAllText(file);

			Logger.Instance.WriteDebug("Pre-processing test bench...");

			// Preprocessing
			testBenchContents = TestBenchGenerator.PreProcessTestBench(testBenchContents, "-P -CC -w", includeDirectories);
			testBenchText = testBenchContents;

			Logger.Instance.WriteDebug("Pre-processed test bench == ");
			Logger.Instance.WriteDebug(testBenchContents.Replace("\n", "\n\t"));

			// If valid, load the test bench
			if (!string.IsNullOrEmpty(testBenchContents))
			{
				return Load(testBenchContents);
			}
			return null;
		}

		private static Regex commandRegex = new Regex(@"#(?<command>.*?)(@(?<cycles>.*?))?\((?<content>.*?)\)", RegexOptions.IgnoreCase);
		private bool ParseCommand(string command)
		{
			Match m = commandRegex.Match(command);
			if (m.Success)
			{
				Logger.Instance.WriteDebug("Command:" + command);
				// Standard command
				// #type@cycleoffset(parameters)

				string type = m.Groups["command"].Value.ToLower();
				string cycles = m.Groups["cycles"].Value;
				string content = m.Groups["content"].Value;

				int cyclesOffset = 1;
				if (!string.IsNullOrEmpty(cycles) && !int.TryParse(cycles, out cyclesOffset))
				{
					throw new Exception(string.Format("Invalid integer '{0}' in command '{1}'", cycles, command));
				}

				// Determine actual command class
				TestCommand cmd = null;
				switch (type)
				{
					case "end":
						cmd = new EndTestCommand(this, instructionsCount, cyclesOffset, content);
						break;
					case "test":
						cmd = new AssertTestCommand(this, instructionsCount, cyclesOffset, content);
						break;
					case "skip":
						instructionsCount += cyclesOffset;
						break;
					case "alias":
						AliasManager.ParseAliasCommand(content);
						break;
					case "irq":
						cmd = new IrqTestCommand(this, instructionsCount, cyclesOffset, content);
					default:
						throw new Exception(string.Format("Invalid type '{0}' in command '{1}'", type, command));
				}
				
				if (cmd != null)
				{
					commands.Add(cmd);
				}
				return true;
			}
			else
			{
				return false;
			}
		}
		#endregion
	}
}
