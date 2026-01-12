#include <xc.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
#include <stddef.h>

#include "system.h"

#include "usb/usb.h"
#include "usb/usb_config.h"
#include "usb/usb_device.h"
#include "usb/usb_device_cdc.h"


extern void reset_leds(void);
extern void test_leds(void);
extern void InitTuring(void);
extern void TuringExec(void);
extern void ProcessCommand(uint8_t*, uint8_t);

void init_timer(void);
void APP_DeviceCDCEmulatorTasks(void);


// ms counter
uint16_t Ticks = 0;

// auto run flag
bool auto_run = false;

// static uint8_t USB_Out_Buffer[CDC_DATA_OUT_EP_SIZE];
static uint8_t USB_In_Buffer[CDC_DATA_IN_EP_SIZE];


// main program entry point
MAIN_RETURN main(void)
{
	SYSTEM_Initialize(SYSTEM_STATE_USB_START);

	init_timer();

	test_leds();

	USBDeviceInit();
	USBDeviceAttach();

	while (true)
	{
		SYSTEM_Tasks();

		#if defined(USB_POLLING)
		// Interrupt or polling method. If using polling, must call
		// this function periodically. This function will take care
		// of processing and responding to SETUP transactions
		// (such as during the enumeration process when you first
		// plug in). USB hosts require that USB devices should accept
		// and process SETUP packets in a timely fashion. Therefore,
		// when using polling, this function should be called
		// regularly (such as once every 1.8ms or faster** [see
		// inline code comments in usb_device.c for explanation when
		// "or faster" applies]) In most cases, the USBDeviceTasks()
		// function does not take very long to execute (ex: <100
		// instruction cycles) before it returns.
		USBDeviceTasks();
		#endif

		/* If the USB device isn't configured yet, we can't really do anything
		 * else since we don't have a host to talk to. So jump back to the
		 * top of the while loop. */
		// if (USBGetDeviceState() < CONFIGURED_STATE) continue;

		/* If we are currently suspended, then we need to see if we need to
		 * issue a remote wakeup. In either case, we shouldn't process any
		 * keyboard commands since we aren't currently communicating to the host
		 * thus just continue back to the start of the while loop. */
		// if (USBIsDeviceSuspended()) continue;

		// application specific tasks
		APP_DeviceCDCEmulatorTasks();

		if (!auto_run)
		{
			auto_run = true;
			InitTuring();
		}

		TuringExec();
	}
}


void init_timer(void)
{
	// instruction clock, 1:1 prescale
	T1CONbits.TMR1CS = 0;
	T1CONbits.T1CKPS = 0;

	T1GCON = 0;

	TMR1 = (unsigned) -1;

	PIR1bits.TMR1IF = 0;
	PIE1bits.TMR1IE = 1;
	INTCONbits.PEIE = 1;

	T1CONbits.TMR1ON = 1;
}


#define S1_PORT PORTAbits.RA5
#define S1_TRIS TRISAbits.TRISA5
#define S1_WPU WPUAbits.WPUA5

bool BUTTON_IsPressed(void)
{
	return S1_PORT == 0;
}

void BUTTON_Enable(void)
{
	ANSELA = 0;
	S1_WPU = 1;
	OPTION_REGbits.nWPUEN = 0;
	S1_TRIS = 1;
}


#define LED						//************
#define LED_LAT LATCbits.LATC1
#define LED_TRIS TRISCbits.TRISC1

void LED_On(void)
{
	#if defined (LED)
	LED_LAT = 1;
	#endif
}

void LED_Off(void)
{
	#if defined (LED)
	LED_LAT = 0;
	#endif
}

void LED_Enable(void)
{
	#if defined (LED)
	LED_TRIS = 0;
	#endif
}

void LED_Flash(void)
{
	#if defined (LED)
	#define delay() {for (long int i = 0; i < 100000L; i++) ; __asm("clrwdt");}
	LED_On();
	delay();
	LED_Off();
	delay();
	#endif
}


void APP_LEDUpdateUSBStatus(void)
{
	static uint16_t ledCount = 0;

	if (USBIsDeviceSuspended())
	{
		LED_Off();
		return;
	}

	switch(USBGetDeviceState())
	{
	case CONFIGURED_STATE:
		// We are configured. Blink fast. On for 75ms, off for 75ms.
		if (ledCount == 1) LED_On();
		else if (ledCount == 75) LED_Off();
		else if (ledCount > 150) ledCount = 0;
		break;

	default:
		// We aren't configured yet, but we aren't suspended so let's blink with
		// a slow pulse. On for 50ms, then off for 950ms.
		if (ledCount == 1) LED_On();
		else if (ledCount == 50) LED_Off();
		else if (ledCount > 950) ledCount = 0;
		break;
	}

	// increment the millisecond counter
	ledCount++;
}


#if defined(USB_CDC_SET_LINE_CODING_HANDLER)
void USART_mySetLineCodingHandler(void)
{
	// CDCSetBaudRate(cdc_notice.GetLineCoding.dwDTERate);
}
#endif


void APP_DeviceCDCEmulatorInitialize(void)
{
	CDCInitEP();

	line_coding.bCharFormat = 0;
	line_coding.bDataBits = 8;
	line_coding.bParityType = 0;
	line_coding.dwDTERate = 19200;

	// initialize array
	// for (int i = 0; i < sizeof(USB_Out_Buffer); i++) USB_Out_Buffer[i] = 0;
}


void APP_DeviceCDCEmulatorTasks(void)
{
	if (USBGetDeviceState() < CONFIGURED_STATE) return;

	if (USBIsDeviceSuspended()) return;

	uint8_t n = getsUSBUSART(USB_In_Buffer, 64);
	if (n > 0)
	{
		// LED_Flash();
		ProcessCommand(USB_In_Buffer, n);
	}

	// CDCTxService();
}
