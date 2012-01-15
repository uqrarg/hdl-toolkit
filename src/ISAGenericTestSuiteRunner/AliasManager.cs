using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HDLToolkit;

namespace ISAGenericTestSuiteRunner
{
	public class AliasManager
	{
		public class Alias
		{
			public string Name { get; set; }
			public string Expression { get; set; }

			public Dictionary<string, string> RangeAliases { get; private set; }

			public Alias()
			{
				RangeAliases = new Dictionary<string,string>();
			}

			public void AddRangeAlias(string alias, string expression)
			{
				RangeAliases[alias] = expression;
			}
		}

		public Dictionary<string, Alias> Aliases { get; private set; }

		private static Regex aliasExpression = new Regex(@"(?<alias>.*?)(\[(?<range>.*?)\])?=(?<expression>.*?)(\[(?<range>.*?)\])?$",
			RegexOptions.IgnoreCase | RegexOptions.Multiline);
		private static Regex aliasRange = new Regex("(?<name>.*?)=(?<value>.*?)(,|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

		public AliasManager()
		{
			Aliases = new Dictionary<string,Alias>();
		}

		public void ParseAliasCommand(string command)
		{
			if (!string.IsNullOrEmpty(command))
			{
				Match m = aliasExpression.Match(command);
				if (m.Success)
				{
					string alias = m.Groups["alias"].Value.Trim();
					string expression = m.Groups["expression"].Value.Trim();
					string range = (m.Groups["range"] != null) ? m.Groups["range"].Value : null;

					Alias create = new Alias();
					Aliases.Add(alias, create);
					create.Name = alias;
					create.Expression = expression;
					Logger.Instance.WriteDebug("Alias Command created alias '{0}' = '{1}'", alias, expression);

					if (!string.IsNullOrEmpty(range))
					{
						Match rangeMatch = aliasRange.Match(range);
						while (rangeMatch.Success)
						{
							string rangeAlias = rangeMatch.Groups["name"].Value.Trim();
							string rangeExpression = rangeMatch.Groups["value"].Value.Trim();

							create.AddRangeAlias(rangeAlias, rangeExpression);
							Logger.Instance.WriteDebug("\t\tRange Alias '{0}' = '{1}'", rangeAlias, rangeExpression);

							rangeMatch = rangeMatch.NextMatch();
						}
					}
				}
			}
		}

		private static Regex parseOperand = new Regex(@"(?<operand>.*?)(\[(?<range>.*?)(:(?<rangeend>.*?))?\])?$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

		public string AliasConvertOperand(string operand)
		{
			if (!string.IsNullOrEmpty(operand))
			{
				Match m = parseOperand.Match(operand);
				if (m.Success)
				{
					string name = m.Groups["operand"].Value.Trim();
					string range = (m.Groups["range"] != null) ? m.Groups["range"].Value.Trim() : null;
					string rangeEnd = (m.Groups["rangeend"] != null) ? m.Groups["rangeend"].Value.Trim() : null;

					// Check for any aliases with name
					if (Aliases.ContainsKey(name))
					{
						Alias alias = Aliases[name];
						name = alias.Expression;

						// Check for any range aliases with the expression
						if (!string.IsNullOrEmpty(range) && alias.RangeAliases.ContainsKey(range))
						{
							range = alias.RangeAliases[range];
						}

						if (!string.IsNullOrEmpty(rangeEnd) && alias.RangeAliases.ContainsKey(rangeEnd))
						{
							range = alias.RangeAliases[rangeEnd];
						}
					}
					
					if (!string.IsNullOrEmpty(range))
					{
						if (!string.IsNullOrEmpty(rangeEnd))
						{
							return string.Format("{0}[{1}:{2}]", name, range, rangeEnd);
						}
						return string.Format("{0}[{1}]", name, range);
					}
					return name;
				}
			}
			return operand;
		}
		
		public static AliasManager Instance = new AliasManager();
	}
}
