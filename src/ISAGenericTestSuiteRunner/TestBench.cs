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
		private int instructionsCount = 0;

		private List<TestCommand> commands;

		public int failedAssertions = 0;
		public int passedAssertions = 0;
		public bool endCalled = false;

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
		}

		public void Reset()
		{
			queuedCommands.Clear();

			failedAssertions = 0;
			passedAssertions = 0;
			endCalled = false;
		}

		public void RunAssertions(ProcessorState state)
		{
			int nextPhysicalPC = state.Pipeline[1].Value / Stride;

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
					q.Command.Execute(this, state);
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

		public void EndTest()
		{
			endCalled = true;
		}

		public bool IsTestComplete()
		{
			return endCalled;
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
		public static TestBench Load(string file)
		{
			TestBench bench = new TestBench();

			using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read))
			{
				using (StreamReader reader = new StreamReader(stream))
				{
					while (!reader.EndOfStream)
					{
						string line = reader.ReadLine().Trim();

						if (!string.IsNullOrEmpty(line))
						{
							if (ParseCommand(line, bench))
								continue;
							if (line.StartsWith("##todo", StringComparison.InvariantCultureIgnoreCase))
							{
								Logger.Instance.WriteWarning("{0}: {1}", Path.GetFileName(file), line);
								continue;
							}
							bench.instructionsList.Add(line);
							bench.instructionsCount++;
						}
					}
				}
			}

			return bench;
		}

		private static Regex commandRegex = new Regex(@"#(?<command>.*?)(@(?<cycles>.*?))?\((?<content>.*?)\)", RegexOptions.IgnoreCase);
		private static bool ParseCommand(string command, TestBench bench)
		{
			Match m = commandRegex.Match(command);
			if (m.Success)
			{
				Logger.Instance.WriteDebug("Command:" + command);
				// Standard command
				// #type@cycleoffset(parameters)

				string type = m.Groups["command"].Value.ToLower();
				string content = m.Groups["content"].Value;

				int cycles = 0;
				if (!string.IsNullOrEmpty(m.Groups["cycles"].Value))
				{
					if (!int.TryParse(m.Groups["cycles"].Value, out cycles))
					{
						//FIXME: print a warning or error message here
						return true;
					}
				}

				// Determine actual command class
				TestCommand cmd = null;
				switch (type)
				{
					case "end":
						cmd = new EndTestCommand(bench.instructionsCount, cycles, content); break;
					case "assert":
						cmd = new AssertTestCommand(bench.instructionsCount, cycles, content); break;
					case "skip":
						bench.instructionsCount += cycles; break;
					default:
						//FIXME: add warning or error message
						return true;
				}
				if (cmd != null)
					bench.commands.Add(cmd);
				return true;
			} else {
				return false;
			}
		}
		#endregion
	}
}
