#include "system.h"
#include "usb/usb.h"


// PIC16F1454 configuration bit settings:
#if defined (USE_INTERNAL_OSC)
    // CONFIG1
    #pragma config FOSC = INTOSC		// Oscillator Selection Bits (INTOSC oscillator: I/O function on CLKIN pin)
    #pragma config WDTE = OFF			// Watchdog Timer Enable (WDT disabled)
    #pragma config PWRTE = OFF		// Power-up Timer Enable (PWRT disabled)
    #pragma config MCLRE = ON			// MCLR Pin Function Select (MCLR/VPP pin function is digital input)
    #pragma config CP = OFF			// Flash Program Memory Code Protection (Program memory code protection is disabled)
    #pragma config BOREN = ON			// Brown-out Reset Enable (Brown-out Reset enabled)
    #pragma config CLKOUTEN = OFF		// Clock Out Enable (CLKOUT function is disabled. I/O or oscillator function on the CLKOUT pin)
    #pragma config IESO = OFF			// Internal/External Switchover Mode (Internal/External Switchover Mode is disabled)
    #pragma config FCMEN = OFF		// Fail-Safe Clock Monitor Enable (Fail-Safe Clock Monitor is disabled)

    // CONFIG2
    #pragma config WRT = OFF			// Flash Memory Self-Write Protection (Write protection off)
    #pragma config CPUDIV = NOCLKDIV	// CPU System Clock Selection Bit (NO CPU system divide)
    #pragma config USBLSCLK = 48MHz	// USB Low Speed Clock Selection bit (System clock expects 48 MHz, FS/LS USB CLKENs divide-by is set to 8.)
    #pragma config PLLMULT = 3x		// PLL Multipler Selection Bit (3x Output Frequency Selected)
    #pragma config PLLEN = ENABLED		// PLL Enable Bit (3x or 4x PLL Enabled)
    #pragma config STVREN = ON		// Stack Overflow/Underflow Reset Enable (Stack Overflow or Underflow will cause a Reset)
    #pragma config BORV = LO			// Brown-out Reset Voltage Selection (Brown-out Reset Voltage (Vbor), low trip point selected.)
    #pragma config LPBOR = OFF		// Low-Power Brown Out Reset (Low-Power BOR is disabled)
    #pragma config LVP = ON			// Low-Voltage Programming Enable (High-voltage on MCLR/VPP must be used for programming)
#else
    // CONFIG1
    #pragma config FOSC = HS			// Oscillator Selection Bits (HS Oscillator, High-speed crystal/resonator connected between OSC1 and OSC2 pins)
    #pragma config WDTE = OFF			// Watchdog Timer Enable (WDT disabled)
    #pragma config PWRTE = OFF		// Power-up Timer Enable (PWRT disabled)
    #pragma config MCLRE = OFF		// MCLR Pin Function Select (MCLR/VPP pin function is digital input)
    #pragma config CP = OFF			// Flash Program Memory Code Protection (Program memory code protection is disabled)
    #pragma config BOREN = ON			// Brown-out Reset Enable (Brown-out Reset enabled)
    #pragma config CLKOUTEN = ON		// Clock Out Enable (CLKOUT function is disabled. I/O or oscillator function on the CLKOUT pin)
    #pragma config IESO = OFF			// Internal/External Switchover Mode (Internal/External Switchover Mode is disabled)
    #pragma config FCMEN = OFF		// Fail-Safe Clock Monitor Enable (Fail-Safe Clock Monitor is disabled)

    // CONFIG2
    #pragma config WRT = OFF			// Flash Memory Self-Write Protection (Write protection off)
    #pragma config CPUDIV = NOCLKDIV	// CPU System Clock Selection Bit (NO CPU system divide)
    #pragma config USBLSCLK = 48MHz	// USB Low Speed Clock Selection bit (System clock expects 48 MHz, FS/LS USB CLKENs divide-by is set to 8.)
    #pragma config PLLMULT = 4x		// PLL Multipler Selection Bit (4x Output Frequency Selected)
    #pragma config PLLEN = ENABLED		// PLL Enable Bit (3x or 4x PLL Enabled)
    #pragma config STVREN = ON		// Stack Overflow/Underflow Reset Enable (Stack Overflow or Underflow will cause a Reset)
    #pragma config BORV = LO			// Brown-out Reset Voltage Selection (Brown-out Reset Voltage (Vbor), low trip point selected.)
    #pragma config LPBOR = OFF		// Low-Power Brown Out Reset (Low-Power BOR is disabled)
    #pragma config LVP = OFF			// Low-Voltage Programming Enable (High-voltage on MCLR/VPP must be used for programming)
#endif


extern uint16_t Ticks;

extern void LED_Enable(void);
extern void BUTTON_Enable(void);


/*********************************************************************
* Function: void SYSTEM_Initialize(SYSTEM_STATE state)
*
* Overview: Initializes the system.
*
* PreCondition: None
*
* Input:  SYSTEM_STATE - the state to initialize the system into
*
* Output: None
*
********************************************************************/

void SYSTEM_Initialize(SYSTEM_STATE state)
{
	switch (state)
	{
	case SYSTEM_STATE_USB_START:
		#if defined(USE_INTERNAL_OSC)
		// turn on active clock tuning for USB full speed operation from the INTOSC
		OSCCON = 0xFC;  // HFINTOSC @ 16MHz, 3X PLL, PLL enabled
		ACTCON = 0x90;  // active clock tuning enabled for USB
		#endif
		LED_Enable();
		BUTTON_Enable();
		break;
            
	case SYSTEM_STATE_USB_SUSPEND: 
		break;
            
	case SYSTEM_STATE_USB_RESUME:
		break;
	}
}


#if (__XC8_VERSION < 2000)
	#define INTERRUPT interrupt
#else
	#define INTERRUPT __interrupt()
#endif

void INTERRUPT SYS_InterruptHigh(void)
{
	if (PIR1bits.TMR1IF == 1)
	{
		Ticks++;

		// 1ms
		TMR1 = (unsigned) -12000;

		PIR1bits.TMR1IF = 0;

		return;
	}

	#if defined(USB_INTERRUPT)
	USBDeviceTasks();
	#endif
}
