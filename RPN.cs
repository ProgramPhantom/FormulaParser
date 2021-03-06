using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

#nullable enable


namespace FormulaParser
{
	public class RPN
	{
		#region Fields
		private string _infixString;
		private List<Token> _RPNTokens;
		private List<Token> _infixTokens;
		#endregion

		#region Important Data
		public static Dictionary<string, Operator> Operators = new Dictionary<string, Operator>()
		{
			["+"] = new Operator("+", 1),
			["-"] = new Operator("-", 1),
			["*"] = new Operator("*", 2),
			["/"] = new Operator("/", 2),
			["^"] = new Operator("^", 3, true),
		};

		public static string[] Functions = new string[] { "sin", "cos", "tan", "sqrt", "ln", "log" };

		public static readonly Dictionary<string, double> Constants = new Dictionary<string, double>()
		{
			["pi"] = Math.PI,
			["e"] = Math.E,
		};

		public static readonly Dictionary<TokenType, Dictionary<TokenType, int>> OrderRules = new Dictionary<TokenType, Dictionary<TokenType, int>>()
		{
			[TokenType.CloseParenthesis] = new Dictionary<TokenType, int>
			{
				[TokenType.CloseParenthesis] = 0,
				[TokenType.Constant] = 1,
				[TokenType.Function] = 1,
				[TokenType.Number] = 1,
				[TokenType.OpenParenthesis] = 1,
				[TokenType.Operator] = 0,
				[TokenType.Variable] = 1,
			},
			[TokenType.Constant] = new Dictionary<TokenType, int>()
			{
				[TokenType.CloseParenthesis] = 0,
				[TokenType.Constant] = 2,
				[TokenType.Function] = 2,
				[TokenType.Number] = 1,
				[TokenType.OpenParenthesis] = 1,
				[TokenType.Operator] = 0,
				[TokenType.Variable] = 2
			},
			[TokenType.Function] = new Dictionary<TokenType, int>()
			{
				[TokenType.CloseParenthesis] = -1,
				[TokenType.Constant] = 2,
				[TokenType.Function] = 2,
				[TokenType.Number] = -1,
				[TokenType.OpenParenthesis] = 0,
				[TokenType.Operator] = -1,
				[TokenType.Variable] = 2
			},
			[TokenType.Number] = new Dictionary<TokenType, int>()
			{
				[TokenType.CloseParenthesis] = 0,
				[TokenType.Constant] = 1,
				[TokenType.Function] = 1,
				[TokenType.Number] = 2,
				[TokenType.OpenParenthesis] = 1,
				[TokenType.Operator] = 0,
				[TokenType.Variable] = 1
			},
			[TokenType.OpenParenthesis] = new Dictionary<TokenType, int>()
			{
				[TokenType.CloseParenthesis] = 0,
				[TokenType.Constant] = 0,
				[TokenType.Function] = 0,
				[TokenType.Number] = 0,
				[TokenType.OpenParenthesis] = 0,
				[TokenType.Operator] = -1,
				[TokenType.Variable] = 0
			},
			[TokenType.Operator] = new Dictionary<TokenType, int>()
			{
				[TokenType.CloseParenthesis] = -1,
				[TokenType.Constant] = 0,
				[TokenType.Function] = 0,
				[TokenType.Number] = 0,
				[TokenType.OpenParenthesis] = 0,
				[TokenType.Operator] = -1,
				[TokenType.Variable] = 0
			},
			[TokenType.Variable] = new Dictionary<TokenType, int>()
			{
				[TokenType.CloseParenthesis] = 0,
				[TokenType.Constant] = 2,
				[TokenType.Function] = 2,
				[TokenType.Number] = 1,
				[TokenType.OpenParenthesis] = 1,
				[TokenType.Operator] = 0,
				[TokenType.Variable] = 2
			},
		};
		#endregion

		#region Properties
		public List<Token> InfixTokens { get => _infixTokens; set => _infixTokens = value; }

		public List<Token> RPNTokens { get => _RPNTokens; set => _RPNTokens = value; }

		public string InfixString
		{
			get { return _infixString; }
			set { _infixString = value; }
		}
		#endregion

		// Constructor
		public RPN(string infix)
		{
			if (CheckBrackets(infix))
			{
				_infixString = infix;

			}
			else
			{

				throw new Exception("Mismatched brackets");
			}


			_infixTokens = Tokenize(infix);
			_RPNTokens = ShuntingYard();


		}

		private bool CheckBrackets(string formula)
		{
			int open = formula.Count(c => (c == '('));
			int close = formula.Count(c => (c == ')'));

			if (open == close)
			{
				return true;
			}
			else
			{
				return false;
			}

		}

		private bool CompareOperators(Operator op1, Operator op2) => op1.RightAssociative ? op1.Precedence < op2.Precedence : op1.Precedence <= op2.Precedence;

		#region Type Determiners
		public static TokenType DetermineType(string str)
		{
			if (Regex.IsMatch(str, @"^[a-zA-Z]+$")) // All letters?
				if (RPN.Functions.Contains(str))
				{
					return TokenType.Function;
				}
				else if (RPN.Constants.ContainsKey(str))
				{
					Console.WriteLine($"{str} is a constant!");
					return TokenType.Constant;
				}
				else
				{
					return TokenType.Variable;
				}

			if (double.TryParse(str, out _))  // All numbers?
				return TokenType.Number;

			if (string.IsNullOrWhiteSpace(str))
				return TokenType.WhiteSpace;

			if (str == "(")
				return TokenType.OpenParenthesis;

			if (str == ")")
				return TokenType.CloseParenthesis;

			if (Operators.ContainsKey(str))
				return TokenType.Operator;

			if (str == ".")
			{
				return TokenType.DecimalPoint;
			}


			throw new Exception($"Unknown character {str}");
		}

		public static TokenType DetermineCharType(char ch)
		{
			if (Regex.IsMatch(ch.ToString(), @"^[a-zA-Z]+$"))
			{
				return TokenType.Letter;
			}
			if (double.TryParse(ch.ToString(), out _))
			{
				return TokenType.Number;
			}
			if (string.IsNullOrWhiteSpace(ch.ToString()))
				return TokenType.WhiteSpace;

			if (ch == '(')
				return TokenType.OpenParenthesis;

			if (ch == ')')
				return TokenType.CloseParenthesis;

			if (Operators.ContainsKey(ch.ToString()))
				return TokenType.Operator;

			if (ch == '.')
			{
				return TokenType.DecimalPoint;
			}

			throw new Exception($"Unknown character {ch}");
		}
		#endregion

		public List<Token> Tokenize(string infix)
		{
			#region Variable Setup
			List<Token> tokens = new List<Token>();

			TextReader textReader = new StringReader(infix);

			TokenType nextType;
			int curr;
			char ch;
			int next;
			char nextCh;
			bool parenthesis;
			int operation;

			string readCharacters = "";
			#endregion


			#region FIRST PASS
			// FIRST ITERATION: split the infix expression crudely up into it's seconctions
			while ((curr = textReader.Read()) != -1)
			{
				ch = (char)curr;  // Current Character
				next = textReader.Peek();  // Integer holding character value for next character
				nextCh = (char)next;  // Next character


				TokenType currentType = DetermineCharType(ch);


				parenthesis = (currentType == TokenType.CloseParenthesis || currentType == TokenType.OpenParenthesis) ? true : false;  // Is it a parenthesis?

				nextType = next != -1 ? DetermineCharType(nextCh) : TokenType.WhiteSpace;

				if (currentType == TokenType.WhiteSpace)
				{
					continue; // Remove the whitespace! This will mean the "currentType != nextType" will always fire on the last letter, so there are no errors
				}
				else
				{
					readCharacters += ch;  //
				}

				// Do not worry about the type changing if it is going to form a decimal number!
				if (currentType == TokenType.Number && nextType == TokenType.DecimalPoint || currentType == TokenType.DecimalPoint && nextType == TokenType.Number)
				{
					continue;
				}


				// Multiple brackets are the only situation where you want to add similar types in a row as seperate tokens
				if (currentType != nextType || parenthesis || currentType == TokenType.Operator)  // Operators are also immediately added
				{
					TokenType cumulativeType = DetermineType(readCharacters);

					tokens.Add(new Token(cumulativeType, readCharacters));

					readCharacters = "";
				}

			}
			#endregion


			#region SECOND PASS
			// SECOND ITERATION: Combine and add in operators where needed
			for (int i = 0; i < tokens.Count() - 1; i++)
			{
				Token currentToken = tokens[i];
				Token nextToken = tokens[i + 1];

				// DECIMAL POINT WAS NOT IN DICTIONARY ERROR
				operation = RPN.OrderRules[currentToken.Type][nextToken.Type];

				Console.WriteLine($"{currentToken.Value} to {nextToken.Value}: {operation}");


				// Get subtract's special stuff out the way
				if (currentToken.Value == "-")
				{
					// Check next value first for simplicity
					if (nextToken.Type == TokenType.Constant || nextToken.Type == TokenType.Number || nextToken.Type == TokenType.Variable)
					{

						// First value is Open Parenthesis, Operator, or it is the first value

						if (i == 0)
						{
							// ConSUME the next token, and add a "-"
							tokens.RemoveRange(i, 1);  // Remove the -
							tokens[i].AddNeg(); // Not i + 1 because we just removed it on the line above
						}
						else
						{  // Only try to do this if we defo know we are not on the first token!
							if (tokens[i - 1].Type == TokenType.OpenParenthesis || tokens[i - 1].Type == TokenType.Operator)
							{

								// ConSUME the next token, and add a "-"
								tokens.RemoveRange(i, 1);  // Remove the -
								tokens[i].AddNeg();  // Not i + 1 because we just removed it on the line above
							}

						}

					}
				}

				if ((currentToken.Type == TokenType.OpenParenthesis || currentToken.Type == TokenType.Operator) && nextToken.Value == "-") { continue; }  // To fix the "Syntax error unless minus"


				switch (operation)
				{
					case -1:
						{
							throw new Exception($"Illegal Syntax {currentToken.Type}:{nextToken.Type}");
						}
					case 0:
						break;
					case 1:
						{
							tokens.Insert(i + 1, new Token(TokenType.Operator, "*"));
							i += 2;  // Might as well skip over the * too
							break;
						}
					case 2:
						Console.WriteLine("Should not be possible");
						break;

						// NEED TO FIX: when making a variable: cannot use the phrases that referr to the constants, including the letter "e" ):
				}

				Console.WriteLine($"{currentToken.Value} to {nextToken.Value}: {operation}");
			}
			#endregion

			return tokens;
		}

		public List<Token> ShuntingYard()
		{
			List<Token> outputList = new List<Token>();
			Stack<Token> operatorStack = new Stack<Token>();
			foreach (var tok in InfixTokens)  // Iterate throught the tokens
			{
				switch (tok.Type)
				{
					case TokenType.Constant:
					case TokenType.Variable: // If number or varible put straight into the output
					case TokenType.Number:
						outputList.Add(tok);
						break;
					case TokenType.Function:  // If function, put on the op stack
						operatorStack.Push(tok);
						break;
					case TokenType.Operator:
						//   Any elements in opstack?    Top token is an operator?             Is the current token's precedeance less than the token on top?
						while (operatorStack.Any() && operatorStack.Peek().Type == TokenType.Operator && CompareOperators(Operators[tok.Value], Operators[operatorStack.Peek().Value]))
						{
							_RPNTokens.Add(operatorStack.Pop()); // Then put that on the output!
						}
						operatorStack.Push(tok);  // Then put the operator on top
						break;
					case TokenType.OpenParenthesis:
						operatorStack.Push(tok);
						break;
					case TokenType.CloseParenthesis:
						while (operatorStack.Peek().Type != TokenType.OpenParenthesis) { outputList.Add(operatorStack.Pop()); }   // Pop off the opstack until the open bracket is found

						operatorStack.Pop();  // Then pop off the open bracket

						if (operatorStack.Count() != 0) // If not empty
						{
							if (operatorStack.Peek().Type == TokenType.Function)
								outputList.Add(operatorStack.Pop());  // If it was a function before that, add it to the output
						}


						break;
					default:
						throw new Exception("Wrong token");
				}
			}
			while (operatorStack.Any()) // If any operators left on the opstack
			{
				var tok = operatorStack.Pop();
				if (tok.Type == TokenType.OpenParenthesis || tok.Type == TokenType.CloseParenthesis)
					throw new Exception("Mismatched parentheses");  // Invalid infix! ):
				outputList.Add(tok);
			}

			return outputList;
		}

		public double Compute(Dictionary<string, double> variableContext)
		{


			double tempNumber;  // Needed.
			Stack<double> valueStack = new Stack<double>();

			foreach (Token tok in RPNTokens)
			{

				if (tok.Type == TokenType.Number)
				{
					valueStack.Push(double.Parse(tok.Value));
				}
				else if (tok.Type == TokenType.Constant)
				{
					try
					{
						valueStack.Push((double)RPN.Constants[tok.Value]);
					}
					catch (KeyNotFoundException)
					{
						Console.WriteLine($"Uknown variable: {tok.Value}");
						break;
					}


				}
				else if (tok.Type == TokenType.Variable)
				{
					try
					{
						valueStack.Push((double)variableContext[tok.Value]);
					}
					catch (KeyNotFoundException)
					{
						Console.WriteLine($"Uknown variable: {tok.Value}");
						break;
					}
				}
				else if (tok.Type == TokenType.Function)
				{
					switch (tok.Value)
					{
						case "sin":
							{
								valueStack.Push((double)Math.Sin(valueStack.Pop()));
								break;
							}
						case "cos":
							{
								valueStack.Push((double)Math.Cos(valueStack.Pop()));
								break;
							}
						case "tan":
							{
								valueStack.Push((double)Math.Tan((double)valueStack.Pop()));
								break;
							}
						case "sqrt":
							{
								valueStack.Push((double)Math.Sqrt(valueStack.Pop()));
								break;
							}
						case "ln":
							{
								valueStack.Push((double)Math.Log(valueStack.Pop(), Math.E));
								break;
							}
						case "log":
							{
								valueStack.Push((double)Math.Log10(valueStack.Pop()));
								break;
							}

					}
				}
				else if (tok.Type == TokenType.Operator)
				{
					switch (tok.Value)
					{
						case "^":
							{
								tempNumber = valueStack.Pop();
								valueStack.Push(Math.Pow(valueStack.Pop(), tempNumber));
								break;
							}
						case "*":
							{
								valueStack.Push(valueStack.Pop() * valueStack.Pop());
								break;
							}
						case "/":
							{
								tempNumber = valueStack.Pop();
								valueStack.Push(valueStack.Pop() / tempNumber);
								break;
							}
						case "+":
							{
								valueStack.Push(valueStack.Pop() + valueStack.Pop());
								break;
							}
						case "-":
							{
								tempNumber = valueStack.Pop();
								valueStack.Push(valueStack.Pop() - tempNumber);
								break;
							}
					}
				}
			}

			try
			{
				return valueStack.Pop();

			}
			catch (InvalidOperationException)
			{
				Console.WriteLine("Problem parsing");
				return -1;
			}
		}

	}
}