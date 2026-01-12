/***************************************************************************
* FILE:      turing.s											*
* CONTENTS:  Turing Machine routines								*
* COPYRIGHT: MadLab Ltd. 2025										*
* AUTHOR:    James Hutchby										*
* UPDATED:   17/12/25											*
***************************************************************************/

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>


//**************************************************************************
// linkage
//**************************************************************************

extern uint16_t Ticks;

extern void LED_Flash(void);
extern void reset_leds(void);
extern void set_led(void);

void error(int err);
void skip_instruction(void);


//**************************************************************************
// constants
//**************************************************************************

// error return value
#define ERROR 0x7fff

// number of squares on the tape
#define NUM_SQUARES 27

// maximum number of variables
#define MAX_VARIABLES 10

// maximum number of significant characters in label and variable names
#define NAME_LEN 10

// maximum program length
#define MAX_PROGRAM 256


//**************************************************************************
// variables
//**************************************************************************

// tape symbols
char Symbols[NUM_SQUARES];

// program variable names and values
char VariableNames[MAX_VARIABLES][NAME_LEN+1];
int8_t VariableValues[MAX_VARIABLES];

// tape head posiiton
int8_t HeadPosition;

// current program
char Program[MAX_PROGRAM+1] = {'\0'};

// program length
uint8_t ProgramLength = 0;

// program position
int16_t ProgramPosition;

// wait periods
int8_t WaitPeriods;

// saved settings
struct
{
	// Turing Machine clock speed - 1, 2, 5, 10, 20, 40 instructions/second
	uint8_t ClockSpeed;

	// tapehead highlighting
	bool TapeheadHighlighting;
}
Settings;

// LED components
uint8_t cnt, red, green, blue;

// timer enabled
bool TimerEnabled = false;

// timer counter
uint16_t TimerCnt = 1000 / 1;

// previous ticks
uint16_t PrevTicks = (unsigned) -1;


//**************************************************************************
// flash functions
//**************************************************************************

// flash storage locations (end of memory)
#define PROGRAM_BASE 0x1e00
#define PROGRAM_SIZE (((MAX_PROGRAM+1)/32+1)*32)
#define SETTINGS_BASE (PROGRAM_BASE+PROGRAM_SIZE)
#define SETTINGS_SIZE ((sizeof(Settings)/32+1)*32)
const uint8_t Program_[PROGRAM_SIZE] __at(PROGRAM_BASE) = {0};
const uint8_t Settings_[SETTINGS_SIZE] __at(SETTINGS_BASE) = {0};

// reads a byte from memory
uint8_t read_byte(uint16_t addr)
{
	PMADR = addr;

	PMCON1bits.CFGS = 0;
	PMCON1bits.RD = 1;
	__asm("nop");
	__asm("nop");

	return PMDATL;
}

// reads from memory
void read_mem(uint16_t addr, uint16_t len, uint8_t* dst)
{
	while (len-- > 0) *dst++ = read_byte(addr++);
}

// writes to memory
void write_mem(uint16_t addr, uint16_t len, uint8_t* src)
{
	INTCONbits.GIE = 0;

	#define ROW_ERASE 32

	// erase rows
	for (uint8_t i = 0; i <= (len/ROW_ERASE); i++)
	{
		PMADR = addr + i * ROW_ERASE;

		PMCON1bits.CFGS = 0;
		PMCON1bits.FREE = 1;
		PMCON1bits.WREN = 1;

		PMCON2 = 0x55;
		PMCON2 = 0xaa;
		PMCON1bits.WR = 1;
		__asm("nop");
		__asm("nop");

		PMCON1bits.WREN = 0;
	}

	#define WRITE_LATCHES 32

	// write rows
	for (uint8_t i = 0; i <= (len/WRITE_LATCHES); i++)
	{
		PMADR = addr + i * WRITE_LATCHES;

		PMCON1bits.CFGS = 0;
		PMCON1bits.WREN = 1;

		PMCON1bits.LWLO = 1;

		while (true)
		{
			PMDAT = (uint16_t) *src++;

			#define MASK (WRITE_LATCHES-1)
			if ((PMADRL & MASK) == MASK) break;

			PMCON2 = 0x55;
			PMCON2 = 0xaa;
			PMCON1bits.WR = 1;
			__asm("nop");
			__asm("nop");

			PMADR++;
		}

		PMCON1bits.LWLO = 0;

		PMCON2 = 0x55;
		PMCON2 = 0xaa;
		PMCON1bits.WR = 1;
		__asm("nop");
		__asm("nop");

		PMCON1bits.WREN = 0;
	}

	INTCONbits.GIE = 1;
}


//**************************************************************************
// helper functions
//**************************************************************************

// errors
enum
{
	ERR_SYNTAX_ERROR = 1,
	ERR_INSTRUCTION_ERROR = 2,
	ERR_OPERAND_ERROR = 3,
	ERR_TOO_MANY_VARIABLES = 4,
	ERR_VARIABLE_NOT_FOUND = 5,
	ERR_LABEL_NOT_FOUND = 6
};

// displays all tape symbols
void update_tape(void)
{
	#define HI_BRIGHTNESS 0x60
	#define LO_BRIGHTNESS 0x20

	reset_leds();

	char* p = Symbols;

	for (uint8_t i = 0; i < NUM_SQUARES; i++)
	{
		uint8_t brightness = LO_BRIGHTNESS;
		if (Settings.TapeheadHighlighting && i == HeadPosition) brightness = HI_BRIGHTNESS;

		switch (*p++)
		{
		case 'R':
			red = brightness; green = blue = 0;
			break;
		case 'G':
			green = brightness; red = blue = 0;
			break;
		case 'B':
			blue = brightness; red = green = 0;
			break;
		case 'C':
			green = blue = brightness; red = 0;
			break;
		case 'M':
			red = blue = brightness; green = 0;
			break;
		case 'Y':
			red = green = brightness; blue = 0;
			break;
		case 'W':
			red = green = blue = brightness;
			break;
		case 'K':
			red = green = blue = 0;
			break;
		default:
			red = green = blue = 0;
			break;
		}

		set_led();
	}
}

// returns length of a string (excluding zero terminator)
uint8_t str_len(char* s)
{
	uint8_t len = 0;
	while (*s++ != '\0') len++;
	return len;
}

// returns true if two strings match
bool cmp_strs(char* s1, char* s2)
{
	while (true)
	{
		if (*s1 == '\0' && *s2 == '\0') return true;
		if (*s1++ != *s2++) return false;
	}
}

// copies a string
void copy_str(char* s, char* d)
{
	char c;
	do
	{
		c = *d++ = *s++;
	}
	while (c != '\0');
}

// returns current character in program
inline char current(void)
{
	return ProgramPosition >= ProgramLength ? '\0' : Program[ProgramPosition];
}

// steps over current character in program
inline void step(void)
{
	if (ProgramPosition < ProgramLength) ProgramPosition++;
}

// returns next character in program
inline char next(void)
{
	return ProgramPosition >= ProgramLength ? '\0' : (++ProgramPosition >= ProgramLength ? '\0' : Program[ProgramPosition]);
}

// returns true if end of program
inline bool end_of_program(void)
{
	return ProgramPosition >= ProgramLength;
}

// returns true if valid character in label or variable name
bool is_name(char c)
{
	return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
}

// returns true if symbol
bool is_symbol(char c)
{
	return c == 'R' || c == 'G' || c == 'B' || c == 'C' || c == 'M' || c == 'Y' || c == 'W' || c == 'K';
}

// returns true if arithmetic or bitwise operator
bool is_operator(char c)
{
	return c == '+' || c == '-' || c == '*' || c == '/' || c == '&' || c == '|';
}

// returns true if start of expression
bool is_expression(char c)
{
	return (c >= '0' && c <= '9') || c == '-' || c == '$' || c == '(';
}

// parses a decimal number
int8_t get_number(void)
{
	bool negate = current() == '-';
	if (negate) step();

	int8_t n = 0;
	while (current() != '\0' && (current() >= '0' && current() <= '9'))
	{
		n = n * 10 + (int8_t) (current() - '0');
		step();
	}

	return negate ? -n : n;
}

// skips over whitespace and comments, returns true if end of program
bool skip_space(void)
{
	while (true)
	{
		if (current() == ';')
		{
			// comment - skip to end of line
			while (next() != '\0' && current() != '\n') ;
		}
		else if (current() == ' ' || current() == '\t' || current() == '\r' || current() == '\n')
		{
			step();
		}
		else
		{
			break;
		}
	}

	return end_of_program();
}

// parses a label or variable name
char* get_name(void)
{
	static char name[NAME_LEN+1];
	uint8_t i = 0;

	skip_space();
	while (is_name(current()))
	{
		if (i < NAME_LEN) name[i++] = current();
		step();
	}
	name[i] = '\0';

	return name;
}

// finds a variable, returns variable index or -1 if not found
int8_t find_variable(char* name)
{
	for (uint8_t i = 0; i < MAX_VARIABLES; i++)
	{
		if (VariableNames[i][0] == '\0') continue;
		if (cmp_strs(VariableNames[i], name)) return (int8_t) i;
	}
	return -1;
}

// adds a new variable, fed with name and value, returns variable index or -1 if error
int8_t add_variable(char* name, int8_t value)
{
	uint8_t i;
	for (i = 0; i < MAX_VARIABLES; i++)
	{
		if (VariableNames[i][0] == '\0') break;
	}
	if (i >= MAX_VARIABLES) return -1;

	copy_str(name, VariableNames[i]);
	VariableValues[i] = value;

	return (int8_t) i;
}

// parses an operand (symbol, variable or decimal number), returns ERROR if error
int16_t get_operand(void)
{
	skip_space();

	if (is_symbol(current()))
	{
		char symbol = current();
		step();
		// off-tape squares read as black
		if (HeadPosition < 0 || HeadPosition >= NUM_SQUARES) return symbol == 'K' ? 1 : 0;
		return Symbols[HeadPosition] == symbol ? 1 : 0;
	}

	else if (current() == '$')
	{
		step();
		char* variable = get_name();
		int8_t ndx = find_variable(variable);
		if (ndx != -1) return VariableValues[ndx];
		error(ERR_VARIABLE_NOT_FOUND);
		return ERROR;
	}

	else if (current() == '-' || (current() >= '0' && current() <= '9'))
	{
		return get_number();
	}

	error(ERR_OPERAND_ERROR);
	return ERROR;
}

// parses an expression, returns ERRIR if error
int16_t __reentrant get_expression(void)
{
	skip_space();

	int16_t result;
	if (current() == '(')
	{
		step();
		result = get_expression();
		if (current() != ')') return ERROR;
		step();
	}
	else
	{
		result = get_operand();
	}
	if (result == ERROR) return ERROR;

	while (true)
	{
		skip_space();

		if (current() == ')') break;

		char op = current();
		if (!is_operator(op)) break;

		step();

		int16_t operand = get_operand();
		if (operand == ERROR) return ERROR;

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

	// signed 8-bit
	return (int16_t) (int8_t) result;
}


//**************************************************************************
// Turing Machine functions
//**************************************************************************

// handles <, <n, <<, >, >n, >>
void do_movement(void)
{
	if (current() == '<')
	{
		if (next() == '<')
		{
			step();
			// first square
			HeadPosition = 0;
		}
		else if (is_expression(current()))
		{
			int16_t n = get_expression();
			if (n == ERROR) return;
			HeadPosition -= (int8_t) n;
		}
		else
		{
			HeadPosition--;
		}
	}

	else if (current() == '>')
	{
		if (next() == '>')
		{
			step();
			// last square
			HeadPosition = NUM_SQUARES-1;
		}
		else if (is_expression(current()))
		{
			int16_t n = get_expression();
			if (n == ERROR) return;
			HeadPosition += (int8_t) n;
		}
		else
		{
			HeadPosition++;
		}
	}

	// allow one square off tape
	if (HeadPosition < 0) HeadPosition = -1;
	else if (HeadPosition >= NUM_SQUARES) HeadPosition = NUM_SQUARES;
}

void skip_movement(void)
{
	if (current() == '<')
	{
		if (next() == '<') step();
		else if (is_expression(current())) get_expression();
	}

	else if (current() == '>')
	{
		if (next() == '>') step();
		else if (is_expression(current())) get_expression();
	}
}

// handles assignments
void do_assignment(void)
{
	step();
	char* variable = get_name();
	int8_t ndx = find_variable(variable);
	if (ndx == -1)
	{
		ndx = add_variable(variable, 0);
		if (ndx == -1)
		{
			error(ERR_TOO_MANY_VARIABLES);
			return;
		}
	}

	skip_space();

	if (current() == '+')
	{
		if (next() == '+')
		{
			step();
			if (VariableValues[ndx] < 127) VariableValues[ndx]++;
			return;
		}
	}

	else if (current() == '-')
	{
		if (next() == '-')
		{
			step();
			if (VariableValues[ndx] > -128) VariableValues[ndx]--;
			return;
		}
	}

	else if (current() == '=')
	{
		step();
		int16_t x = get_expression();
		if (x == ERROR) return;
		VariableValues[ndx] = (int8_t) x;
		return;
	}

	error(ERR_SYNTAX_ERROR);
}

void skip_assignment(void)
{
	step();
	get_name();

	skip_space();

	if (current() == '+')
	{
		if (next() == '+') step();
	}

	else if (current() == '-')
	{
		if (next() == '-') step();
	}

	else if (current() == '=')
	{
		step();
		get_expression();
	}
}

// handles conditionals
void do_conditional(void)
{
	bool test;

	if (next() == '!')
	{
		step();
		int16_t x = get_expression();
		if (x == ERROR) return;
		test = x == 0;
	}

	else if (current() == '>')
	{
		if (next() == '=')
		{
			step();
			int16_t x = get_expression();
			if (x == ERROR) return;
			test = x >= 0;
		}
		else
		{
			int16_t x = get_expression();
			if (x == ERROR) return;
			test = x > 0;
		}
	}

	else if (current() == '<')
	{
		if (next() == '=')
		{
			step();
			int16_t x = get_expression();
			if (x == ERROR) return;
			test = x <= 0;
		}
		else
		{
			int16_t x = get_expression();
			if (x == ERROR) return;
			test = x < 0;
		}
	}

	else
	{
		int16_t x = get_expression();
		if (x == ERROR) return;
		test = x != 0;
	}

	if (!test)
	{
		skip_space();
		skip_instruction();
	}
}

void skip_conditional(void)
{
	if (next() == '!')
	{
		step();
	}
	else if (current() == '>')
	{
		if (next() == '=') step();
	}
	else if (current() == '<')
	{
		if (next() == '=') step();
	}
	get_expression();

	skip_space();
	skip_instruction();
}

// handles branches
void do_branch(void)
{
	static char label[NAME_LEN+1];

	step();
	copy_str(get_name(), label);

	ProgramPosition = 0;
	while (true)
	{
		if (current() == '\0')
		{
			error(ERR_LABEL_NOT_FOUND);
			return;
		}

		skip_space();

		if (current() != '#')
		{
			step();
			continue;
		}
		step();
		if (cmp_strs(get_name(), label)) return;
	}
}

void skip_branch(void)
{
	step();
	get_name();
}

// handles waits
void do_wait(void)
{
	WaitPeriods = 1;
	if (is_expression(next()))
	{
		int16_t n = get_expression();
		if (n == ERROR) return;
		WaitPeriods = (int8_t) n;
		if (WaitPeriods == 0) WaitPeriods = -1;
	}
}

void skip_wait(void)
{
	if (is_expression(next())) get_expression();
}

// handles R, G, B, C, M, Y, W, K
void do_set(void)
{
	char symbol = current();
	step();
	// off-tape squares can't be set
	if (HeadPosition < 0 || HeadPosition >= NUM_SQUARES) return;
	Symbols[HeadPosition] = symbol;
}

void skip_set(void)
{
	step();
}

// handles the next instruction
void do_instruction(void)
{
	if (current() == '<' || current() == '>')
	{
		do_movement();
	}

	else if (current() == '$')
	{
		do_assignment();
	}

	else if (current() == '?')
	{
		do_conditional();
	}

	else if (current() == '^')
	{
		do_branch();
	}

	else if (current() == '%')
	{
		do_wait();
	}

	else if (is_symbol(current()))
	{
		do_set();
	}
	else
	{
		error(ERR_INSTRUCTION_ERROR);
	}
}

// skips over the next instruction
void skip_instruction(void)
{
	if (current() == '<' || current() == '>')
	{
		skip_movement();
	}

	else if (current() == '$')
	{
		skip_assignment();
	}

	else if (current() == '?')
	{
		skip_conditional();
	}

	else if (current() == '^')
	{
		skip_branch();
	}

	else if (current() == '%')
	{
		skip_wait();
	}

	else if (is_symbol(current()))
	{
		skip_set();
	}
}


//**************************************************************************
// executive functions
//**************************************************************************

void StartTuring(void)
{
	TimerCnt = 1000 / Settings.ClockSpeed;
	TimerEnabled = true;
}

void StopTuring(void)
{
	TimerEnabled = false;
}

// resets the Turing Machine
void ResetTuring(void)
{
	StopTuring();

	// leftmost square
	HeadPosition = 0;

	// start of program
	ProgramPosition = 0;

	// no wait
	WaitPeriods = 0;

	// reset timer
	TimerCnt = 1000 / Settings.ClockSpeed;

	// clear symbols
	for (uint8_t i = 0; i < NUM_SQUARES; i++) Symbols[i] = 'K';

	// clear variables
	for (uint8_t i = 0; i < MAX_VARIABLES; i++) VariableNames[i][0] = '\0';

	// update LEDs
	update_tape();
}

// steps the Turing Machine, returns false if end of program or wait
bool StepTuring(void)
{
	// if halted
	if (WaitPeriods < 0) return false;

	if (WaitPeriods > 0)
	{
		WaitPeriods--;
		return false;
	}

	if (*Program == '\0')
	{
		StopTuring();
		return false;
	}

	if (ProgramPosition >= ProgramLength)
	{
		StopTuring();
		return false;
	}

	// step over whitespace and labels
	while (true)
	{
		skip_space();
		if (current() != '#') break;
		step();
		while (is_name(current())) step();
	}

	if (current() == '\0')
	{
		StopTuring();
		return false;
	}

	do_instruction();

	update_tape();

	if (WaitPeriods > 0)
	{
		WaitPeriods--;
		return false;
	}

	return true;
}

void TuringExec(void)
{
	if (PrevTicks == Ticks) return;
	PrevTicks = Ticks;

	if (!TimerEnabled || --TimerCnt != 0) return;
	TimerCnt = 1000 / Settings.ClockSpeed;

	StepTuring();
}

void error(int err)
{
	while (err-- > 0) LED_Flash();
	StopTuring();
	ProgramPosition = (int8_t) ProgramLength;
}

// processes USB commands, fed with buffer pointer and character count
void ProcessCommand(uint8_t* buffer, uint8_t cnt)
{
	// commands
	enum {RESET = 1, LOAD, RUN, STEP, SET_SPEED, SET_HIGHLIGHT, STORE};

	switch (buffer[0])
	{
	case RESET:
		ResetTuring();
		break;

	case LOAD:
		for (int8_t i = 1; i < cnt; i++)
		{
			if (ProgramPosition < MAX_PROGRAM) Program[ProgramPosition++] = buffer[i];
		}
		Program[ProgramPosition] = '\0';
		ProgramLength = str_len(Program);
		break;

	case RUN:
		ResetTuring();
		StartTuring();
		break;

	case STEP:
		StopTuring();
		StepTuring();
		break;

	case SET_SPEED:
		if (cnt > 1) Settings.ClockSpeed = buffer[1], TimerCnt = 1000 / Settings.ClockSpeed;
		break;

	case SET_HIGHLIGHT:
		if (cnt > 1) Settings.TapeheadHighlighting = buffer[1] != 0;
		break;

	case STORE:
		write_mem(SETTINGS_BASE, sizeof(Settings), (uint8_t*) &Settings);
		write_mem(PROGRAM_BASE, sizeof(Program), (uint8_t*) Program);
		LED_Flash();
		break;
	}
}

void InitTuring(void)
{
	read_mem(SETTINGS_BASE, sizeof(Settings), (uint8_t*) &Settings);
	read_mem(PROGRAM_BASE, sizeof(Program), (uint8_t*) Program);

	ProgramLength = str_len(Program);

	if (read_byte(SETTINGS_BASE) == 0xff || read_byte(SETTINGS_BASE) == 0)
	{
		Settings.ClockSpeed = 1;
		Settings.TapeheadHighlighting = true;
	}

	if (read_byte(PROGRAM_BASE) != 0xff && read_byte(PROGRAM_BASE) != 0)
	{
		ResetTuring();
		StartTuring();
	}
}
