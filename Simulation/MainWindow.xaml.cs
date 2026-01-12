using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Windows.Threading;

// complete instructions.md

namespace TuringMachine
{
	// square symbols or states
	public enum Symbol {RED, GREEN, BLUE, CYAN, MAGENTA, YELLOW, WHITE, BLACK};

	// commands
	public enum Command {RESET = 1, LOAD, RUN, STEP, SET_SPEED, SET_HIGHLIGHT, STORE};

	public static class Extensions
	{
		public static Symbol ToSymbol(this char c)
		{
			return c switch
			{
				'R' => Symbol.RED,
				'G' => Symbol.GREEN,
				'B' => Symbol.BLUE,
				'C' => Symbol.CYAN,
				'M' => Symbol.MAGENTA,
				'Y' => Symbol.YELLOW,
				'W' => Symbol.WHITE,
				'K' => Symbol.BLACK,
				 _  => Symbol.BLACK,
			};
		}
	}

	public partial class MainWindow : Window
	{
		private const string DEFAULT_FILENAME = "temp";
		private const string DEFAULT_EXTENSION = ".txt";

		private readonly string[] SymbolNames = Enum.GetNames(typeof(Symbol));

		private readonly Color[] SymbolColours = new Color[] {Colors.Red, Colors.Green, Colors.Blue, Colors.Cyan, Colors.Magenta, Colors.Yellow, Colors.White, Colors.Black};

		// number of squares on the tape
		private const int NUM_SQUARES = 27;

		// maximum number of variables
		private const int MAX_VARIABLES = 10;

		// maximum number of significant characters in label and variable names
		private const int NAME_LEN = 10;

		// maximum program length
		private const int MAX_PROGRAM = 256;

		private const string WindowTitle = "Turing Snake Turing Machine";
		private string ProgramFilename = "";
		private string ProgramDirectory = Properties.Settings.Default.ProgramDirectory;

		// tape head posiiton
		private int HeadPosition = 0;

		// current program
		private string Program = "";

		// program position
		private int ProgramPosition = 0;

		// command buffer
		private static byte[] CommandBuffer = new byte[64];

		// current character in program
		private char current => ProgramPosition >= Program.Length ? '\0' : Program[ProgramPosition];

		// steps over current character in program
		private void step() {if (ProgramPosition < Program.Length) ProgramPosition++;}

		// next character in program
		private char next => ProgramPosition >= Program.Length ? '\0' : (++ProgramPosition >= Program.Length ? '\0' : Program[ProgramPosition]);

		// true if end of program
		private bool end_of_program => ProgramPosition >= Program.Length;

		// true if valid character in variable or label name
		private bool is_name(char c) => char.IsLower(c) || char.IsDigit(c) || c == '_';

		// true if symbol
		private bool is_symbol(char c) => c == 'R' || c == 'G' || c == 'B' || c == 'C' || c == 'M' || c == 'Y' || c == 'W' || c == 'K';

		// true if arithmetic or bitwise operator
		private bool is_operator(char c) => c == '+' || c == '-' || c == '*' || c == '/' || c == '&' || c == '|';

		// true if start of expression
		private bool is_expression(char c) => char.IsDigit(c) || c == '-' || c == '$' || c == '(';

		// wait periods
		private int WaitPeriods = 0;

		private List<Symbol> Symbols = new();
		private List<Ellipse> LEDs = new();
		private Dictionary<string, int> Variables = new();

		public static RoutedCommand NewCommand = new();
		public static RoutedCommand LoadCommand = new();
		public static RoutedCommand SaveCommand = new();
		public static RoutedCommand ReloadCommand = new();
		public static RoutedCommand EscCommand = new();
		public static RoutedCommand F1Command = new();
		public static RoutedCommand F5Command = new();
		public static RoutedCommand F6Command = new();
		public static RoutedCommand F7Command = new();

		private const int MAX_HISTORY = 6;
		private List<string> History = new();

		private static DispatcherTimer StepTimer;

		public MainWindow()
		{
			InitializeComponent();

			CommandBindings.Add(new CommandBinding(NewCommand, NewCommandExecute, NewCommandCanExecute));
			CommandBindings.Add(new CommandBinding(LoadCommand, LoadCommandExecute, LoadCommandCanExecute));
			CommandBindings.Add(new CommandBinding(SaveCommand, SaveCommandExecute, SaveCommandCanExecute));
			CommandBindings.Add(new CommandBinding(ReloadCommand, ReloadCommandExecute, ReloadCommandCanExecute));
			CommandBindings.Add(new CommandBinding(EscCommand, EscCommandExecute, EscCommandCanExecute));
			CommandBindings.Add(new CommandBinding(F1Command, F1CommandExecute, F1CommandCanExecute));
			CommandBindings.Add(new CommandBinding(F5Command, F5CommandExecute, F5CommandCanExecute));
			CommandBindings.Add(new CommandBinding(F6Command, F6CommandExecute, F6CommandCanExecute));
			CommandBindings.Add(new CommandBinding(F7Command, F7CommandExecute, F7CommandCanExecute));

			for (int i = 0; i < NUM_SQUARES; i++) Symbols.Add(Symbol.BLACK);

			background.Background = Properties.Settings.Default.DarkMode ? Brushes.Black : Brushes.White;
			outline.BorderBrush = Properties.Settings.Default.DarkMode ? Brushes.White : Brushes.Black;
			editor.Background = Properties.Settings.Default.DarkMode ? Brushes.Black : Brushes.White;

			foreach (MenuItem menuItem in ClockSpeedMenuItem.Items)
			{
				menuItem.IsChecked = Convert.ToInt32(menuItem.Tag) == Properties.Settings.Default.ClockSpeed;
			}

			if (Properties.Settings.Default.FileHistory1 != "") History.Add(Properties.Settings.Default.FileHistory1);
			if (Properties.Settings.Default.FileHistory2 != "") History.Add(Properties.Settings.Default.FileHistory2);
			if (Properties.Settings.Default.FileHistory3 != "") History.Add(Properties.Settings.Default.FileHistory3);
			if (Properties.Settings.Default.FileHistory4 != "") History.Add(Properties.Settings.Default.FileHistory4);
			if (Properties.Settings.Default.FileHistory5 != "") History.Add(Properties.Settings.Default.FileHistory5);
			if (Properties.Settings.Default.FileHistory6 != "") History.Add(Properties.Settings.Default.FileHistory6);

			StepTimer = new DispatcherTimer(DispatcherPriority.Normal);
			StepTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / Properties.Settings.Default.ClockSpeed);
			StepTimer.Tick += new EventHandler(StepTimer_Tick);
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			ShowHistory();

			DarkModeMenuItem.IsChecked = Properties.Settings.Default.DarkMode;
			SyntaxHighlightingMenuItem.IsChecked = Properties.Settings.Default.SyntaxHighlighting;
			RepeatStepMenuItem.IsChecked = Properties.Settings.Default.RepeatStep;
			TapeheadHighlightingMenuItem.IsChecked = Properties.Settings.Default.TapeheadHighlighting;

			DrawAll();

			Reset();

			while (true)
			{
				if (DevicePort.Open()) break;

				MessageBoxResult button = MessageBox.Show("Unable to open port - retry ?", "Virtual serial port",
				  MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);
				if (button != MessageBoxResult.OK) break;
			}

			if (DevicePort.IsOpen()) StatusMessage.Text = "Virtual serial port open";

			editor.Focus();

			editor.Selection.Select(editor.Document.ContentStart, editor.Document.ContentStart);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			DevicePort.Flush();
			DevicePort.Close();
		}

		private void AddHistory(string filename)
		{
			if (History.Contains(filename)) History.Remove(filename);
			if (History.Count >= MAX_HISTORY) History.RemoveAt(History.Count-1);
			History.Insert(0, filename);

			Properties.Settings.Default.FileHistory1 = History.Count >= 1 ? History[0] : "";
			Properties.Settings.Default.FileHistory2 = History.Count >= 2 ? History[1] : "";
			Properties.Settings.Default.FileHistory3 = History.Count >= 3 ? History[2] : "";
			Properties.Settings.Default.FileHistory4 = History.Count >= 4 ? History[3] : "";
			Properties.Settings.Default.FileHistory5 = History.Count >= 5 ? History[4] : "";
			Properties.Settings.Default.FileHistory6 = History.Count >= 6 ? History[5] : "";
			Properties.Settings.Default.Save();

			ShowHistory();
		}

		private void ShowHistory()
		{
			int i = FileMenu.Items.IndexOf(FileHistory);

			Separator separator = FileMenu.Items[i] as Separator;
			separator.Visibility = History.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

			for (int n = 0; n < MAX_HISTORY; n++)
			{
				MenuItem menuItem = FileMenu.Items[++i] as MenuItem;
				menuItem.Visibility = Visibility.Collapsed;
				if (n >= History.Count) continue;
				menuItem.Visibility = Visibility.Visible;
				menuItem.Header = new TextBlock() {Text = System.IO.Path.GetFileName(History[n])};
				menuItem.ToolTip = History[n];
			}
		}

		private void History_Click(object sender, RoutedEventArgs e)
		{
			int i = Convert.ToInt32((e.Source as MenuItem).Tag);
			if (i > History.Count) return;
			string filename = History[i - 1];

			AddHistory(filename);

			LoadProgram(filename);
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			DrawAll();
		}

		private void Options_SubmenuOpened(object sender, RoutedEventArgs e)
		{
			foreach (MenuItem menuItem in ClockSpeedMenuItem.Items)
			{
				menuItem.IsChecked = Convert.ToInt32(menuItem.Tag) == Properties.Settings.Default.ClockSpeed;
			}
		}

		private void StepTimer_Tick(object sender, EventArgs e)
		{
			if (Properties.Settings.Default.RepeatStep)
			{
				Dispatcher.InvokeAsync(StepRepeat, DispatcherPriority.Background);
			}
			else
			{
				Step();

				UpdateTape();
			}
		}

		private void DrawAll()
		{
			tape.Width = ActualWidth - 6;
			tape.Height = (ActualHeight - 370) * 1.00;

			outline.Width = ActualWidth * 0.90;
			outline.Margin = new Thickness((ActualWidth - outline.Width - 6) / 2, 0, 0, 0);

			transport.Margin =  new Thickness((ActualWidth - transport.ActualWidth - 6) / 2, 25, 0, 0);

			DrawTape();

			SyntaxHighlight();
		}

		// draws all the squares of the tape
		private void DrawTape()
		{
			System.Drawing.Color outlineColour = Properties.Settings.Default.OutlineColour;
			Brush borderBrush = new SolidColorBrush(Color.FromRgb(outlineColour.R, outlineColour.G, outlineColour.B));
			System.Drawing.Color tapeColour = Properties.Settings.Default.TapeColour;
			Brush tapeBrush = new SolidColorBrush(Color.FromRgb(tapeColour.R, tapeColour.G, tapeColour.B));
			Brush backgroundBrush = Properties.Settings.Default.DarkMode ? Brushes.Black : Brushes.White;

			double width = tape.ActualWidth;
			double height = tape.ActualHeight;
			Point centre = new(width/2, height/2);

			const double BORDER = 2;
			double square = Math.Min(width/12, height/8);
			double spacing = square * 2.0;
			double led = square/2.0;

			LEDs.Clear();
			tape.Children.Clear();

			void add_rectangle(double x, double y)
			{
				Rectangle rectangle = new() {StrokeThickness = BORDER, Stroke = borderBrush, Fill = tapeBrush, Width = square, Height = square};
				Canvas.SetLeft(rectangle, x - square/2 - BORDER);
				Canvas.SetTop(rectangle, y - square/2 - BORDER);
				Canvas.SetZIndex(rectangle, +1);
				tape.Children.Add(rectangle);
			}

			void add_semicircle(double x, double y, double radius, Brush fill)
			{
				PathFigure pathFigure = new() {StartPoint = new(x + radius - BORDER, y)};
				ArcSegment arcSegment = new() {Point = new(x - radius - BORDER, y), Size = new(Math.Abs(radius), Math.Abs(radius)),
				  IsLargeArc = false, SweepDirection = SweepDirection.Counterclockwise, RotationAngle = 180};
				PathSegmentCollection pathSegmentCollection = new() {arcSegment};
				pathFigure.Segments = pathSegmentCollection;
				PathFigureCollection pathFigureCollection = new() {pathFigure};
				PathGeometry pathGeometry = new() {Figures = pathFigureCollection};
				Path path = new() {StrokeThickness = BORDER, Stroke = borderBrush, Fill = fill, Data = pathGeometry};
				tape.Children.Add(path);
			}

			void add_line(double x1, double y1, double x2, double y2)
			{
				Line line = new() {StrokeThickness = BORDER, Stroke = borderBrush, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2};
				tape.Children.Add(line);
			}

			void add_led(double x, double y)
			{
				Ellipse ellipse = new() {StrokeThickness = 0, Stroke = Brushes.Transparent, Fill = Brushes.Black, Width = led, Height = led};
				Canvas.SetLeft(ellipse, x - led/2 - BORDER);
				Canvas.SetTop(ellipse, y - led/2 - BORDER);
				Canvas.SetZIndex(ellipse, +2);
				tape.Children.Add(ellipse);

				LEDs.Add(ellipse);
			}

			double X1 = centre.X - 2.0 * spacing;
			double X2 = centre.X - 1.5 * spacing;
			double X3 = centre.X - 1.0 * spacing;
			double X4 = centre.X - 0.5 * spacing;
			double X5 = centre.X;
			double X6 = centre.X + 0.5 * spacing;
			double X7 = centre.X + 1.0 * spacing;
			double X8 = centre.X + 1.5 * spacing;
			double X9 = centre.X + 2.0 * spacing;

			double Y1 = centre.Y - 1.5 * square + BORDER * 2;
			double Y2 = centre.Y - 1.0 * square + BORDER;
			double Y3 = centre.Y;
			double Y4 = centre.Y + 1.0 * square - BORDER;
			double Y5 = centre.Y + 1.5 * square - BORDER * 2;

			double radius1 = (spacing + square)/2 - BORDER/2;
			double radius2 = spacing/2 - BORDER/2;
			double radius3 = spacing/2 + BORDER/2;
			double radius4 = (spacing - square)/2 + BORDER/2;

			double sin1 = Math.Sin(Math.PI/3);
			double cos1 = Math.Cos(Math.PI/3);
			double sin2 = Math.Sin(Math.PI/6);
			double cos2 = Math.Cos(Math.PI/6);

			add_rectangle(X1, Y2);
			add_rectangle(X1, Y3);
			add_rectangle(X1, Y4);

			add_semicircle(X2, Y1, +radius1, tapeBrush);
			add_semicircle(X2, Y1, +radius4, backgroundBrush);

			add_line(X2 - radius4 * cos1, Y1 - radius4 * sin1, X2 - radius1 * cos1, Y1 - radius1 * sin1);
			add_line(X2 - radius4 * cos1, Y1 - radius4 * sin1, X2 - radius1 * cos1, Y1 - radius1 * sin1);
			add_line(X2 + radius4 * cos1, Y1 - radius4 * sin1, X2 + radius1 * cos1, Y1 - radius1 * sin1);
			add_line(X2 + radius4 * cos1, Y1 - radius4 * sin1, X2 + radius1 * cos1, Y1 - radius1 * sin1);

			add_rectangle(X3, Y2);
			add_rectangle(X3, Y3);
			add_rectangle(X3, Y4);

			add_semicircle(X4, Y5, -radius1, tapeBrush);
			add_semicircle(X4, Y5, -radius4, backgroundBrush);

			add_line(X4 - radius4 * cos1, Y5 + radius4 * sin1, X4 - radius1 * cos1, Y5 + radius1 * sin1);
			add_line(X4 - radius4 * cos1, Y5 + radius4 * sin1, X4 - radius1 * cos1, Y5 + radius1 * sin1);
			add_line(X4 + radius4 * cos1, Y5 + radius4 * sin1, X4 + radius1 * cos1, Y5 + radius1 * sin1);
			add_line(X4 + radius4 * cos1, Y5 + radius4 * sin1, X4 + radius1 * cos1, Y5 + radius1 * sin1);

			add_rectangle(X5, Y2);
			add_rectangle(X5, Y3);
			add_rectangle(X5, Y4);

			add_semicircle(X6, Y1, +radius1, tapeBrush);
			add_semicircle(X6, Y1, +radius4, backgroundBrush);

			add_line(X6 - radius4 * cos1, Y1 - radius4 * sin1, X6 - radius1 * cos1, Y1 - radius1 * sin1);
			add_line(X6 - radius4 * cos1, Y1 - radius4 * sin1, X6 - radius1 * cos1, Y1 - radius1 * sin1);
			add_line(X6 + radius4 * cos1, Y1 - radius4 * sin1, X6 + radius1 * cos1, Y1 - radius1 * sin1);
			add_line(X6 + radius4 * cos1, Y1 - radius4 * sin1, X6 + radius1 * cos1, Y1 - radius1 * sin1);

			add_rectangle(X7, Y2);
			add_rectangle(X7, Y3);
			add_rectangle(X7, Y4);

			add_semicircle(X8, Y5, -radius1, tapeBrush);
			add_semicircle(X8, Y5, -radius4, backgroundBrush);

			add_line(X8 - radius4 * cos1, Y5 + radius4 * sin1, X8 - radius1 * cos1, Y5 + radius1 * sin1);
			add_line(X8 - radius4 * cos1, Y5 + radius4 * sin1, X8 - radius1 * cos1, Y5 + radius1 * sin1);
			add_line(X8 + radius4 * cos1, Y5 + radius4 * sin1, X8 + radius1 * cos1, Y5 + radius1 * sin1);
			add_line(X8 + radius4 * cos1, Y5 + radius4 * sin1, X8 + radius1 * cos1, Y5 + radius1 * sin1);

			add_rectangle(X9, Y2);
			add_rectangle(X9, Y3);
			add_rectangle(X9, Y4);

			add_led(X1, Y4);
			add_led(X1, Y3);
			add_led(X1, Y2);

			add_led(X2 - radius2 * cos2, Y1 - radius2 * sin2);
			add_led(X2 + BORDER, Y1 - radius2 + BORDER/2);
			add_led(X2 + radius2 * cos2 + BORDER/2, Y1 - radius2 * sin2);

			add_led(X3, Y2);
			add_led(X3, Y3);
			add_led(X3, Y4);

			add_led(X4 - radius3 * cos2, Y5 + radius3 * sin2);
			add_led(X4 + BORDER, Y5 + radius3 + BORDER/2);
			add_led(X4 + radius3 * cos2, Y5 + radius3 * sin2);

			add_led(X5, Y4);
			add_led(X5, Y3);
			add_led(X5, Y2);

			add_led(X6 - radius2 * cos2, Y1 - radius2 * sin2);
			add_led(X6 + BORDER, Y1 - radius2 + BORDER/2);
			add_led(X6 + radius2 * cos2 + BORDER/2, Y1 - radius2 * sin2);

			add_led(X7, Y2);
			add_led(X7, Y3);
			add_led(X7, Y4);

			add_led(X8 - radius3 * cos2, Y5 + radius3 * sin2);
			add_led(X8 + BORDER, Y5 + radius3 + BORDER/2);
			add_led(X8 + radius3 * cos2, Y5 + radius3 * sin2);

			add_led(X9, Y4);
			add_led(X9, Y3);
			add_led(X9, Y2);
		}

		private void F1CommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void F1CommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			Instructions_Click(this, null);
		}

		private void EscCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void EscCommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			Reset();
		}

		private void F5CommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void F5CommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			Reset();
		}
		private void Reset_Click(object sender, RoutedEventArgs e)
		{
			Reset();
		}

		private void F6CommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void F6CommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			Run();
		}
		private void Run_Click(object sender, RoutedEventArgs e)
		{
			Run();
		}

		private void F7CommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void F7CommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			Stop();

			Step();

			UpdateTape();
		}
		private void Step_Click(object sender, RoutedEventArgs e)
		{
			Stop();

			Step();

			UpdateTape();
		}

		// update all LEDs
		private void UpdateTape()
		{
			for (int i = 0; i < NUM_SQUARES; i++)
			{
				Color colour = SymbolColours[(int) Symbols[i]];
				LEDs[i].Fill = new SolidColorBrush(colour);

				// indicate tape head position with outline
				bool highlight = Properties.Settings.Default.TapeheadHighlighting && i == HeadPosition;
				LEDs[i].StrokeThickness = highlight ? 2 : 0;
				LEDs[i].Stroke = highlight ? Brushes.LightGray : Brushes.Transparent;
			}
		}

		// returns rich text as simple string
		private string GetProgram()
		{
			return (new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text).Trim();

			#if false

			// flattens rich text into string
			string flatten(Inline inline)
			{
				if (inline is Run)
				{
					return (inline as Run).Text;
				}
				else if (inline is Span)
				{
					string s = "";
					foreach (Inline child in (inline as Span).Inlines)
					{
						if (s != "") s += "\n";
						s += flatten(child);
					}
					return s;
				}
				return "";
			}

			string text = "";
			foreach (Block block in editor.Document.Blocks)
			{
				if (!(block is Paragraph)) continue;
				foreach (Inline inline in (block as Paragraph).Inlines) text += flatten(inline);
				if (text != "") text += "\n";
			}

			return text.Trim();

			#endif
		}

		// removes comments and whitespace
		private string RemoveComments(string s)
		{
			string d = "";
			bool comment = false;

			foreach (char c in s)
			{
				if (c == ';') comment = true;
				if (!comment && !(c == ' ' || c == '\t' || c == '\r' || c == '\n')) d += c;
				if (c == '\r' || c == '\n') comment = false;
			}

			return d;
		}

		private bool HighlightUpdate = true;

		private void editor_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (HighlightUpdate) SyntaxHighlight();
		}

		private enum FSM {DEFAULT, COMMENT, INSTRUCTION, CONDITIONAL, BRANCH, CONSTANT, VARIABLE, LABEL, SYMBOL};

		// colour code program
		void SyntaxHighlight()
		{
			HighlightUpdate = false;

			Color DEFAULT_COLOUR = Properties.Settings.Default.DarkMode ? Colors.White : Colors.Black;
			Color COMMENT_COLOUR = Colors.Green;
			Color INSTRUCTION_COLOUR = Colors.Blue;
			Color CONDITIONAL_COLOUR = INSTRUCTION_COLOUR;
			Color BRANCH_COLOUR = INSTRUCTION_COLOUR;
			Color CONSTANT_COLOUR = Colors.Red;
			Color VARIABLE_COLOUR = Colors.Magenta;
			Color LABEL_COLOUR = Colors.Cyan;
			Color SYMBOL_COLOUR = DEFAULT_COLOUR;

			FSM fsm = FSM.DEFAULT;

			SolidColorBrush[] brushes = new SolidColorBrush[] {new(DEFAULT_COLOUR), new(COMMENT_COLOUR), new(INSTRUCTION_COLOUR),
			  new(CONDITIONAL_COLOUR), new(BRANCH_COLOUR), new(CONSTANT_COLOUR), new(VARIABLE_COLOUR), new(LABEL_COLOUR), new(SYMBOL_COLOUR)};
			foreach (Brush brush in brushes) brush.Freeze();

			void do_fsm(char c)
			{
				switch (fsm)
				{
				case FSM.DEFAULT:
					break;
				case FSM.COMMENT:
					if (c != '\n') return;
					break;
				case FSM.INSTRUCTION:
					break;
				case FSM.CONDITIONAL:
					if (c == '!' || c == '<' || c == '>' || c == '=') return;
					break;
				case FSM.BRANCH:
					fsm = FSM.LABEL;
					if (is_name(c)) return;
					break;
				case FSM.CONSTANT:
					if (char.IsDigit(c)) return;
					break;
				case FSM.VARIABLE:
					if (is_name(c)) return;
					break;
				case FSM.LABEL:
					if (is_name(c)) return;
					break;
				case FSM.SYMBOL:
					break;
				}

				fsm = FSM.DEFAULT;

				if (c == ' ')
				{
					return;
				}
				else if (c == ';')
				{
					fsm = FSM.COMMENT;
				}
				else if (c == '<' || c == '>' || c == '%')
				{
					fsm = FSM.INSTRUCTION;
				}
				else if (c == '?')
				{
					fsm = FSM.CONDITIONAL;
				}
				else if (c == '^')
				{
					fsm = FSM.BRANCH;
				}
				else if (char.IsDigit(c))
				{
					if (fsm != FSM.VARIABLE && fsm != FSM.LABEL) fsm = FSM.CONSTANT;
				}
				else if (c == '$')
				{
					fsm = FSM.VARIABLE;
				}
				else if (c == '#')
				{
					fsm = FSM.LABEL;
				}
				else if (is_symbol(c))
				{
					fsm = FSM.SYMBOL;
				}
			}

			const double FONT_SIZE = 20.0;
			const string FONT_FAMILY = "Segoe UI";

			TextPointer p = editor.Document.ContentStart;
			while (p != null)
			{
				TextPointerContext context = p.GetPointerContext(LogicalDirection.Forward);
				if (context == TextPointerContext.Text)
				{
					char[] buffer = new char[1];
					while (p.GetTextInRun(LogicalDirection.Forward, buffer, 0, buffer.Length) > 0)
					{
						do_fsm(buffer[0]);

						TextRange textRange = new(p, p.GetNextInsertionPosition(LogicalDirection.Forward));
						SolidColorBrush brush = brushes[(int) fsm];
						if (!Properties.Settings.Default.SyntaxHighlighting) brush = brushes[0];
						else if (fsm == FSM.SYMBOL)
						{
							Symbol symbol = buffer[0].ToSymbol();
							brush = new SolidColorBrush(SymbolColours[(int) symbol]);
							if (symbol == Symbol.BLACK && Properties.Settings.Default.DarkMode) brush = Brushes.Gray;
							else if (symbol == Symbol.WHITE && !Properties.Settings.Default.DarkMode) brush = Brushes.Gray;
						}
						textRange.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
						textRange.ApplyPropertyValue(TextElement.FontSizeProperty, FONT_SIZE);
						textRange.ApplyPropertyValue(TextElement.FontFamilyProperty, FONT_FAMILY);
						textRange.ApplyPropertyValue(TextElement.FontWeightProperty, fsm == FSM.SYMBOL ? FontWeights.Bold : FontWeights.Normal);

						p = p.GetNextInsertionPosition(LogicalDirection.Forward);
					}
				}
				else if (context == TextPointerContext.ElementEnd)
				{
					do_fsm('\n');
				}
				p = p.GetNextContextPosition(LogicalDirection.Forward);
			}

			HighlightUpdate = true;
		}

		// resets the Turing Machine
		private void Reset()
		{
			Stop();

			// leftmost square
			HeadPosition = 0;

			// start of program
			ProgramPosition = 0;

			// no wait
			WaitPeriods = 0;

			// clear symbols
			for (int i = 0; i < NUM_SQUARES; i++) Symbols[i] = Symbol.BLACK;

			Variables.Clear();

			StartButtonIcon.Visibility = StartMenuIcon.Visibility = Visibility.Visible;
			PauseButtonIcon.Visibility = PauseMenuIcon.Visibility = Visibility.Hidden;

			UpdateTape();

			SyntaxHighlight();

			StatusMessage.Text = "";
			InstrMessage.Text = "";

			CommandBuffer[0] = (byte) Command.RESET;
			DevicePort.Write(CommandBuffer, 1);
		}

		private void Run()
		{
			if (StepTimer.IsEnabled) Stop(); else Start();

			CommandBuffer[0] = (byte) Command.SET_SPEED;
			CommandBuffer[1] = (byte) Properties.Settings.Default.ClockSpeed;
			DevicePort.Write(CommandBuffer, 2);

			CommandBuffer[0] = (byte) Command.SET_HIGHLIGHT;
			CommandBuffer[1] = Properties.Settings.Default.TapeheadHighlighting ? (byte) 1 : (byte) 0;
			DevicePort.Write(CommandBuffer, 2);

			CommandBuffer[0] = (byte) Command.RUN;
			DevicePort.Write(CommandBuffer, 1);
		}

		private void Start()
		{
			StepTimer.Start();

			StartButtonIcon.Visibility = StartMenuIcon.Visibility = Visibility.Hidden;
			PauseButtonIcon.Visibility = PauseMenuIcon.Visibility = Visibility.Visible;
		}

		private void Stop()
		{
			StepTimer.Stop();

			StartButtonIcon.Visibility = StartMenuIcon.Visibility = Visibility.Visible;
			PauseButtonIcon.Visibility = PauseMenuIcon.Visibility = Visibility.Hidden;
		}

		private void StepRepeat()
		{
			if (!StepTimer.IsEnabled || !Step())
			{
				UpdateTape();
			}
			else
			{
				Dispatcher.InvokeAsync(StepRepeat, DispatcherPriority.Background);
			}
		}

		// steps the Turing Machine, returns false if end of program or wait
		private bool Step()
		{
			// if halted
			if (WaitPeriods < 0) return false;

			if (WaitPeriods > 0)
			{
				WaitPeriods--;
				return false;
			}

			Program = GetProgram();
			if (string.IsNullOrEmpty(Program))
			{
				Stop();
				return false;
			}

			// skips over whitespace and comments, returns true if end of program
			bool skip_space()
			{
				while (true)
				{
					if (current == ';')
					{
						// comment - skip to end of line
						while (next != '\0' && current != '\n') ;
					}
					else if (current == ' ' || current == '\t' || current == '\r' || current == '\n')
					{
						step();
					}
					else
					{
						break;
					}
				}

				return end_of_program;
			}

			void error(string err)
			{
				Stop();
				StatusMessage.Text = err + " at position ~" + ProgramPosition;
				ProgramPosition = Program.Length;
			}

			// parses a decimal number
			int get_number()
			{
				bool negate = current == '-';
				if (negate) step();

				int n = 0;
				while (current != '\0' && char.IsDigit(current))
				{
					n = n * 10 + (current - '0');
					step();
				}
				return negate ? -n : n;
			}

			// parses a label or variable name
			string get_name()
			{
				skip_space();
				string name = "";
				while (is_name(current))
				{
					if (name.Length < NAME_LEN) name += current;
					step();
				}
				return name;
			}

			// parses an operand (variable or decimal number), returns null if error
			int? get_operand()
			{
				skip_space();

				if (is_symbol(current))
				{
					Symbol symbol = current.ToSymbol();
					step();
					// off-tape squares read as black
					if (HeadPosition < 0 || HeadPosition >= NUM_SQUARES) return symbol == Symbol.BLACK ? 1 : 0;
					return Symbols[HeadPosition] == symbol ? 1 : 0;
				}

				else if (current == '$')
				{
					step();
					string variable = get_name();
					if (Variables.ContainsKey(variable)) return Variables[variable];
					error("Variable $" + variable + " not found");
					return null;
				}

				else if (current == '-' || char.IsDigit(current))
				{
					return get_number();
				}

				error("Operand error");
				return null;
			}

			// parses an expression, returns null if error
			int? get_expression()
			{
				skip_space();

				int? result;
				if (current == '(')
				{
					step();
					result = get_expression();
					if (result == null) return null;
					if (current != ')') return null;
					step();
				}
				else
				{
					result = get_operand();
				}
				if (result == null) return null;

				while (true)
				{
					skip_space();

					if (current == ')') break;

					char op = current;
					if (!is_operator(op)) break;

					step();

					int? operand = get_operand();
					if (operand == null) return null;

					switch (op)
					{
					case '+':
						result += operand;
						break;
					case '-':
						result -= operand;
						break;
					case '*':
						result *= operand;
						break;
					case '/':
						result /= operand;
						break;
					case '&':
						result &= operand;
						break;
					case '|':
						result |= operand;
						break;
					}
				}

				// 8-bit
				result = (int) (sbyte) result;

				return result;
			}

			// returns the current instruction
			string current_instr()
			{
				int saved = ProgramPosition;

				skip_space();
				skip_instruction();

				int len = ProgramPosition - saved;
				ProgramPosition = saved;

				if (len <= 0) return "";

				return Program.Substring(ProgramPosition, len).Trim();
			}

			// handles <, <n, <<, >, >n, >>
			void do_movement()
			{
				if (current == '<')
				{
					if (next == '<')
					{
						step();
						// first square
						HeadPosition = 0;
						StatusMessage.Text = "Move to the first square";
					}
					else if (is_expression(current))
					{
						int? n = get_expression();
						if (n == null) return;
						HeadPosition -= (int) n;
						StatusMessage.Text = "Move " + n + " squares to the left";
					}
					else
					{
						HeadPosition--;
						StatusMessage.Text = "Move one square to the left";
					}
				}

				else if (current == '>')
				{
					if (next == '>')
					{
						step();
						// last square
						HeadPosition = NUM_SQUARES-1;
						StatusMessage.Text = "Move to the last square";
					}
					else if (is_expression(current))
					{
						int? n = get_expression();
						if (n == null) return;
						HeadPosition += (int) n;
						StatusMessage.Text = "Move " + n + " squares to the right";
					}
					else
					{
						HeadPosition++;
						StatusMessage.Text = "Move one square to the right";
					}
				}

				// allow one square off tape
				if (HeadPosition < 0) HeadPosition = -1;
				else if (HeadPosition >= NUM_SQUARES) HeadPosition = NUM_SQUARES;
			}

			void skip_movement()
			{
				if (current == '<')
				{
					if (next == '<') step();
					else if (is_expression(current)) get_expression();
				}

				else if (current == '>')
				{
					if (next == '>') step();
					else if (is_expression(current)) get_expression();
				}
			}

			// handles assignments
			void do_assignment()
			{
				step();
				string variable = get_name();
				if (!Variables.ContainsKey(variable))
				{
					if (Variables.Count >= MAX_VARIABLES) error("Too many variables");
					else Variables.Add(variable, 0);
				}

				skip_space();

				if (current == '+')
				{
					if (next == '+')
					{
						step();
						Variables[variable]++;
						if (Variables[variable] > 127) Variables[variable] = 127;
						StatusMessage.Text = "Increment variable $" + variable + " = " + Variables[variable];
						return;
					}
				}

				else if (current == '-')
				{
					if (next == '-')
					{
						step();
						Variables[variable]--;
						if (Variables[variable] < -128) Variables[variable] = -128;
						StatusMessage.Text = "Decrement variable $" + variable + " = " + Variables[variable];
						return;
					}
				}

				else if (current == '=')
				{
					step();
					int? x = get_expression();
					if (x == null) return;
					Variables[variable] = (int) x;
					StatusMessage.Text = "Set variable $" + variable + " = " + Variables[variable];
					return;
				}

				error("Syntax error");
			}

			void skip_assignment()
			{
				step();
				get_name();

				skip_space();

				if (current == '+')
				{
					if (next == '+') step();
				}

				else if (current == '-')
				{
					if (next == '-') step();
				}

				else if (current == '=')
				{
					step();
					get_expression();
				}
			}

			// handles conditionals
			void do_conditional()
			{
				bool test;
				if (next == '!')
				{
					step();
					int? x = get_expression();
					if (x == null) return;
					test = x == 0;
				}
				else if (current == '>')
				{
					if (next == '=')
					{
						step();
						int? x = get_expression();
						if (x == null) return;
						test = x >= 0;
					}
					else
					{
						int? x = get_expression();
						if (x == null) return;
						test = x > 0;
					}
				}
				else if (current == '<')
				{
					if (next == '=')
					{
						step();
						int? x = get_expression();
						if (x == null) return;
						test = x <= 0;
					}
					else
					{
						int? x = get_expression();
						if (x == null) return;
						test = x < 0;
					}
				}
				else
				{
					int? x = get_expression();
					if (x == null) return;
					test = x != 0;
				}
				if (!test)
				{
					skip_space();
					skip_instruction();
				}
				StatusMessage.Text = "Conditional = " + (test ? "true" : "false");
			}

			void skip_conditional()
			{
				if (next == '!')
				{
					step();
				}
				else if (current == '>')
				{
					if (next == '=') step();
				}
				else if (current == '<')
				{
					if (next == '=') step();
				}
				get_expression();
				skip_space();
				skip_instruction();
			}

			// handles branches
			void do_branch()
			{
				step();
				string label = get_name();
				StatusMessage.Text = "Branch to #" + label;

				ProgramPosition = 0;
				while (true)
				{
					if (current == '\0')
					{
						error("Label not found");
						Stop();
						return;
					}

					skip_space();

					if (current != '#')
					{
						step();
						continue;
					}
					step();
					if (get_name() == label) return;
				}
			}

			void skip_branch()
			{
				step();
				get_name();
			}

			// handles waits
			void do_wait()
			{
				WaitPeriods = 1;
				if (is_expression(next))
				{
					int? n = get_expression();
					if (n == null) return;
					WaitPeriods = (int) n;
					if (WaitPeriods == 0) WaitPeriods = -1;
				}
				if (WaitPeriods < 0) StatusMessage.Text = "Halt";
				else StatusMessage.Text = "Wait " + WaitPeriods + " period(s)";
			}

			void skip_wait()
			{
				if (is_expression(next)) get_expression();
			}

			// handles R, G, B, C, M, Y, W, K
			void do_set()
			{
				Symbol symbol = current.ToSymbol();
				step();
				// off-tape squares can't be set
				if (HeadPosition < 0 || HeadPosition >= NUM_SQUARES) return;
				Symbols[HeadPosition] = symbol;
				StatusMessage.Text = "Set current square to " + SymbolNames[(int) symbol];
			}

			void skip_set()
			{
				step();
			}

			// handles the next instruction
			void do_instruction()
			{
				if (current == '<' || current == '>')
				{
					do_movement();
				}

				else if (current == '$')
				{
					do_assignment();
				}

				else if (current == '?')
				{
					do_conditional();
				}

				else if (current == '^')
				{
					do_branch();
				}

				else if (current == '%')
				{
					do_wait();
				}

				else if (is_symbol(current))
				{
					do_set();
				}
				else
				{
					error("Instruction error");
				}
			}

			// skips over the next instruction
			void skip_instruction()
			{
				if (current == '<' || current == '>')
				{
					skip_movement();
				}

				else if (current == '$')
				{
					skip_assignment();
				}

				else if (current == '?')
				{
					skip_conditional();
				}

				else if (current == '^')
				{
					skip_branch();
				}

				else if (current == '%')
				{
					skip_wait();
				}

				else if (is_symbol(current))
				{
					skip_set();
				}
			}

			if (ProgramPosition >= Program.Length)
			{
				Stop();
				return false;
			}

			// step over whitespace and labels
			while (true)
			{
				skip_space();
				if (current != '#') break;
				step();
				while (is_name(current)) step();
			}

			if (current == '\0')
			{
				Stop();
				return false;
			}

			string instr = current_instr();
			if (instr != "") InstrMessage.Text = "Executed instruction: " + instr;

			do_instruction();

			CommandBuffer[0] = (byte) Command.STEP;
			DevicePort.Write(CommandBuffer, 1);

			if (WaitPeriods > 0)
			{
				WaitPeriods--;
				return false;
			}

			return true;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			// insert instruction into program
			bool end = editor.CaretPosition.GetNextInsertionPosition(LogicalDirection.Forward) == null;
			string text = (sender as Button).Tag as string;
			new TextRange(editor.CaretPosition, editor.CaretPosition) {Text = text};
			if (end) editor.CaretPosition = editor.Document.ContentEnd;
			SyntaxHighlight();
			editor.Focus();
		}

		private int GetCaretOffset()
		{
			int offset = 0;

			TextPointer p = editor.Document.ContentStart;
			while (p != null && p.CompareTo(editor.CaretPosition) < 0)
			{
				TextPointerContext context = p.GetPointerContext(LogicalDirection.Forward);
				if (context == TextPointerContext.Text)
				{
					char[] buffer = new char[1];
					while (p.GetTextInRun(LogicalDirection.Forward, buffer, 0, buffer.Length) > 0)
					{
						offset++;
						p = p.GetNextInsertionPosition(LogicalDirection.Forward);
						if (p.CompareTo(editor.CaretPosition) >= 0) break;
					}
				}
				else if (context == TextPointerContext.ElementEnd)
				{
					offset++;
				}
				p = p.GetNextContextPosition(LogicalDirection.Forward);
			}

			return offset;
		}

		private void editor_SelectionChanged(object sender, RoutedEventArgs e)
		{
			PositionMessage.Text = "Editor position: " + GetCaretOffset();
		}

		private void NewCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void NewCommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			NewProgram();
		}

		private void NewProgram()
		{
			Stop();

			Title = WindowTitle;
			ProgramFilename = DEFAULT_FILENAME + DEFAULT_EXTENSION;

			editor.Document.Blocks.Clear();
			editor.Selection.Select(editor.Document.ContentStart, editor.Document.ContentStart);

			Reset();
		}

		private void LoadCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void LoadCommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			LoadProgram();
		}

		private void LoadProgram()
		{
			string filename = ProgramFilename;
			if (filename == "") filename = DEFAULT_FILENAME + DEFAULT_EXTENSION;

			Microsoft.Win32.OpenFileDialog dialog = new();
			dialog.InitialDirectory = ProgramDirectory;
			dialog.FileName = filename;
			dialog.DefaultExt = ".txt";
			dialog.Filter = "Programs|*.txt|All files|*.*";

			bool? result = dialog.ShowDialog();
			if (result != true || String.IsNullOrEmpty(dialog.FileName)) return;

			AddHistory(dialog.FileName);

			LoadProgram(dialog.FileName);
		}

		public void LoadProgram(string filename)
		{
			Stop();

			Title = WindowTitle + " - " + System.IO.Path.GetFileNameWithoutExtension(filename);

			ProgramFilename = System.IO.Path.GetFileName(filename);
			ProgramDirectory = System.IO.Path.GetDirectoryName(filename);

			if (ProgramDirectory != Properties.Settings.Default.ProgramDirectory)
			{
				Properties.Settings.Default.ProgramDirectory = ProgramDirectory;
				Properties.Settings.Default.Save();
			}

			Cursor cursor = Mouse.OverrideCursor;
			Mouse.OverrideCursor = Cursors.Wait;

			try
			{
				using (System.IO.StreamReader reader = new(filename))
				{
					editor.Document.Blocks.Clear();
					editor.AppendText(reader.ReadToEnd());
					editor.Selection.Select(editor.Document.ContentStart, editor.Document.ContentStart);
				}

				SyntaxHighlight();

				Mouse.OverrideCursor = cursor;
			}
			catch (Exception ex)
			{
				Mouse.OverrideCursor = cursor;

				MessageBox.Show(ex.Message, "Load program", MessageBoxButton.OK, MessageBoxImage.Error);

				editor.Document.Blocks.Clear();
			}

			Reset();

			Program = GetProgram();
			StatusMessage.Text = "Program length = " + Program.Length + " (" + RemoveComments(Program).Length + " compacted)";
		}

		private void SaveCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void SaveCommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			SaveProgram();
		}
		private void SaveAs_Click(object sender, RoutedEventArgs e)
		{
			SaveAsProgram();
		}

		private void SaveProgram()
		{
			if (ProgramDirectory == "" || ProgramFilename == "")
			{
				SaveAsProgram();
				return;
			}

			string filename = System.IO.Path.Combine(ProgramDirectory, ProgramFilename);

			Title = WindowTitle + " - " + System.IO.Path.GetFileNameWithoutExtension(filename);

			Cursor cursor = Mouse.OverrideCursor;
			Mouse.OverrideCursor = Cursors.Wait;

			try
			{
				using (System.IO.StreamWriter writer = new(filename))
				{
					writer.WriteLine(GetProgram());
				}

				Mouse.OverrideCursor = cursor;
			}
			catch (Exception ex)
			{
				Mouse.OverrideCursor = cursor;

				MessageBox.Show(ex.Message, "Save program", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void SaveAsProgram()
		{
			string filename = ProgramFilename;
			if (filename == "") filename = DEFAULT_FILENAME + DEFAULT_EXTENSION;

			Microsoft.Win32.SaveFileDialog dialog = new();
			dialog.InitialDirectory = ProgramDirectory;
			dialog.FileName = filename;
			dialog.DefaultExt = ".txt";
			dialog.Filter = "Programs|*.txt|All files|*.*";

			bool? result = dialog.ShowDialog();
			if (result != true || String.IsNullOrEmpty(dialog.FileName)) return;

			ProgramFilename = System.IO.Path.GetFileName(dialog.FileName);
			ProgramDirectory = System.IO.Path.GetDirectoryName(dialog.FileName);

			if (ProgramDirectory != Properties.Settings.Default.ProgramDirectory)
			{
				Properties.Settings.Default.ProgramDirectory = ProgramDirectory;
				Properties.Settings.Default.Save();
			}

			AddHistory(dialog.FileName);

			SaveProgram();
		}

		private void ReloadCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		private void ReloadCommandExecute(object sender, ExecutedRoutedEventArgs e)
		{
			ReloadProgram();
		}

		private void ReloadProgram()
		{
			Stop();

			Cursor cursor = Mouse.OverrideCursor;
			Mouse.OverrideCursor = Cursors.Wait;

			try
			{
				string filename = System.IO.Path.Combine(ProgramDirectory, ProgramFilename);
				using (System.IO.StreamReader reader = new(filename))
				{
					editor.Document.Blocks.Clear();
					editor.AppendText(reader.ReadToEnd());
					editor.Selection.Select(editor.Document.ContentStart, editor.Document.ContentStart);
				}

				SyntaxHighlight();

				Mouse.OverrideCursor = cursor;
			}
			catch (Exception ex)
			{
				Mouse.OverrideCursor = cursor;

				MessageBox.Show(ex.Message, "Load program", MessageBoxButton.OK, MessageBoxImage.Error);

				editor.Document.Blocks.Clear();
			}

			Reset();
		}

		private void UploadMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (!DevicePort.IsOpen())
			{
				MessageBox.Show("Virtual serial port not open", "Upload program", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			CommandBuffer[0] = (byte) Command.RESET;
			DevicePort.Write(CommandBuffer, 1);

			CommandBuffer[0] = (byte) Command.SET_SPEED;
			CommandBuffer[1] = (byte) Properties.Settings.Default.ClockSpeed;
			DevicePort.Write(CommandBuffer, 2);

			CommandBuffer[0] = (byte) Command.SET_HIGHLIGHT;
			CommandBuffer[1] = Properties.Settings.Default.TapeheadHighlighting ? (byte) 1 : (byte) 0;
			DevicePort.Write(CommandBuffer, 2);

			Program = GetProgram();
			string prog = RemoveComments(Program);
			int ndx = 0, len = prog.Length;

			// MessageBox.Show(prog, "Upload program", MessageBoxButton.OK, MessageBoxImage.Information);

			if (len >= MAX_PROGRAM)
			{
				MessageBox.Show("Program too large", "Upload program", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			while (len > 0)
			{
				CommandBuffer[0] = (byte) Command.LOAD;
				int n = Math.Min(len, 64-1);
				for (int i = 1; i <= n; i++) CommandBuffer[i] = (byte) prog[ndx++];
				DevicePort.Write(CommandBuffer, 1+n);
				len -= n;
			}

			Reset();
		}

		private void StoreMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (!DevicePort.IsOpen())
			{
				MessageBox.Show("Virtual serial port not open", "Store program", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			CommandBuffer[0] = (byte) Command.STORE;
			DevicePort.Write(CommandBuffer, 1);
		}

		private void ReconnectMenuItem_Click(object sender, RoutedEventArgs e)
		{
			DevicePort.Close();

			if (!DevicePort.Open()) MessageBox.Show("Unable to open port", "Virtual serial port", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			else StatusMessage.Text = "Virtual serial port open";
		}

		private void DarkMode_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.DarkMode = DarkModeMenuItem.IsChecked;
			Properties.Settings.Default.Save();

			background.Background = Properties.Settings.Default.DarkMode ? Brushes.Black : Brushes.White;
			outline.BorderBrush = Properties.Settings.Default.DarkMode ? Brushes.White : Brushes.Black;
			editor.Background = Properties.Settings.Default.DarkMode ? Brushes.Black : Brushes.White;

			DrawTape();

			SyntaxHighlight();
		}

		private void SyntaxHighlighting_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.SyntaxHighlighting = SyntaxHighlightingMenuItem.IsChecked;
			Properties.Settings.Default.Save();

			SyntaxHighlight();
		}

		private void RepeatStep_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.RepeatStep = RepeatStepMenuItem.IsChecked;
			Properties.Settings.Default.Save();
		}

		private void ClockSpeed_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.ClockSpeed = Convert.ToInt32((e.Source as MenuItem).Tag);
			Properties.Settings.Default.Save();

			StepTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / Properties.Settings.Default.ClockSpeed);

			CommandBuffer[0] = (byte) Command.SET_SPEED;
			CommandBuffer[1] = (byte) Properties.Settings.Default.ClockSpeed;
			DevicePort.Write(CommandBuffer, 2);
		}

		private void TapeheadHighlighting_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.TapeheadHighlighting = TapeheadHighlightingMenuItem.IsChecked;
			Properties.Settings.Default.Save();

			CommandBuffer[0] = (byte) Command.SET_HIGHLIGHT;
			CommandBuffer[1] = Properties.Settings.Default.TapeheadHighlighting ? (byte) 1 : (byte) 0;
			DevicePort.Write(CommandBuffer, 2);
		}

		private void Instructions_Click(object sender, RoutedEventArgs e)
		{
			#if DEBUG
			try {System.Windows.Forms.Help.ShowHelp(null, AppDomain.CurrentDomain.BaseDirectory + "..\\..\\" + "instructions.md");}
			#else
			try {System.Windows.Forms.Help.ShowHelp(null, AppDomain.CurrentDomain.BaseDirectory + "instructions.md");}
			#endif
			catch (Exception) {}
		}

		private void About_Click(object sender, RoutedEventArgs e)
		{
			const string about = "Version 1.0\n" + "Written by James Hutchby\n" + "james@madlab.org\n" + "www.madlab.org\n" + "@clubmadlab";
			MessageBox.Show(about, "Turing Machine SAO", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
